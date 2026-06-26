$ErrorActionPreference = 'Stop'
$proj = "E:\DEV\PhoenixPoint\FreeCamera\FreeCamera.csproj"
$out  = "E:\DEV\PhoenixPoint\FreeCamera\bin\Release"
$dest = "D:\Steam\steamapps\common\Phoenix Point\Mods\FreeCamera"
dotnet build $proj -c Release
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)." }
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item "$out\FreeCamera.dll" $dest -Force
if (Test-Path "$out\FreeCamera.pdb") { Copy-Item "$out\FreeCamera.pdb" $dest -Force }
Copy-Item "E:\DEV\PhoenixPoint\FreeCamera\meta.json" $dest -Force
Write-Host "Deployed FreeCamera to $dest"
