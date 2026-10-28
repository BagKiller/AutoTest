param([string]$RepoRoot = 'C:\AutoTest\workspaces\Vega_SW', [string]$EvidenceRoot)
$ErrorActionPreference='Stop'
$env:DOTNET_ROOT='C:\AutoTest\runtime\dotnet10'
$env:PATH=$env:DOTNET_ROOT+';'+$env:PATH
$env:NUGET_PACKAGES='C:\Users\Administrator\.nuget\packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$runRoot=if($EvidenceRoot){$EvidenceRoot}else{Join-Path $PSScriptRoot 'build'}
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
if(Test-Path (Join-Path $runRoot 'build-provenance.json')){throw 'Evidence directory already contains provenance; use a new attempt directory'}
$targets=Join-Path $runRoot 'Simulator.Build.targets'
$escapedRoot=[System.Security.SecurityElement]::Escape($RepoRoot)
Set-Content -LiteralPath $targets -Value ('<Project><Import Project="'+$escapedRoot+'\Directory.Build.targets" /><ItemGroup><ProjectReference Remove="..\Vega.ThirdParty.NiDQACard\Vega.ThirdParty.NiDQACard.vcxproj" /></ItemGroup></Project>')
$common=@("-p:DirectoryBuildTargetsPath=$targets", "-p:SolutionDir=$RepoRoot\", '-p:RestorePackagesWithLockFile=false', "-p:NuGetLockFilePath=$runRoot\unused.lock.json")
$project=Join-Path $RepoRoot 'Vega\Vega.csproj'
$settings=Get-Content 'C:\AutoTest\AutoTest\code_indicator\vega-agent\config.json' -Raw | ConvertFrom-Json
$env:NuGetPackageSourceCredentials_Galaxy='Username=vega;Password='+$settings.azure_devops.pat+';ValidAuthenticationTypes=Basic'
$env:NuGetPackageSourceCredentials_EFEM=$env:NuGetPackageSourceCredentials_Galaxy
try {
    & "$env:DOTNET_ROOT\dotnet.exe" restore $project --configfile C:\AutoTest\setup\NuGet.Config --disable-parallel @common *> (Join-Path $runRoot 'restore.log')
    if($LASTEXITCODE -ne 0){throw 'Product restore failed; see daily/build/restore.log'}
    & "$env:DOTNET_ROOT\dotnet.exe" restore (Join-Path $RepoRoot 'Vega.PD.XilinxDataCollector\Vega.PD.XilinxDataCollector.csproj') --runtime linux-x64 --configfile C:\AutoTest\setup\NuGet.Config @common *> (Join-Path $runRoot 'restore-xilinx.log')
    if($LASTEXITCODE -ne 0){throw 'Xilinx restore failed; see daily/build/restore-xilinx.log'}
} finally {
    Remove-Item Env:NuGetPackageSourceCredentials_Galaxy,Env:NuGetPackageSourceCredentials_EFEM -ErrorAction SilentlyContinue
}
& "$env:DOTNET_ROOT\dotnet.exe" build $project --no-restore @common -v:minimal *> (Join-Path $runRoot 'build.log')
if($LASTEXITCODE -ne 0){throw 'Product build failed; see daily/build/build.log'}
# CommitProbe references the separate editor binary. A fresh worktree has no
# editor output; building Vega alone otherwise leaves every probe uncompilable.
$batchProject=Join-Path $RepoRoot 'Vega.UI.RecipeBatchEditor\Vega.UI.RecipeBatchEditor.csproj'
$env:NuGetPackageSourceCredentials_Galaxy='Username=vega;Password='+$settings.azure_devops.pat+';ValidAuthenticationTypes=Basic'
$env:NuGetPackageSourceCredentials_EFEM=$env:NuGetPackageSourceCredentials_Galaxy
try {
    & "$env:DOTNET_ROOT\dotnet.exe" restore $batchProject --configfile C:\AutoTest\setup\NuGet.Config --disable-parallel @common *> (Join-Path $runRoot 'restore-batch-editor.log')
    if($LASTEXITCODE -ne 0){throw 'Batch editor restore failed; see restore-batch-editor.log'}
} finally {
    Remove-Item Env:NuGetPackageSourceCredentials_Galaxy,Env:NuGetPackageSourceCredentials_EFEM -ErrorAction SilentlyContinue
}
& "$env:DOTNET_ROOT\dotnet.exe" build $batchProject --no-restore --no-dependencies @common -v:minimal *> (Join-Path $runRoot 'build-batch-editor.log')
if($LASTEXITCODE -ne 0){throw 'Batch editor build failed; see build-batch-editor.log'}
$batchDll=Join-Path (Split-Path $project) 'bin\Debug\net8.0-windows10.0.19041.0\win-x64\RecipeBatchEditor\RecipeBatchEditor.dll'
if(-not (Test-Path -LiteralPath $batchDll)){throw 'Batch editor output missing after build'}
# Hardware Profile changes also require the actual standalone editor UI.
# Build it from the same detached SHA rather than an installed/previous copy.
$componentProject=Join-Path $RepoRoot 'ComponentConfigEditor\ComponentConfigEditor.csproj'
$componentDll=$null
if(Test-Path -LiteralPath $componentProject){
    & "$env:DOTNET_ROOT\dotnet.exe" restore $componentProject --configfile C:\AutoTest\setup\NuGet.Config @common *> (Join-Path $runRoot 'restore-component-editor.log')
    if($LASTEXITCODE -ne 0){throw 'Component editor restore failed; see restore-component-editor.log'}
    & "$env:DOTNET_ROOT\dotnet.exe" build $componentProject --no-restore @common -v:minimal *> (Join-Path $runRoot 'build-component-editor.log')
    if($LASTEXITCODE -ne 0){throw 'Component editor build failed; see build-component-editor.log'}
    $componentDll=Join-Path $RepoRoot 'Vega\bin\Debug\net8.0-windows10.0.19041.0\win-x64\ComponentConfigEditor\ComponentConfigEditor.dll'
    if(-not(Test-Path -LiteralPath $componentDll)){throw 'Component editor output missing after build'}
}
$head= & git -c "safe.directory=$RepoRoot" -C $RepoRoot rev-parse HEAD
$dll=Join-Path $RepoRoot 'Vega\bin\Debug\net8.0-windows10.0.19041.0\win-x64\Vega.dll'
@{commit=$head;builtAt=(Get-Date).ToString('o');repo=$RepoRoot;vegaDllSha256=(Get-FileHash -LiteralPath $dll).Hash;batchEditorDllSha256=(Get-FileHash -LiteralPath $batchDll).Hash;componentEditorDllSha256=$(if($componentDll){(Get-FileHash -LiteralPath $componentDll).Hash}else{$null});hardwareNativeNiProjectExcluded=$true;lockedRestore=$false} | ConvertTo-Json | Set-Content (Join-Path $runRoot 'build-provenance.json')
Get-Content (Join-Path $runRoot 'build.log') -Tail 5
