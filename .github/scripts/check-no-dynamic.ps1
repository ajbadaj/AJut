[CmdletBinding()]
param (
    [string[]]$paths = @("libs/AJut.Core", "libs/AJut.UX")
)

# Fails (exit 1) when C# dynamic is used anywhere in AJut.Core or AJut.UX.
#
# dynamic binds at runtime through code generated on the spot, which an ahead of time compiled build cannot do: on an
#  iOS Release build it throws the first time it runs. The trim and AOT analyzers these libraries turn on
#  (IsAotCompatible) do not report it, so this check does. Generic math (INumber<T> and friends) or a switch over the
#  known types does the same job without it.
#
# Comments and string literals are blanked out first, so the word in prose ("dynamic code") is not a hit, and line
#  numbers stay true because every newline is kept.

$tokenPattern = [regex]'@"(?:[^"]|"")*"|\$?"(?:\\.|[^"\\\r\n])*"|''(?:\\.|[^''\\\r\n])''|//[^\r\n]*|/\*[\s\S]*?\*/'
$dynamicPattern = [regex]'\bdynamic\b'

try {
    $hits = @()
    foreach ($path in $paths) {
        if (-not (Test-Path $path)) {
            Write-Host "$path does not exist"
            exit 1
        }

        foreach ($file in Get-ChildItem -Path $path -Filter "*.cs" -Recurse -File) {
            if ($file.FullName -match '[\\/](bin|obj)[\\/]') {
                continue
            }

            $text = Get-Content -Raw -Path $file.FullName
            if ($null -eq $text) {
                continue
            }

            $code = $tokenPattern.Replace($text, { param($match) $match.Value -replace '[^\n]', ' ' })
            foreach ($match in $dynamicPattern.Matches($code)) {
                $line = ($code.Substring(0, $match.Index) -split "`n").Count
                $hits += "$($file.FullName):$line"
            }
        }
    }

    if ($hits.Count -gt 0) {
        Write-Host "C# dynamic is used in $($hits.Count) place(s), and it throws in an ahead of time compiled build (iOS):"
        $hits | ForEach-Object { Write-Host "  $_" }
        exit 1
    }

    Write-Host "No C# dynamic in $($paths -join ', ')"
    exit 0
}
catch {
    Write-Host "Checking for C# dynamic failed: $_"
    exit 1
}
