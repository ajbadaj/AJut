# Builds the Android showroom, installs it on a device or emulator through adb, launches it, and optionally saves a
# screenshot. Nothing here taps or types: it is install, launch and screencap only.
#
#   .\run-showroom.ps1                                        build, install and launch on the home page
#   .\run-showroom.ps1 -Page BrushButton -Screenshot shot.png open a page and capture it
#   .\run-showroom.ps1 -NoBuild -Night yes -Page BrushButton  switch the device to dark first (yes, no or auto)
#   .\run-showroom.ps1 -Theme ajut -Page BrushButton          start on the AJut theme (ajut) or plain defaults (plain)
#   .\run-showroom.ps1 -Theme ajut -Touch off                 the AJut theme's desktop palette instead of its touch one
#   .\run-showroom.ps1 -Theme ajut -Mode dark -Page 'Theme Tester'   force the app dark, whatever the device is set to
param(
    [string]$Page,
    [ValidateSet('ajut', 'plain')]
    [string]$Theme,
    # The theme bar's Dark, Light or System; left out, System, or with -Touch the light or dark the device shows
    [ValidateSet('dark', 'light', 'system')]
    [string]$Mode,
    [ValidateSet('on', 'off')]
    [string]$Touch,
    [string]$Screenshot,
    [ValidateSet('yes', 'no', 'auto')]
    [string]$Night,
    [string]$Device,
    [switch]$NoBuild,
    [int]$SettleMilliseconds = 1500
)

$ErrorActionPreference = 'Stop'

$kPackage = 'dev.ajut.showroom.android'
$kActivity = 'dev.ajut.showroom.android.MainActivity'

$project = Join-Path $PSScriptRoot 'AJutShowRoomAndroid.csproj'
$sdkRoot = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { 'C:\Program Files (x86)\Android\android-sdk' }
$adb = Join-Path $sdkRoot 'platform-tools\adb.exe'
if (-not (Test-Path $adb)) {
    Write-Error "adb not found at $adb. Set ANDROID_HOME to the Android SDK."
}

$adbTarget = @()
if ($Device) {
    $adbTarget = @('-s', $Device)
}

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

if ($Night) {
    & $adb @adbTarget shell cmd uimode night $Night | Out-Null
}

& $adb @adbTarget shell am force-stop $kPackage
$startArgs = @('shell', 'am', 'start', '-W', '-n', "$kPackage/$kActivity")
if ($Page) {
    # adb runs the command through the device's shell, so a page name with a space in it is quoted for that shell
    $startArgs += @('--es', 'page', "'$Page'")
}

if ($Theme) {
    $startArgs += @('--es', 'theme', $Theme)
}

if ($Mode) {
    $startArgs += @('--es', 'mode', $Mode)
}

if ($Touch) {
    $startArgs += @('--es', 'touch', $Touch)
}

& $adb @adbTarget @startArgs | Out-Null

if ($Screenshot) {
    Start-Sleep -Milliseconds $SettleMilliseconds

    # adb reports a successful pull on stderr, and when the caller redirects stderr Windows PowerShell 5.1 turns that
    # line into an error record, which 'Stop' would end the script on.
    $ErrorActionPreference = 'Continue'

    # Pull the PNG through a file on the device: exec-out piped through PowerShell would re-encode the bytes.
    $devicePath = '/sdcard/ajut-showroom-capture.png'
    & $adb @adbTarget shell screencap -p $devicePath
    & $adb @adbTarget pull $devicePath $Screenshot | Out-Null
    & $adb @adbTarget shell rm $devicePath
    Write-Output "Saved $Screenshot"
}
