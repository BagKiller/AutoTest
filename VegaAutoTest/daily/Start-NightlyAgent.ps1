# Unattended nightly entry. Sends the fixed prompt to the local Cursor CLI.
# The agent performs the check. This script does not run tests or collect commits.
# The scheduled task calls this file with no arguments. Optional parameters are only for a dated drill.
param(
    [string]$PromptPath = 'C:\AutoTest\AutoTest\VegaAutoTest\daily\nightly-prompt.txt',
    [string]$LogRoot = 'C:\AutoTest\AutoTest\VegaAutoTest\daily\nightly-logs'
)
$ErrorActionPreference = 'Stop'
$agent = 'C:\Users\Administrator\AppData\Local\cursor-agent\agent.cmd'
if (-not (Test-Path -LiteralPath $agent)) { throw "Cursor CLI is missing: $agent" }
if (-not (Test-Path -LiteralPath $promptPath)) { throw "Nightly prompt is missing: $promptPath" }
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$log = Join-Path $logRoot ($stamp + '.log')
$gitBin = 'C:\Program Files\Git\bin'
$gitCmd = 'C:\Program Files\Git\cmd'
$env:PATH = @(
    'C:\Users\Administrator\AppData\Local\cursor-agent',
    $gitCmd,
    $gitBin,
    $env:PATH
) -join ';'
$prompt = Get-Content -LiteralPath $promptPath -Raw -Encoding UTF8
Set-Location 'C:\AutoTest'
& $agent -p --force --trust --sandbox disabled --workspace 'C:\AutoTest' --output-format text $prompt *>&1 |
    Tee-Object -FilePath $log
exit $LASTEXITCODE
