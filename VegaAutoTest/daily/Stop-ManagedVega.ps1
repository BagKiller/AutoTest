param([Parameter(Mandatory=$true)][string]$AuditPath, [string]$WorkspaceRoot)
$ErrorActionPreference='Stop'
$records=@()
$workspaceBase='C:\AutoTest\workspaces\'
if($WorkspaceRoot){
    # Only a dedicated project workspace directly under C:\AutoTest\workspaces may be released.
    $WorkspaceRoot=[IO.Path]::GetFullPath($WorkspaceRoot).TrimEnd('\')
    if(-not $WorkspaceRoot.StartsWith($workspaceBase,[StringComparison]::OrdinalIgnoreCase) -or
       (Split-Path $WorkspaceRoot -Parent).TrimEnd('\') -ne $workspaceBase.TrimEnd('\')){
        throw "WorkspaceRoot must be a direct child of $workspaceBase"
    }
}
foreach($item in @(Get-CimInstance Win32_Process -Filter "Name='Vega.exe'")) {
    $image=$item.ExecutablePath
    # Only dedicated validation workspaces; never match C:\Vega or arbitrary processes.
    if(!$image -or !$image.StartsWith('C:\AutoTest\workspaces\',[StringComparison]::OrdinalIgnoreCase)){continue}
    $config=Join-Path (Split-Path $image) 'Resources\ComponentConfig.json'
    if(!(Test-Path -LiteralPath $config)){throw "Cannot establish Simulator ownership for PID $($item.ProcessId)"}
    $content=Get-Content -LiteralPath $config -Raw | ConvertFrom-Json
    $selected=$content.ToolConfigCollection.SelectedToolType
    $tool=@($content.ToolConfigCollection.ToolConfigs | Where-Object ToolType -eq $selected)
    if($tool.Count -ne 1 -or 'Simulator' -notin ($tool[0].RootCondition -split ',')){throw "Selected configuration is not Simulator for PID $($item.ProcessId)"}
    $process=Get-Process -Id $item.ProcessId -ErrorAction SilentlyContinue
    if(!$process){continue}
    if($process.Path -ne $image){throw 'Process identity changed'}
    $record=@{pid=$process.Id;image=$image;startedAt=$process.StartTime.ToString('o');action='force-stop validation Simulator';at=(Get-Date).ToString('o')}
    Stop-Process -InputObject $process -Force
    if(!$process.WaitForExit(10000)){throw "Validation process did not exit: $($process.Id)"}
    $record.restore=@()
    foreach($sessionFile in @(Get-ChildItem (Join-Path $PSScriptRoot 'sessions') -Filter session.json -Recurse -ErrorAction SilentlyContinue)) {
        $session=Get-Content -LiteralPath $sessionFile.FullName -Raw | ConvertFrom-Json
        if($session.state -ne 'PREPARED' -or $session.bin -ne (Split-Path $image)){continue}
        $profileArg=if($session.model -eq 'Vega100'){($session.profile -split '-')[1]}elseif($session.model -eq 'Vega300'){($session.profile -split '-')[1]}else{'Default'}
        & (Join-Path $PSScriptRoot 'Start-ModelValidation.ps1') -Action Restore -Model $session.model -Profile $profileArg -RepoRoot $session.repo -SessionPath $sessionFile.DirectoryName
        $record.restore+=@{session=$sessionFile.DirectoryName;state='RESTORED';sha256Verified=$true}
    }
    $records+=$record
    ConvertTo-Json -InputObject @($records) -Depth 8 | Set-Content -LiteralPath $AuditPath
}
ConvertTo-Json -InputObject @($records) -Depth 8 | Set-Content -LiteralPath $AuditPath
# Prepare can fail before any Vega process is started. Such sessions still own
# modified configuration and must not be forgotten by process-only cleanup.
foreach($sessionFile in @(Get-ChildItem (Join-Path $PSScriptRoot 'sessions') -Filter session.json -Recurse -ErrorAction SilentlyContinue)) {
    $session=Get-Content -LiteralPath $sessionFile.FullName -Raw | ConvertFrom-Json
    if($session.state -notin @('PREPARING','PREPARED')){continue}
    $resolvedRepo=[IO.Path]::GetFullPath($session.repo)
    if(-not $resolvedRepo.StartsWith('C:\AutoTest\workspaces\',[StringComparison]::OrdinalIgnoreCase)){continue}
    $profileArg=if($session.model -in @('Vega100','Vega300')){($session.profile -split '-')[1]}else{'Default'}
    & (Join-Path $PSScriptRoot 'Start-ModelValidation.ps1') -Action Restore -Model $session.model -Profile $profileArg -RepoRoot $resolvedRepo -SessionPath $sessionFile.DirectoryName
    $records+=@{action='restore orphan prepared session';session=$sessionFile.DirectoryName;sha256Verified=$true}
    ConvertTo-Json -InputObject @($records) -Depth 8 | Set-Content -LiteralPath $AuditPath
}
# Workspace release: after Vega sessions are restored above, stop any remaining
# process whose executable image is inside this project workspace (logger,
# editors, test hosts built there). Images elsewhere are never touched.
if($WorkspaceRoot){
    $prefix=$WorkspaceRoot+'\'
    foreach($item in @(Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) })){
        $process=Get-Process -Id $item.ProcessId -ErrorAction SilentlyContinue
        if(!$process -or $process.Path -ne $item.ExecutablePath){continue}
        $record=@{pid=$process.Id;image=$item.ExecutablePath;startedAt=$process.StartTime.ToString('o');action='stop process inside released workspace';at=(Get-Date).ToString('o')}
        Stop-Process -InputObject $process -Force
        $record.exited=$process.WaitForExit(10000)
        $records+=$record
    }
    ConvertTo-Json -InputObject @($records) -Depth 8 | Set-Content -LiteralPath $AuditPath
}
