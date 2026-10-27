param([string]$CaseId)
$ErrorActionPreference = 'Stop'
$repo = 'C:\AutoTest\workspaces\individual-20261022-20261023'
$env:VEGA_SW_ROOT = $repo
$env:VEGA_INDIVIDUAL_EVIDENCE = $PSScriptRoot
$env:DOTNET_ROOT = 'C:\AutoTest\runtime\dotnet'
$env:PATH = $env:DOTNET_ROOT + ';' + $env:PATH
$env:NUGET_PACKAGES = 'C:\Users\Administrator\.nuget\packages'
$env:RestoreConfigFile = 'C:\AutoTest\setup\NuGet.Config'
$env:GIT_CONFIG_COUNT = '1'
$env:GIT_CONFIG_KEY_0 = 'safe.directory'
$env:GIT_CONFIG_VALUE_0 = $repo.Replace('\','/')
$settings = Get-Content -LiteralPath 'C:\AutoTest\AutoTest\code_indicator\vega-agent\config.json' -Raw | ConvertFrom-Json
$env:NuGetPackageSourceCredentials_Galaxy = 'Username=vega;Password=' + $settings.azure_devops.pat + ';ValidAuthenticationTypes=Basic'
$env:NuGetPackageSourceCredentials_EFEM = $env:NuGetPackageSourceCredentials_Galaxy
try {
    & "$env:DOTNET_ROOT\dotnet.exe" 'C:\AutoTest\AutoTest\VegaAutoTest\runner\Vega.AutoTest.Runner\bin\Debug\net8.0-windows\Vega.AutoTest.Runner.dll' verify --case $CaseId --layers logic --repo-root $repo --runs-dir (Join-Path $PSScriptRoot $CaseId)
    exit $LASTEXITCODE
} finally {
    Remove-Item Env:NuGetPackageSourceCredentials_Galaxy,Env:NuGetPackageSourceCredentials_EFEM -ErrorAction SilentlyContinue
}
