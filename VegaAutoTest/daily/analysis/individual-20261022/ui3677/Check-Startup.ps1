$ErrorActionPreference = 'Stop'
$repo = 'C:\AutoTest\workspaces\individual-20261022-20261023'
$helper = 'C:\AutoTest\AutoTest\VegaAutoTest\daily\Start-ModelValidation.ps1'
$status = @{targetHead='10c33df5e69aa932bea8d843a29d8f6530d1fd73';model='Vega300';profile='HWv1';actualUiWorkflowCompleted=$false;desktopBlocker='Computer Use helper timeouts persisted after kernel reset';startedAt=(Get-Date).ToString('o')}
$session = $null
try {
    $prepare = & $helper -Model Vega300 -Profile HWv1 -RepoRoot $repo -Action Prepare
    $session = [regex]::Match(($prepare -join "`n"),'SessionPath=([^\r\n]+)').Groups[1].Value.Trim()
    if(-not $session){throw 'Prepare did not return SessionPath'}
    $status.sessionPath = $session
    & $helper -Model Vega300 -Profile HWv1 -RepoRoot $repo -Action Start -SessionPath $session
    $sessionData = Get-Content -LiteralPath (Join-Path $session 'session.json') -Raw | ConvertFrom-Json
    $status.process = $sessionData.process
    Start-Sleep -Seconds 20
    $process = Get-Process -Id $sessionData.process.pid -ErrorAction SilentlyContinue
    $status.processStillRunning = $null -ne $process
    if($process){$status.mainWindowTitle=$process.MainWindowTitle;$status.responding=$process.Responding}
    $status.note='Actual Simulator startup check only. Login and required UI postprocess were not completed; process metadata is not UI acceptance.'
} catch {
    $status.error = $_.Exception.Message
} finally {
    if($session){
        try { & $helper -Model Vega300 -Profile HWv1 -RepoRoot $repo -Action Stop -SessionPath $session; $status.stopCompleted=$true } catch { $status.stopError=$_.Exception.Message }
        try { & $helper -Model Vega300 -Profile HWv1 -RepoRoot $repo -Action Restore -SessionPath $session; $status.restoreCompleted=$true } catch { $status.restoreError=$_.Exception.Message }
        $current = Get-Content -LiteralPath (Join-Path $session 'session.json') -Raw | ConvertFrom-Json
        $machineBackup=Join-Path $session 'MachineConfiguration.before.json'
        if((Test-Path -LiteralPath $machineBackup) -and $current.machineConfiguration){
            Copy-Item -LiteralPath $machineBackup -Destination $current.machineConfiguration -Force
            $status.machineOriginalHash=(Get-FileHash -LiteralPath $machineBackup).Hash
            $status.machineRestoredHash=(Get-FileHash -LiteralPath $current.machineConfiguration).Hash
        }
        $bin = Join-Path $repo 'Vega\bin\Debug\net8.0-windows10.0.19041.0\win-x64\Resources'
        $status.restoration = @{
            state=$current.state
            componentExpected=$current.originalHashes.ComponentConfig
            componentActual=(Get-FileHash -LiteralPath (Join-Path $bin 'ComponentConfig.json')).Hash
            hardwareExpected=$current.originalHashes.HardwareProfiles
            hardwareActual=(Get-FileHash -LiteralPath (Join-Path $bin 'HardwareProfiles.json')).Hash
        }
    }
    $status.finishedAt=(Get-Date).ToString('o')
    $status | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'startup-status.json') -Encoding utf8
}
$status | ConvertTo-Json -Depth 10
