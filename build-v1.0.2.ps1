$env:DOTNET_ROOT = "E:\dev\dotnet"
$dotnet = "E:\dev\dotnet\dotnet.exe"
$iscc = "E:\dev\inno-setup\tools\ISCC.exe"
$issFile = "E:\DEVS\Purges\PurgeWin\installer\PurgeSetup.iss"
$version = "1.0.2"
$distDir = "E:\DEVS\Purges\PurgeWin\dist"

if (-not (Test-Path $distDir)) { New-Item -ItemType Directory -Path $distDir -Force | Out-Null }

$buildArch = {
    param($arch, $platform, $targetDir, $outDir)
    Write-Host "=== Building $arch ($platform) ==="
    if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force -ErrorAction SilentlyContinue }
    
    & $dotnet publish "E:\DEVS\Purges\PurgeWin\PurgeWin.csproj" -c Release -r win-$arch --self-contained true -p:Platform=$platform -o $outDir
    
    # Ensure PRI and XBF are co-located in the publish directory
    if (Test-Path "$targetDir\Purge.pri") {
        Copy-Item "$targetDir\Purge.pri" "$outDir\Purge.pri" -Force
        Write-Host "  -> Copied Purge.pri to publish"
    }
    if (Test-Path "$targetDir\resources.pri") {
        Copy-Item "$targetDir\resources.pri" "$outDir\resources.pri" -Force
        Write-Host "  -> Copied resources.pri to publish"
    }
    if (Test-Path "$targetDir\App.xbf") {
        Copy-Item "$targetDir\App.xbf" "$outDir\App.xbf" -Force
        Write-Host "  -> Copied App.xbf to publish"
    }
    
    # Compile installer
    Write-Host "  -> Compiling Inno Setup for $arch..."
    & $iscc "/DMyAppVersion=$version" "/DMyArch=$arch" "/DSourceDir=$outDir" $issFile
    
    # Create portable ZIP
    Write-Host "  -> Creating portable ZIP for $arch..."
    $zipPath = "$distDir\Purge-$version-win-$arch.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path "$outDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
    
    # Create .nupkg alias for squirrel-compatible manifest
    Copy-Item $zipPath "$distDir\Purge-$version-win-$arch-full.nupkg" -Force
}

# 1. win-x64
& $buildArch "x64" "x64" "E:\DEVS\Purges\PurgeWin\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64" "E:\DEVS\Purges\PurgeWin\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\publish"

# 2. win-arm64
& $buildArch "arm64" "ARM64" "E:\DEVS\Purges\PurgeWin\bin\ARM64\Release\net10.0-windows10.0.19041.0\win-arm64" "E:\DEVS\Purges\PurgeWin\bin\ARM64\Release\net10.0-windows10.0.19041.0\win-arm64\publish"

# 3. win-x86
& $buildArch "x86" "x86" "E:\DEVS\Purges\PurgeWin\bin\x86\Release\net10.0-windows10.0.19041.0\win-x86" "E:\DEVS\Purges\PurgeWin\bin\x86\Release\net10.0-windows10.0.19041.0\win-x86\publish"

Write-Host "=== 4. Updating releases JSON manifests ==="
@("x64", "arm64", "x86") | ForEach-Object {
    $arch = $_
    $manifest = @"
{
  "currentVersion": "$version",
  "releases": [
    {
      "version": "$version",
      "package": "Purge-$version-win-$arch-full.nupkg",
      "installer": "Purge-$version-win-$arch-Setup.exe",
      "zip": "Purge-$version-win-$arch.zip",
      "arch": "$arch",
      "published": "$(Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ')"
    }
  ]
}
"@
    $manifest | Out-File -FilePath "$distDir\releases.win-$arch.json" -Encoding utf8
}

Write-Host "=== 5. Computing SHA256 checksums ==="
$binFiles = Get-ChildItem "$distDir\Purge-$version-*" | Where-Object { $_.Extension -in ".exe", ".zip" }
$hashes = foreach ($f in $binFiles) {
    $h = (Get-FileHash -Algorithm SHA256 $f.FullName).Hash.ToLower()
    "$h  $($f.Name)"
}
$hashes | Out-File -FilePath "$distDir\SHA256SUMS.txt" -Encoding utf8

Write-Host "=== BUILD AND PACKAGING COMPLETED SUCCESSFULLY ==="
