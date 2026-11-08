param(
    [Parameter(Mandatory)][string]$Executable,
    [ValidateRange(5, 120)][int]$TimeoutSeconds = 60,
    [switch]$ExpectStartupFailure
)

$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path -LiteralPath $Executable).Path
# Launch the exact artifact, rather than an installed shortcut with the same name.
$process = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -PassThru
try {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) {
            throw "Purge exited before showing its window (code $($process.ExitCode))."
        }
        if ($process.MainWindowTitle -eq 'Purge — Erreur / Error') {
            if (!$ExpectStartupFailure) { throw 'Purge reported a startup error.' }
            if (!$process.CloseMainWindow() -or !$process.WaitForExit(5000)) {
                throw 'Purge did not exit after its error dialog was closed.'
            }
            if ($process.ExitCode -ne 1) { throw "Expected exit code 1, got $($process.ExitCode)." }
            Write-Output 'PASS: Startup failure displayed an error and terminated with exit code 1.'
            return
        }
        if ($process.MainWindowHandle -ne 0 -and $process.MainWindowTitle -eq 'Purge') {
            if ($ExpectStartupFailure) { throw 'Purge unexpectedly started normally.' }
            Start-Sleep -Seconds 2
            $process.Refresh()
            if (!$process.HasExited -and $process.MainWindowHandle -ne 0 -and $process.Responding) {
                Write-Output "PASS: Purge opened a responsive window ($exe)."
                return
            }
        }
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Purge is running without a main window after $TimeoutSeconds seconds."
}
finally {
    # Only the process created by this test is closed. No cleaning action is invoked.
    if (!$process.HasExited) {
        if (!$process.CloseMainWindow() -or !$process.WaitForExit(5000)) {
            $process.Kill()
            $process.WaitForExit()
        }
    }
    $process.Dispose()
}
