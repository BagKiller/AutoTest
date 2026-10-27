$ErrorActionPreference = 'Stop'
$repo = 'C:\AutoTest\workspaces\individual-20261022-20261023'
$evidence = $PSScriptRoot
$env:VEGA_SW_ROOT = $repo
$env:DOTNET_ROOT = 'C:\AutoTest\runtime\dotnet10'
$env:PATH = $env:DOTNET_ROOT + ';' + $env:PATH
$env:NUGET_PACKAGES = 'C:\Users\Administrator\.nuget\packages'
$settings = Get-Content -LiteralPath 'C:\AutoTest\AutoTest\code_indicator\vega-agent\config.json' -Raw | ConvertFrom-Json
$env:NuGetPackageSourceCredentials_Galaxy = 'Username=vega;Password=' + $settings.azure_devops.pat + ';ValidAuthenticationTypes=Basic'
$env:NuGetPackageSourceCredentials_EFEM = $env:NuGetPackageSourceCredentials_Galaxy
$common = @('-p:RestorePackagesWithLockFile=false', "-p:NuGetLockFilePath=$evidence\unused.lock.json", '-p:NuGetAudit=false')
try {
    foreach ($name in @('Vega.BL.WaferAlignment','Vega.BL.WaferRun')) {
        $project = Join-Path $repo "$name\$name.csproj"
        & "$env:DOTNET_ROOT\dotnet.exe" restore $project --configfile C:\AutoTest\setup\NuGet.Config --disable-parallel @common *> (Join-Path $evidence "$name.restore.log")
        if ($LASTEXITCODE -ne 0) { throw "Restore failed: $name" }
        & "$env:DOTNET_ROOT\dotnet.exe" build $project --no-restore @common -v:minimal *> (Join-Path $evidence "$name.build.log")
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $name" }
        Write-Output "BUILT $name"
    }
} finally {
    Remove-Item Env:NuGetPackageSourceCredentials_Galaxy,Env:NuGetPackageSourceCredentials_EFEM -ErrorAction SilentlyContinue
}
