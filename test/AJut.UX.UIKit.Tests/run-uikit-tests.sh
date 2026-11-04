#!/usr/bin/env bash
# Builds the AJut UIKit test app, runs it in an iOS simulator in light and then in dark, and prints the results it
# writes. Mac only. Nothing here touches the Mac's screen: build, install, launch, the simulator's appearance setting
# (put back as it was when the run ends), and the app's own results file.
#
#   bash run-uikit-tests.sh                           every test, on the first available iPhone simulator
#   bash run-uikit-tests.sh -f FlatTree               the tests whose Class.Method name contains FlatTree
#   bash run-uikit-tests.sh -d "iPhone 17 Pro" -n     on that simulator, without building first
#   bash run-uikit-tests.sh -t 600                    waiting up to 600 seconds for each run (300 by default)
#
# Exit code is 0 when every test passed in both appearances, 1 when any failed, and 2 when a run never finished.

set -u

kBundleId="dev.ajut.ux.uikit.tests"
kResultsFileName="ajut-test-results.txt"
kUdidPattern='[0-9A-F]{8}-([0-9A-F]{4}-){3}[0-9A-F]{12}'

here="$(cd "$(dirname "$0")" && pwd)"
project="$here/AJut.UX.UIKit.Tests.csproj"
filter=""
device=""
noBuild=0
timeoutSeconds=300

while getopts "f:d:nt:" option; do
    case "$option" in
        f) filter="$OPTARG" ;;
        d) device="$OPTARG" ;;
        n) noBuild=1 ;;
        t) timeoutSeconds="$OPTARG" ;;
        *) echo "Usage: $0 [-f filter] [-d simulator name] [-n] [-t timeout seconds]"; exit 2 ;;
    esac
done

# 1. The simulator: the one named, or the first available iPhone, booted if it isn't
if [ -z "$device" ]; then
    device="$(xcrun simctl list devices available | grep -E '^[[:space:]]+iPhone' | head -n 1 | sed -E 's/^[[:space:]]+//; s/ \([0-9A-F-]{36}\).*$//')"
fi

udid="$(xcrun simctl list devices available | grep -F "    $device (" | head -n 1 | grep -oE "$kUdidPattern")"
if [ -z "$udid" ]; then
    echo "No available simulator named \"$device\". See: xcrun simctl list devices available"
    exit 2
fi

echo "Simulator: $device ($udid)"
xcrun simctl boot "$udid" > /dev/null 2>&1
xcrun simctl bootstatus "$udid" -b > /dev/null

# 2. Build and install, for the simulator's architecture
if [ "$(uname -m)" = "arm64" ]; then
    runtimeId="iossimulator-arm64"
else
    runtimeId="iossimulator-x64"
fi

if [ "$noBuild" -eq 0 ]; then
    if ! dotnet build "$project" -c Debug -f net10.0-ios -r "$runtimeId" -nologo -v q; then
        echo "The test app did not build."
        exit 2
    fi
fi

appPath="$here/bin/Debug/net10.0-ios/$runtimeId/AJut.UX.UIKit.Tests.app"
if ! xcrun simctl install "$udid" "$appPath"; then
    echo "The test app did not install from $appPath."
    exit 2
fi

container="$(xcrun simctl get_app_container "$udid" "$kBundleId" data)"
results="$container/Documents/$kResultsFileName"
logFile="$(mktemp -t ajut-uikit-tests)"

# 3. Run once per appearance, waiting for the results file's DONE line
originalAppearance="$(xcrun simctl ui "$udid" appearance 2> /dev/null)"
exitCode=0
for appearance in light dark; do
    xcrun simctl ui "$udid" appearance "$appearance"
    rm -f "$results"

    SIMCTL_CHILD_AJUT_TEST_FILTER="$filter" xcrun simctl launch --console-pty --terminate-running-process "$udid" "$kBundleId" > "$logFile" 2>&1 &
    launchPid=$!

    deadline=$(( $(date +%s) + timeoutSeconds ))
    finished=0
    while [ "$(date +%s)" -lt "$deadline" ]; do
        if [ -f "$results" ] && grep -q '^DONE ' "$results"; then
            finished=1
            break
        fi

        sleep 1
    done

    xcrun simctl terminate "$udid" "$kBundleId" > /dev/null 2>&1
    wait "$launchPid" 2> /dev/null

    if [ "$finished" -eq 0 ]; then
        echo "[$appearance] The run did not finish within $timeoutSeconds seconds. The app's last output:"
        tail -n 40 "$logFile"
        exitCode=2
        continue
    fi

    grep -E '^(PASS|FAIL|SKIP|DONE) ' "$results" | sed "s/^/[$appearance] /"
    failedCount="$(grep -E '^DONE ' "$results" | grep -oE 'failed=[0-9]+' | cut -d= -f2)"
    if [ "${failedCount:-0}" -gt 0 ] && [ "$exitCode" -eq 0 ]; then
        exitCode=1
    fi
done

# 4. Put the simulator's appearance back
if [ "$originalAppearance" = "light" ] || [ "$originalAppearance" = "dark" ]; then
    xcrun simctl ui "$udid" appearance "$originalAppearance"
fi

rm -f "$logFile"
exit "$exitCode"
