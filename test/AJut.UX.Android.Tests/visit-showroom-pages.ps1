# Opens every page of the Android showroom on a device or emulator through adb, in the plain and the AJut look,
# saves a screenshot of each, and prints any crash or exception lines logcat caught while the page was up. Nothing
# here touches the screen: launch, screencap, pull and logcat only. The showroom must already be installed; build it
# in Release (dotnet build sandbox/android/AJutShowRoomAndroid.csproj -c Release, then adb install -r on the
# -Signed.apk) to check a trimmed build.
#
#   .\visit-showroom-pages.ps1 -OutDir C:\shots                       every page, both looks
#   .\visit-showroom-pages.ps1 -OutDir C:\shots -Pages ToggleStrip    one page
#   .\visit-showroom-pages.ps1 -OutDir C:\shots -Themes ajut -Device emulator-5554
#   .\visit-showroom-pages.ps1 -OutDir C:\shots -Themes ajut -Touch off   the AJut theme's desktop palette
#   .\visit-showroom-pages.ps1 -OutDir C:\shots -Themes ajut -Mode dark -Pages 'Theme Tester'
#
# Light and dark follow the device's night mode (adb shell cmd uimode night yes, or no, before a run), unless -Mode
# forces one in the app, the way the showroom's theme bar does.
#
# Screenshots are written on the device and pulled, rather than read through adb exec-out: Windows PowerShell 5.1
# re-encodes a native command's output when it is redirected to a file, so "adb exec-out screencap -p > shot.png"
# writes a corrupt image.
#
# The page names are the titles in sandbox/android/ShowroomMap.cs, which the showroom's "page" launch extra opens.
# A page added there goes in the default list here too.
#
# Exit code is 0 when every page stayed up, and 1 when the app died on any of them.
param(
    [Parameter(Mandatory = $true)]
    [string]$OutDir,
    [string[]]$Pages = @('Home', 'Controls', 'BrushButton', 'ColorEditIngressControl', 'EnumComboBox', 'EnumToggleStrip', 'GeometryButton', 'NumericEditor', 'ToggleStrip', 'Debug', 'Theme Tester'),
    [string[]]$Themes = @('plain', 'ajut'),
    # The theme bar's Dark, Light or System; left out, System, or with -Touch the light or dark the device shows
    [ValidateSet('dark', 'light', 'system')]
    [string]$Mode,
    # The AJut theme's touch variant, on or off; left out, the app's default (on)
    [ValidateSet('on', 'off')]
    [string]$Touch,
    [string]$Device,
    [int]$SettleSeconds = 3
)

# Not 'Stop': adb reports a successful pull on stderr, and when the caller redirects stderr Windows PowerShell 5.1
# turns each of those lines into an error record, which 'Stop' would end the run on.
$ErrorActionPreference = 'Continue'

$kPackage = 'dev.ajut.showroom.android'
$kActivity = 'dev.ajut.showroom.android/dev.ajut.showroom.android.MainActivity'
$kDeviceShot = '/sdcard/ajut-showroom-shot.png'

$sdkRoot = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { 'C:\Program Files (x86)\Android\android-sdk' }
$adb = Join-Path $sdkRoot 'platform-tools\adb.exe'
if (-not (Test-Path $adb)) {
    throw "adb not found at $adb. Set ANDROID_HOME to the Android SDK."
}

$adbTarget = @()
if ($Device) {
    $adbTarget = @('-s', $Device)
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$anyDied = $false

foreach ($theme in $Themes) {
    foreach ($page in $Pages) {
        # 1. Start clean, so the log read back is this page's only
        & $adb @adbTarget shell am force-stop $kPackage
        & $adb @adbTarget logcat -c

        $startArgs = @('shell', 'am', 'start', '-W', '-n', $kActivity, '--es', 'theme', $theme)
        if ($page -ne 'Home') {
            # adb runs the command through the device's shell, so a page name with a space in it is quoted for that shell
            $startArgs += @('--es', 'page', "'$page'")
        }

        $modeTag = ''
        if ($Mode) {
            $startArgs += @('--es', 'mode', $Mode)
            $modeTag = "-$Mode"
        }

        $touchTag = ''
        if ($Touch) {
            $startArgs += @('--es', 'touch', $Touch)
            $touchTag = "-touch$Touch"
        }

        & $adb @adbTarget @startArgs | Out-Null
        Start-Sleep -Seconds $SettleSeconds

        # 2. Screenshot, written on the device and pulled (see the header)
        $shot = Join-Path $OutDir ("$theme$modeTag$touchTag-$page.png" -replace ' ', '')
        & $adb @adbTarget shell screencap -p $kDeviceShot
        & $adb @adbTarget pull $kDeviceShot $shot | Out-Null

        # 3. Report whether the app is still up, and the app's own crash lines. The system's lines about the
        #    previous instance being stopped are left out.
        $processId = & $adb @adbTarget shell pidof $kPackage
        $problems = & $adb @adbTarget logcat -d -v brief | Where-Object {
            ($_ -cmatch 'FATAL|E/AndroidRuntime|UNHANDLED EXCEPTION|InflateException|ClassNotFoundException|MissingMethodException|TypeLoadException') -and ($_ -notmatch 'DeadObjectException|dispatchAppVisibility')
        }

        $status = if ($processId) { 'up' } else { 'DIED' }
        if (-not $processId) {
            $anyDied = $true
        }

        Write-Output ("{0,-6} {1,-24} {2,-5} {3}" -f $theme, $page, $status, $shot)
        $problems | Select-Object -First 8 | ForEach-Object { Write-Output "    $_" }
    }
}

& $adb @adbTarget shell rm -f $kDeviceShot

if ($anyDied) {
    exit 1
}

exit 0
