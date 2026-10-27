$ErrorActionPreference = 'Stop'
$cfg = Get-Content -LiteralPath 'C:\AutoTest\AutoTest\code_indicator\vega-agent\config.json' -Raw | ConvertFrom-Json
$auth = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(':' + $cfg.azure_devops.pat))
foreach ($wi in @(3672,3677)) {
    $x = Invoke-RestMethod -Uri "http://win-devops:8080/DefaultCollection/Vega_SW/_apis/wit/workitems/${wi}?`$expand=relations&api-version=6.0" -Headers @{Authorization="Basic $auth"} -TimeoutSec 25
    $x | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $PSScriptRoot "wi$wi.json") -Encoding utf8
    foreach ($relation in $x.relations | Where-Object rel -eq 'AttachedFile') {
        $fileName = [IO.Path]::GetFileName($relation.attributes.name)
        if ($fileName -ne $relation.attributes.name) { throw 'Unsafe attachment name' }
        Invoke-WebRequest -Uri $relation.url -Headers @{Authorization="Basic $auth"} -OutFile (Join-Path $PSScriptRoot $fileName) -TimeoutSec 30
        Write-Output "Downloaded WI$wi attachment $fileName"
    }
}
