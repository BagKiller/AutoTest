$ErrorActionPreference='Stop'
$repo='C:\AutoTest\workspaces\individual-20261022-20261023'
$helper='C:\AutoTest\AutoTest\VegaAutoTest\daily\Start-ModelValidation.ps1'
$session='C:\AutoTest\AutoTest\VegaAutoTest\daily\sessions\20261023T175709_Vega300-HWv1_4fa055'
$audit=@{session=$session;finishedAt=(Get-Date).ToString('o')}
try {
    $audit.processesBefore=@(Get-CimInstance Win32_Process | Where-Object {$_.ExecutablePath -and $_.ExecutablePath.StartsWith($repo+'\',[StringComparison]::OrdinalIgnoreCase)} | Select-Object ProcessId,ExecutablePath,CreationDate)
    & $helper -Model Vega300 -Profile HWv1 -RepoRoot $repo -Action Stop -SessionPath $session
    $audit.stopCompleted=$true
} catch {$audit.stopError=$_.Exception.Message}
finally {
    try {
        & $helper -Model Vega300 -Profile HWv1 -RepoRoot $repo -Action Restore -SessionPath $session
        $s=Get-Content -LiteralPath (Join-Path $session 'session.json') -Raw | ConvertFrom-Json
        $backup=Join-Path $session 'MachineConfiguration.before.json'
        if(Test-Path -LiteralPath $backup){
            Copy-Item -LiteralPath $backup -Destination $s.machineConfiguration -Force
            $audit.machineOriginalHash=(Get-FileHash -LiteralPath $backup).Hash
            $audit.machineRestoredHash=(Get-FileHash -LiteralPath $s.machineConfiguration).Hash
        }
        $audit.restoration=@{state=$s.state;componentExpected=$s.originalHashes.ComponentConfig;componentActual=(Get-FileHash -LiteralPath (Join-Path $s.bin 'Resources\ComponentConfig.json')).Hash;hardwareExpected=$s.originalHashes.HardwareProfiles;hardwareActual=(Get-FileHash -LiteralPath (Join-Path $s.bin 'Resources\HardwareProfiles.json')).Hash}
        $audit.remainingProcesses=@(Get-CimInstance Win32_Process | Where-Object {$_.ExecutablePath -and $_.ExecutablePath.StartsWith($repo+'\',[StringComparison]::OrdinalIgnoreCase)} | Select-Object ProcessId,ExecutablePath,CreationDate)
    } catch {$audit.restoreError=$_.Exception.Message}
    $audit | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'restoration.json') -Encoding utf8
}
$audit | ConvertTo-Json -Depth 10
$env:VEGA_SW_ROOT=$repo
$env:VEGA_WI3677_UI_EVIDENCE=$PSScriptRoot
$env:DOTNET_ROOT='C:\AutoTest\runtime\dotnet'
$env:PATH=$env:DOTNET_ROOT+';'+$env:PATH
$env:NUGET_PACKAGES='C:\Users\Administrator\.nuget\packages'
$env:GIT_CONFIG_COUNT='1'
$env:GIT_CONFIG_KEY_0='safe.directory'
$env:GIT_CONFIG_VALUE_0=$repo.Replace('\','/')
& "$env:DOTNET_ROOT\dotnet.exe" 'C:\AutoTest\AutoTest\VegaAutoTest\runner\Vega.AutoTest.Runner\bin\Debug\net8.0-windows\Vega.AutoTest.Runner.dll' verify --case wi3677-ui-evidence --layers uia --repo-root $repo --runs-dir (Join-Path $PSScriptRoot 'runner')
exit $LASTEXITCODE
