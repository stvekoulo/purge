$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot
$testExe = Join-Path $projectRoot 'Tests\bin\Debug\net10.0-windows\UpdateTests.exe'
$installer = Join-Path $projectRoot 'artifacts\handoff-test\HandoffTest.exe'
$target = Join-Path $projectRoot 'artifacts\handoff-test\installed'
$marker = Join-Path $target 'restarted.txt'
if (Test-Path -LiteralPath $marker) { Remove-Item -LiteralPath $marker }
# This fixture has no uninstaller, registry entries, shortcuts or real Purge files.
$parent = Start-Process -FilePath $testExe -ArgumentList '--wait-for-update' -PassThru -WindowStyle Hidden
$timer = [Diagnostics.Stopwatch]::StartNew()
try {
    $setup = Start-Process -FilePath $installer -ArgumentList @('/SP-', '/SILENT', '/NORESTART',
        '/PURGEUPDATE=1', "/PURGEPID=$($parent.Id)") -PassThru -WindowStyle Hidden
    if (!$setup.WaitForExit(45000)) { throw 'Test installer timed out.' }
    if ($setup.ExitCode -ne 0) { throw "Test installer failed with code $($setup.ExitCode)." }
    $parent.Refresh()
    if (!$parent.HasExited -or $timer.Elapsed.TotalSeconds -lt 4) { throw 'Installer did not wait for parent exit.' }
    if (!(Test-Path -LiteralPath $marker)) { throw 'Updated application was not restarted.' }
    Write-Output 'PASS: Inno Setup waited for the parent, installed into the isolated directory and restarted the updated fixture.'
}
finally {
    if (!$parent.HasExited) { $parent.Kill(); $parent.WaitForExit() }
    $parent.Dispose()
}
