[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('Vega100','Vega300','Vega500')][string]$Model='Vega500',
    [ValidateSet('Plan','Prepare','Authenticator','Start','Stop','Restore')][string]$Action='Plan',
    [ValidateSet('PIC','Coherent','HWv1','HWv2','Default')][string]$Profile='Default',
    [string]$RepoRoot='C:\AutoTest\workspaces\Vega_SW',
    [string]$SessionPath,
    [string]$AuthenticatorPath='C:\AutoTest\runtime\AuthenticatorApp\Authenticator.exe'
)
$ErrorActionPreference='Stop'
if($Model -eq 'Vega100'){
    if($Profile -eq 'Default'){$Profile='PIC'}
    if($Profile -notin @('PIC','Coherent')){throw 'Vega100 requires PIC or Coherent'}
    $profileName="$Model-$Profile"
}elseif($Model -eq 'Vega300'){
    if($Profile -eq 'Default'){$Profile='HWv1'}
    if($Profile -notin @('HWv1','HWv2')){throw 'Vega300 requires HWv1 or HWv2'}
    $profileName="$Model-$Profile"
}else{
    if($Profile -ne 'Default'){throw 'Vega500 uses Default profile'}
    $profileName='Vega500'
}
$bin=Join-Path $RepoRoot 'Vega\bin\Debug\net8.0-windows10.0.19041.0\win-x64'
$componentPath=Join-Path $bin 'Resources\ComponentConfig.json'
$hardwarePath=Join-Path $bin 'Resources\HardwareProfiles.json'
$features=switch($Model){'Vega100' {"EFEM,OuterToInner,Laser$Profile"};'Vega300' {'EFEM,LPM,EdgeChuck'};'Vega500' {'EFEM,LPM,DIC,EdgeChuck'}}
$plan=@{model=$Model;profile=$profileName;rootCondition="$Model,Simulator,GemSim,Gen2";features=$features;repo=$RepoRoot;bin=$bin;userDataFolder="ResourcesDaily_$profileName";authentication='Use official Authenticator with matching model, Simulator enabled, Gem disabled, Gen2 and this userDataFolder. Automation explicitly authorized by the user in this task.'}
if($Action -eq 'Plan'){$plan | ConvertTo-Json;return}

function Assert-Stopped([switch]$ForStart) {
    foreach($process in (Get-Process -Name Vega -ErrorAction SilentlyContinue)){
        if($ForStart -or -not $process.Path -or $process.Path -eq (Join-Path $bin 'Vega.exe')){throw 'A conflicting Vega instance is running. Finish its work and close it before switching this runtime configuration or starting another instance.'}
    }
}
function Save-Json($Path,$Value){$Value | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $Path -Encoding utf8}
function Read-Session {
    if(-not $SessionPath){throw '-SessionPath is required for Start/Restore'}
    $sessionRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'sessions'))+[IO.Path]::DirectorySeparatorChar
    $resolved=[IO.Path]::GetFullPath($SessionPath)
    if(-not $resolved.StartsWith($sessionRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Session must be under daily/sessions'}
    $state=Get-Content -LiteralPath (Join-Path $resolved 'session.json') -Raw | ConvertFrom-Json -AsHashtable
    if($state.bin -ne $bin){throw 'Session belongs to a different binary directory; pass its original -RepoRoot'}
    return $state
}
if($Action -eq 'Prepare'){
    Assert-Stopped
    $component=Get-Content -LiteralPath $componentPath -Raw | ConvertFrom-Json -AsHashtable
    $hardware=Get-Content -LiteralPath $hardwarePath -Raw | ConvertFrom-Json -AsHashtable
    $tool=@($component.ToolConfigCollection.ToolConfigs | Where-Object ToolType -eq $Model)
    if($tool.Count -ne 1){throw 'Selected model is absent or ambiguous in ComponentConfig'}
    if(-not $hardware.Profiles.ContainsKey($profileName)){throw 'Selected hardware profile is absent'}
    if(-not $PSCmdlet.ShouldProcess($bin,"Prepare $profileName Simulator/GemSim/Gen2 and back up runtime configuration")){return}
    $session=Join-Path (Join-Path $PSScriptRoot 'sessions') ((Get-Date -Format 'yyyyMMddTHHmmss')+'_'+$profileName+'_'+[guid]::NewGuid().ToString('N').Substring(0,6))
    New-Item -ItemType Directory -Path $session -Force | Out-Null
    $plan.originalHashes=@{}
    foreach($entry in @{ComponentConfig=$componentPath;HardwareProfiles=$hardwarePath}.GetEnumerator()){
        Copy-Item -LiteralPath $entry.Value -Destination (Join-Path $session ($entry.Key+'.original.json'))
        $plan.originalHashes[$entry.Key]=(Get-FileHash -LiteralPath $entry.Value).Hash
    }
    $component.ToolConfigCollection.SelectedToolType=$Model
    $tool[0].RootCondition=$plan.rootCondition
    $tool[0].Features=$features
    $tool[0].UserDataFolder=$plan.userDataFolder
    $hardware.SelectedProfiles[$Model]=$profileName
    try {
        Save-Json $componentPath $component
        Save-Json $hardwarePath $hardware
        # Seed only this isolated profile's machine settings. Installation defaults
        # use D:\Vega\Data, which is unavailable on this validation VM.
        $userFolder=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('Vega\'+$plan.userDataFolder)
        New-Item -ItemType Directory -Path $userFolder -Force | Out-Null
        $machine=Join-Path $userFolder 'MachineConfiguration.json'
        if(Test-Path -LiteralPath $machine){Copy-Item -LiteralPath $machine -Destination (Join-Path $session 'MachineConfiguration.before.json')}
        else{Copy-Item -LiteralPath (Join-Path $bin 'Resources\MachineConfiguration_Default.json') -Destination $machine}
        $dataRoot=Join-Path 'C:\AutoTest\data\validation' $profileName
        New-Item -ItemType Directory -Path $dataRoot -Force | Out-Null
        $machineObject=Get-Content -LiteralPath $machine -Raw | ConvertFrom-Json -AsHashtable
        $general=@($machineObject.Sections | Where-Object Name -eq 'General')
        if($general.Count -ne 1){throw 'Machine settings have no unique General section'}
        # MachineConfiguration_Default only contains section references. Materialize
        # General before first boot, otherwise the loader reintroduces D: defaults.
        if(-not $general[0].Configurations){
            $generalSource=Get-Content -LiteralPath (Join-Path $bin 'Resources\MachineConfiguration\General.json') -Raw | ConvertFrom-Json -AsHashtable
            $general[0].Configurations=$generalSource.Configurations
        }
        foreach($setting in $general[0].Configurations){
            if($setting.Value -is [string]){
                $normalized=$setting.Value -replace '\\{2,}','\'
                if($normalized -match '^D:\\Vega\\Data(?=\\|$)'){$setting.Value=$dataRoot+$normalized.Substring(12)}
            }
        }
        Save-Json $machine $machineObject
        $plan.machineConfiguration=$machine
        $plan.dataRoot=$dataRoot
        $plan.preparedHashes=@{ComponentConfig=(Get-FileHash $componentPath).Hash;HardwareProfiles=(Get-FileHash $hardwarePath).Hash}
        $plan.createdAt=(Get-Date).ToString('o')
        $plan.state='PREPARED'
        Save-Json (Join-Path $session 'session.json') $plan
    }catch{
        Copy-Item -LiteralPath (Join-Path $session 'ComponentConfig.original.json') -Destination $componentPath -Force
        Copy-Item -LiteralPath (Join-Path $session 'HardwareProfiles.original.json') -Destination $hardwarePath -Force
        throw
    }
    Write-Output "Prepared $profileName. SessionPath=$session"
    Write-Output "Use official Authenticator for $profileName / Simulator / GemSim / Gen2 / $($plan.userDataFolder), then -Action Start -SessionPath <path>. Initial calibration data may still be required."
    return
}
if($Action -eq 'Authenticator'){
    if(-not (Test-Path -LiteralPath $AuthenticatorPath)){throw 'Authenticator executable is missing'}
    if($PSCmdlet.ShouldProcess($AuthenticatorPath,"Open interactive authenticator for $profileName / Gen2")){
        $env:DOTNET_ROOT='C:\AutoTest\runtime\dotnet'
        if(-not (Get-Process -Name Authenticator -ErrorAction SilentlyContinue)){Start-Process -FilePath $AuthenticatorPath -WorkingDirectory (Split-Path $AuthenticatorPath) -WindowStyle Normal}
        Write-Output "Authenticator available. Configure $profileName, Simulator enabled, Gem disabled, Gen2; startup resource folder=$($plan.userDataFolder). Use the official generation/load/save controls."
    }
    return
}
$state=Read-Session
if($Action -eq 'Stop'){
    $current=Get-Content -LiteralPath $componentPath -Raw | ConvertFrom-Json -AsHashtable
    $selected=@($current.ToolConfigCollection.ToolConfigs | Where-Object ToolType -eq $current.ToolConfigCollection.SelectedToolType)
    if($selected.Count -ne 1 -or $selected[0].RootCondition -notmatch '(^|,)Simulator(,|$)'){throw 'Stop helper is restricted to this prepared Simulator instance'}
    foreach($process in (Get-Process -Name Vega -ErrorAction SilentlyContinue)){
        if($process.Path -eq (Join-Path $bin 'Vega.exe') -and $PSCmdlet.ShouldProcess($process.Id,'Stop this isolated validation Simulator process')){
            Stop-Process -InputObject $process -Force
            if(!$process.WaitForExit(10000)){throw 'Simulator did not exit within 10 seconds'}
        }
    }
    return
}
Assert-Stopped -ForStart:($Action -eq 'Start')
if($Action -eq 'Start'){
    if($state.state -ne 'PREPARED'){throw 'Session is not prepared'}
    foreach($entry in @{ComponentConfig=$componentPath;HardwareProfiles=$hardwarePath}.GetEnumerator()){
        if((Get-FileHash -LiteralPath $entry.Value).Hash -ne $state.preparedHashes[$entry.Key]){throw 'Runtime configuration changed after preparation; do not start a mismatched model'}
    }
    if(-not (Test-NetConnection -ComputerName 127.0.0.1 -Port 27017 -InformationLevel Quiet -WarningAction SilentlyContinue)){throw 'MongoDB 127.0.0.1:27017 is not ready; start the existing local database first'}
    if($PSCmdlet.ShouldProcess((Join-Path $bin 'Vega.exe'),"Start prepared $($state.profile) Simulator; official authentication automation authorized")){
        $env:DOTNET_ROOT='C:\AutoTest\runtime\dotnet'
        $env:VEGA_SW_ROOT=$state.repo
        $started=Start-Process -FilePath (Join-Path $bin 'Vega.exe') -WorkingDirectory $bin -WindowStyle Normal -PassThru
        $state.process=@{pid=$started.Id;startedAt=$started.StartTime.ToString('o');image=(Join-Path $bin 'Vega.exe')}
        Save-Json (Join-Path $SessionPath 'session.json') $state
    }
}elseif($Action -eq 'Restore'){
    if($PSCmdlet.ShouldProcess($bin,'Restore original configuration and verify original SHA256')){
        foreach($entry in @{ComponentConfig=$componentPath;HardwareProfiles=$hardwarePath}.GetEnumerator()){
            $backup=Join-Path $SessionPath ($entry.Key+'.original.json')
            if((Get-FileHash -LiteralPath $backup).Hash -ne $state.originalHashes[$entry.Key]){throw 'Backup fingerprint mismatch; restore aborted'}
        }
        foreach($entry in @{ComponentConfig=$componentPath;HardwareProfiles=$hardwarePath}.GetEnumerator()){
            Copy-Item -LiteralPath (Join-Path $SessionPath ($entry.Key+'.original.json')) -Destination $entry.Value -Force
            if((Get-FileHash -LiteralPath $entry.Value).Hash -ne $state.originalHashes[$entry.Key]){throw 'Restored configuration fingerprint mismatch'}
        }
        $state.state='RESTORED';Save-Json (Join-Path $SessionPath 'session.json') $state
        Write-Output 'Original configuration restored and SHA256 verified.'
    }
}
