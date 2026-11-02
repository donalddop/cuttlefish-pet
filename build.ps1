# Builds publish\CuttlefishPet (self-contained single file), the two zips and the installer.
# Needs the .NET 8 SDK and Inno Setup 6.
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
$ver = ([xml](Get-Content CuttlefishPet\CuttlefishPet.csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

Remove-Item -Recurse -Force publish\CuttlefishPet, publish\klein -ErrorAction SilentlyContinue
dotnet publish CuttlefishPet -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish\CuttlefishPet
if ($LASTEXITCODE) { throw "publish failed" }
dotnet publish CuttlefishPet -c Release -r win-x64 --self-contained false -o publish\klein
if ($LASTEXITCODE) { throw "publish (klein) failed" }
Copy-Item Installer\LEES-MIJ.txt publish\LEES-MIJ.txt
Copy-Item Installer\LEES-MIJ.txt publish\CuttlefishPet\LEES-MIJ.txt
Copy-Item Installer\LEES-MIJ.txt publish\klein\LEES-MIJ.txt
Compress-Archive -Force publish\CuttlefishPet\* publish\CuttlefishPet-standalone.zip
Compress-Archive -Force publish\klein\* publish\CuttlefishPet-klein.zip

$iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
if (-not $iscc) { $iscc = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" }
& $iscc /Q "/DAppVersion=$ver" Installer\CuttlefishPet.iss
if ($LASTEXITCODE) { throw "installer failed" }
Write-Host "Built version $ver"
Get-ChildItem publish -File
