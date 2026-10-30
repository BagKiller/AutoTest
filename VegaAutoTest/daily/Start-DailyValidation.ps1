[CmdletBinding()]
param([string]$Batch, [switch]$Watch, [string]$LaunchDirectory, [switch]$RuntimeCheck)
$ErrorActionPreference='Stop'
$python='C:\Users\Administrator\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$controller=Join-Path $PSScriptRoot 'autopilot.py'
$env:PYTHONIOENCODING='utf-8'
$runtimeResult=& $python -B $controller --runtime-check
if($LASTEXITCODE -ne 0){throw 'Daily runtime check failed; no batch was collected'}
if($RuntimeCheck){$runtimeResult;return}

$current=& $python -B $controller --status | ConvertFrom-Json
if($current.processAlive){$current | ConvertTo-Json -Depth 20;return}
if(-not $Watch){
    $LaunchDirectory=Join-Path $PSScriptRoot ('controller-launches\'+(Get-Date -Format 'yyyyMMddTHHmmss')+'_'+[guid]::NewGuid().ToString('N').Substring(0,6))
    New-Item -ItemType Directory -Path $LaunchDirectory -Force | Out-Null
    $arguments=@('-NoProfile','-File',('"'+$PSCommandPath+'"'),'-Watch','-LaunchDirectory',('"'+$LaunchDirectory+'"'))
    if($Batch){$arguments+=@('-Batch',('"'+$Batch+'"'))}
    $process=Start-Process -FilePath (Get-Process -Id $PID).Path -ArgumentList $arguments -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $LaunchDirectory 'supervisor.stdout.log') `
        -RedirectStandardError (Join-Path $LaunchDirectory 'supervisor.stderr.log')
    @{state='STARTED';supervisorPid=$process.Id;startedAt=$process.StartTime.ToString('o');launchDirectory=$LaunchDirectory;statusCommand="python -B autopilot.py --status"} | ConvertTo-Json
    return
}
if(-not $LaunchDirectory){
    $LaunchDirectory=Join-Path $PSScriptRoot ('controller-launches\'+(Get-Date -Format 'yyyyMMddTHHmmss')+'_'+[guid]::NewGuid().ToString('N').Substring(0,6))
    New-Item -ItemType Directory -Path $LaunchDirectory -Force | Out-Null
}

# The supervisor survives the launching chat turn. It restarts the same active
# batch after a controller crash; it never creates a new collection on retry.
$arguments=@('-B',$controller)
if($Batch){$arguments+=@('--resume',$Batch)}else{$arguments+='--daily'}
$capacityDeadline=[datetime]::UtcNow.AddHours(6)
for($attempt=1;$attempt -le 3;$attempt++){
    $stdout=Join-Path $LaunchDirectory ("controller-$attempt.stdout.log")
    $stderr=Join-Path $LaunchDirectory ("controller-$attempt.stderr.log")
    & $python @arguments 1> $stdout 2> $stderr
    $code=$LASTEXITCODE
    $state=& $python -B $controller --status | ConvertFrom-Json
    @{attempt=$attempt;exitCode=$code;controller=$state;checkedAt=(Get-Date).ToString('o')} |
        ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $LaunchDirectory 'result.json') -Encoding utf8
    if($code -eq 0){exit 0}
    if($state.processAlive){exit 0} # A second launch attaches to the existing owner.
    if($code -eq 75 -and [datetime]::UtcNow -lt $capacityDeadline){
        # Temporary model capacity must not freeze remaining commits as missing
        # tests. Preserve the batch and retry without recollecting or asking a user.
        if($state.batch){$arguments=@('-B',$controller,'--resume',[string]$state.batch)}
        Start-Sleep -Seconds 300
        $attempt--
        continue
    }
    if(-not $Batch -and $state.phase -eq 'FINISHED'){break} # Failed collection must not replay a stale completed batch.
    if($state.batch){$arguments=@('-B',$controller,'--resume',[string]$state.batch)}
    elseif(-not $Batch){break} # A collection failure must not collect twice blindly.
    if($attempt -lt 3){Start-Sleep -Seconds 5}
}
exit 1
