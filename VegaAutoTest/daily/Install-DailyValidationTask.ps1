[CmdletBinding(SupportsShouldProcess)]
param([string]$TaskName='VegaAutoTest-DailyValidation', [string]$At='23:30', [string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
$shell='C:\Users\Administrator\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe'
$entry=Join-Path $PSScriptRoot 'Start-DailyValidation.ps1'
if(-not (Test-Path -LiteralPath $shell) -or -not (Test-Path -LiteralPath $entry)){throw 'Verified runtime/entrypoint is missing'}
$existing=Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
if($existing){
    if(@($existing.Actions).Count -ne 1 -or $existing.Actions[0].Execute -ne $shell -or $existing.Actions[0].Arguments -notlike ('*'+$entry+'*')){
        throw 'An unrelated task already uses this name; refusing to replace it'
    }
}
$time=[datetime]::ParseExact($At,'HH:mm',[Globalization.CultureInfo]::InvariantCulture)
$principal=New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited
$action=New-ScheduledTaskAction -Execute $shell -Argument ('-NoProfile -NonInteractive -WindowStyle Hidden -File "'+$entry+'" -Watch') -WorkingDirectory (Split-Path $PSScriptRoot)
$trigger=New-ScheduledTaskTrigger -Daily -At ([datetime]::Today.AddHours($time.Hour).AddMinutes($time.Minute))
$settings=New-ScheduledTaskSettingsSet -StartWhenAvailable -MultipleInstances IgnoreNew -ExecutionTimeLimit ([timespan]::FromHours(23)) `
    -RestartCount 2 -RestartInterval ([timespan]::FromMinutes(5)) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
if($PSCmdlet.ShouldProcess($TaskName,'Install/update direct program execution; existing Codex chat is no longer the validation scheduler')){
    Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal `
        -Description 'Vega Simulator daily commit validation: durable model-authored tests, isolated execution and reports.' -Force | Out-Null
    $task=Get-ScheduledTask -TaskName $TaskName
    $info=Get-ScheduledTaskInfo -TaskName $TaskName
    $receipt=@{taskName=$TaskName;action=@{execute=$task.Actions[0].Execute;arguments=$task.Actions[0].Arguments};
        nextRunTime=$info.NextRunTime.ToString('o');localTimeZone=[TimeZoneInfo]::Local.Id;multipleInstances=[string]$task.Settings.MultipleInstances;
        logonType=[string]$task.Principal.LogonType;entrySha256=(Get-FileHash -LiteralPath $entry).Hash;registeredAt=(Get-Date).ToString('o')}
    if($EvidenceDirectory){
        New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null
        $receipt | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'scheduled-task.json') -Encoding utf8
        Export-ScheduledTask -TaskName $TaskName | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'scheduled-task.xml') -Encoding utf8
    }
    $receipt | ConvertTo-Json -Depth 20
}
