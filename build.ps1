$msbuild = Get-Command msbuild -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
if (-not $msbuild) {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
    }
}
if (-not $msbuild) {
    Write-Error "MSBuild not found. Please install Visual Studio or Build Tools."
    exit 1
}

# Compile Release binaries
& $msbuild source/LenovoController.sln /p:Configuration=Release /m /verbosity:normal /p:WarningLevel=0

# Create portable Zip archive
if (Test-Path "LenovoController.zip") { Remove-Item "LenovoController.zip" -Force }
if (Test-Path "source/bin/LenovoController") { Remove-Item "source/bin/LenovoController" -Recurse -Force }
Copy-Item -Path "source/bin/Release" -Destination "source/bin/LenovoController" -Recurse
Compress-Archive -Path "source/bin/LenovoController" -DestinationPath LenovoController.zip -Force

# Build MSI installer using WiX v5
$wix = Get-Command wix -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
if ($wix) {
    Write-Host "Building MSI installer..." -ForegroundColor Cyan
    Push-Location installer
    & $wix build -ext WixToolset.UI.wixext Package.wxs -out ..\LenovoController-1.1.0.msi
    Pop-Location
    if (Test-Path "LenovoController-1.1.0.msi") {
        Write-Host "MSI created successfully: LenovoController-1.1.0.msi" -ForegroundColor Green
    }
} else {
    Write-Warning "WiX CLI not found. MSI was not generated. Install with: dotnet tool install --global wix --version 5.0.2"
}