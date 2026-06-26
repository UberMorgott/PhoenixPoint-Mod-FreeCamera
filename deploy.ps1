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
# Ship the localization assets (in-game options labels/descriptions read from this CSV).
$assets = "E:\DEV\PhoenixPoint\FreeCamera\Assets"
if (Test-Path $assets) { Copy-Item $assets $dest -Recurse -Force }
Write-Host "Deployed FreeCamera to $dest"

# Ensure the 2nd-instance (co-op test) junction exists so this mod loads in both instances.
$inst2Mods = "D:\PP-Instance2\Mods"
$inst2Link = Join-Path $inst2Mods "FreeCamera"
if ((Test-Path $inst2Mods) -and -not (Test-Path $inst2Link)) {
    New-Item -ItemType Junction -Path $inst2Link -Target $dest | Out-Null
    Write-Host "Linked FreeCamera into 2nd instance: $inst2Link -> $dest"
}
