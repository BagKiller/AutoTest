param([Parameter(Mandatory=$true)][string]$AuditPath)
$ErrorActionPreference='Stop'
$records=@()
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
