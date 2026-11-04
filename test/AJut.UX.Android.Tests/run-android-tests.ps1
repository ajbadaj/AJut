# Builds the AJut Android test app, installs it on a device or emulator through adb, runs it, and prints the
# results it logs. Nothing here touches the screen: install, launch and logcat only.
#
#   .\run-android-tests.ps1                      run everything on the only attached device
#   .\run-android-tests.ps1 -Filter ViewBinder   run the tests whose Class.Method name contains "ViewBinder"
#   .\run-android-tests.ps1 -Device emulator-5554 -NoBuild
#
# Exit code is 0 when every test passed, 1 when any failed, and 2 when the run never finished.
param(
    [string]$Filter,
    [string]$Device,
    [switch]$NoBuild,
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'

$kPackage = 'dev.ajut.ux.android.tests'
$kActivity = 'dev.ajut.ux.android.tests.TestRunnerActivity'
$kLogTag = 'AJutTests'

$project = Join-Path $PSScriptRoot 'AJut.UX.Android.Tests.csproj'
$sdkRoot = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { 'C:\Program Files (x86)\Android\android-sdk' }
$adb = Join-Path $sdkRoot 'platform-tools\adb.exe'
if (-not (Test-Path $adb)) {
    Write-Error "adb not found at $adb. Set ANDROID_HOME to the Android SDK."
}

$adbTarget = @()
if ($Device) {
    $adbTarget = @('-s', $Device)
}

# 1. Build and install. The Install target deploys to the device adb would pick, or to -Device.
if (-not $NoBuild) {
    $buildArgs = @('build', $project, '-c', 'Debug', '-t:Install', '-nologo', '-v', 'q')
    if ($Device) {
        $buildArgs += "-p:AdbTarget=-s $Device"
    }

    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build or install failed (exit $LASTEXITCODE)."
    }
}

# 2. Start clean, so the results read back are this run's only.
& $adb @adbTarget shell am force-stop $kPackage
& $adb @adbTarget logcat -c

$startArgs = @('shell', 'am', 'start', '-W', '-n', "$kPackage/$kActivity")
if ($Filter) {
    $startArgs += @('--es', 'filter', $Filter)
}

& $adb @adbTarget @startArgs | Out-Null

# 3. Wait for the runner's DONE line, then print every result line.
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$lines = @()
$done = $null
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 750
    $lines = & $adb @adbTarget logcat -d -v raw -s "${kLogTag}:I"
    $done = $lines | Where-Object { $_ -match '^DONE ' } | Select-Object -Last 1
    if ($done) {
        break
    }
}

$lines | Where-Object { $_ -match '^(PASS|FAIL|SKIP|DONE) ' } | ForEach-Object { Write-Output $_ }

if (-not $done) {
    Write-Output "The test run did not finish within $TimeoutSeconds seconds. Crash output, if any:"
    & $adb @adbTarget logcat -d -v brief -s 'AndroidRuntime:E' 'monodroid:E' | Select-Object -Last 40
    exit 2
}

if ($done -match 'failed=(\d+)' -and [int]$Matches[1] -gt 0) {
    exit 1
}

exit 0
