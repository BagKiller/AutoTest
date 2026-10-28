$ErrorActionPreference='Stop'
$source='C:\AutoTest\workspaces\20261027T233318+0800_9ad645_00b3004a58de_20261027T233503_b4adac\Vega\bin\Debug\net8.0-windows10.0.19041.0\win-x64\Resources'
$repo=Join-Path 'C:\AutoTest\workspaces' ('repair-session-fixture-'+[guid]::NewGuid().ToString('N').Substring(0,8))
$resources=Join-Path $repo 'Vega\bin\Debug\net8.0-windows10.0.19041.0\win-x64\Resources'
New-Item -ItemType Directory -Path (Join-Path $resources 'MachineConfiguration') -Force | Out-Null
foreach($file in @('ComponentConfig.json','HardwareProfiles.json','MachineConfiguration_Default.json','MachineConfiguration\General.json')){
    Copy-Item -LiteralPath (Join-Path $source $file) -Destination (Join-Path $resources $file)
}
$script=Join-Path $PSScriptRoot '..\..\Start-ModelValidation.ps1'
$machine=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Vega\ResourcesDaily_Vega100-PIC\MachineConfiguration.json'
$existed=Test-Path -LiteralPath $machine
$originalMachine=if($existed){(Get-FileHash -LiteralPath $machine).Hash}else{$null}
$originalComponent=(Get-FileHash -LiteralPath (Join-Path $resources 'ComponentConfig.json')).Hash
$originalHardware=(Get-FileHash -LiteralPath (Join-Path $resources 'HardwareProfiles.json')).Hash
$session=$null
try {
    $output=& $script -Action Prepare -Model Vega100 -Profile PIC -RepoRoot $repo
    $line=$output | Where-Object {$_ -match 'SessionPath='} | Select-Object -First 1
    if(-not $line){throw 'No prepared session path'}
    $session=($line -split 'SessionPath=',2)[1]
    & $script -Action Restore -Model Vega100 -Profile PIC -RepoRoot $repo -SessionPath $session | Out-Null
    if((Get-FileHash -LiteralPath (Join-Path $resources 'ComponentConfig.json')).Hash -ne $originalComponent){throw 'Component restore mismatch'}
    if((Get-FileHash -LiteralPath (Join-Path $resources 'HardwareProfiles.json')).Hash -ne $originalHardware){throw 'Hardware restore mismatch'}
    if($existed){if((Get-FileHash -LiteralPath $machine).Hash -ne $originalMachine){throw 'User machine settings restore mismatch'}}
    elseif(Test-Path -LiteralPath $machine){throw 'New machine settings were not removed'}
    @{passed=$true;session=$session;componentSha256=$originalComponent;hardwareSha256=$originalHardware;machineExisted=$existed;machineSha256=$originalMachine} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'session-restore-result.json')
    'PASS: ComponentConfig, HardwareProfiles and user MachineConfiguration restored byte-for-byte.'
} finally {
    if($session){
        $state=Get-Content -LiteralPath (Join-Path $session 'session.json') -Raw | ConvertFrom-Json
        if($state.state -ne 'RESTORED'){& $script -Action Restore -Model Vega100 -Profile PIC -RepoRoot $repo -SessionPath $session | Out-Null}
    }
}
