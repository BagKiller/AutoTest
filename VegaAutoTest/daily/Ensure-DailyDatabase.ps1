[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$mongod='C:\AutoTest\runtime\mongodb\mongodb-win32-x86_64-windows-8.0.20\bin\mongod.exe'
$data='C:\AutoTest\data\mongodb'
function Test-DatabasePort {
    $client=[Net.Sockets.TcpClient]::new()
    try { $connect=$client.ConnectAsync('127.0.0.1',27017); return ($connect.Wait(1000) -and $client.Connected) }
    catch { return $false } finally { $client.Dispose() }
}
if(Test-DatabasePort){Write-Output 'Existing database port 27017 is available.';return}
if(-not (Test-Path -LiteralPath $mongod)){throw 'Existing MongoDB executable missing'}
if(-not (Test-Path -LiteralPath (Join-Path $data 'WiredTiger'))){throw 'Existing database marker missing; refusing to create an empty database'}
$existing=@(Get-CimInstance Win32_Process -Filter "name='mongod.exe'" | Where-Object {$_.CommandLine -like "*$data*"})
if($existing.Count -eq 0){
    $started=Start-Process -FilePath $mongod -ArgumentList '--dbpath',$data,'--bind_ip','127.0.0.1','--port','27017','--replSet','rs0','--logpath',(Join-Path $data 'mongod.log'),'--logappend' -WindowStyle Hidden -PassThru
    Write-Output "Started existing project database PID $($started.Id)."
}
$deadline=[DateTime]::UtcNow.AddSeconds(45)
do {
    if(Test-DatabasePort){Write-Output 'Database port 27017 ready.';return}
    Start-Sleep -Milliseconds 500
} while([DateTime]::UtcNow -lt $deadline)
throw 'Existing MongoDB failed to become ready within 45 seconds; inspect data/mongodb/mongod.log'
