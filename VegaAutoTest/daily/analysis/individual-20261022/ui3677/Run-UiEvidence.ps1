$ErrorActionPreference='Stop'
$repo='C:\AutoTest\workspaces\individual-20261022-20261023'
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
