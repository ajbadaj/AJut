[CmdletBinding()]
param (
    [ValidateSet("major", "minor", "patch")]
    [string]$bump = "",
    [string]$version = "",
    [string[]]$package = @(),
    [switch]$dryRun
)

# Bumps the AJut package versions held in libs/Directory.Build.props. Run from the repository root.
#
#   ./.github/scripts/set-version-numbers.ps1 -bump minor                        every 2.0.x becomes 2.1.0
#   ./.github/scripts/set-version-numbers.ps1 -bump major                        every 2.1.x becomes 3.0.0
#   ./.github/scripts/set-version-numbers.ps1 -version 3.0                       every package becomes 3.0.0
#   ./.github/scripts/set-version-numbers.ps1 -bump patch -package AJut.UX.Wpf   AJut.UX.Wpf 2.1.0 becomes 2.1.1
#
# Every package shares the major and minor, so a minor or major bump, or -version, moves the whole set and sets every
#  patch back to 0. The patch is per package. -package names the packages a fix is in (comma separated for a release
#  with fixes in several), and every package built on one of them is bumped with it, since its dependency on the fixed
#  package asks for the fixed version only once it's repacked. So a fix in AJut.Core moves all seven packages, one in
#  AJut.UX moves AJut.UX and the five platform packages, and one in a platform package moves only that package. Which
#  package is built on which comes from the ProjectReferences in each library's csproj. A package gets one bump however
#  many of the named packages it's built on.
#
# -dryRun says what would change and writes nothing. The output is one entry per package that moves, with its old and
#  new version, so a caller can take it: $moved = ./.github/scripts/set-version-numbers.ps1 ...

. "$PSScriptRoot/ajut-packages.ps1"

try {
    if (($bump -eq "") -eq ($version -eq "")) {
        Write-Error "Pass one of -bump (major, minor, or patch) or -version (major.minor, like 3.0), not both or neither"
        exit 1
    }

    if (($bump -eq "patch") -ne ($package.Count -gt 0)) {
        Write-Error "-package goes with -bump patch, and -bump patch needs -package: the packages the fix is in"
        exit 1
    }

    $packages = Get-AJutPackages
    $packageNames = @($packages | ForEach-Object { $_.Name })

    $propsPath = (Resolve-Path -Path $kAJutPropsPath -ErrorAction Stop).Path
    $text = [System.IO.File]::ReadAllText($propsPath)
    $versions = Read-AJutVersions -Text $text -PackageNames $packageNames

    # 1. Work out the new numbers, starting from the current ones
    $newMajor = $versions.Major
    $newMinor = $versions.Minor
    $newPatches = @{}
    foreach ($name in $packageNames) {
        $newPatches[$name] = [int]$versions.Patches[$name].Groups[2].Value
    }

    if ($bump -eq "patch") {
        # From PowerShell, -package a,b arrives as two entries; from another shell it can arrive as one "a,b"
        $requested = @($package | ForEach-Object { $_ -split "," } | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" })
        $named = @()
        foreach ($request in $requested) {
            # -eq ignores case, so this also gives back the name as the folder spells it
            $match = @($packageNames | Where-Object { $_ -eq $request })
            if ($match.Count -eq 0) {
                if (Test-Path "libs/$request/$request.csproj") {
                    Write-Error "$request isn't a package of its own (its csproj opts out of packing), so it has no patch. Patch the package it ships inside."
                } else {
                    Write-Error "There's no package called $request under libs/. The packages are: $($packageNames -join ', ')"
                }
                exit 1
            }
            $named += $match[0]
        }

        foreach ($name in (Get-AJutPackagesBuiltOn -Packages $packages -Names $named)) {
            $newPatches[$name] = $newPatches[$name] + 1
        }
    } else {
        if ($bump -eq "major") {
            $newMajor = $newMajor + 1
            $newMinor = 0
        } elseif ($bump -eq "minor") {
            $newMinor = $newMinor + 1
        } else {
            # A trailing .0 is taken, since 3.0.0 is the natural way to say it, but no other patch: the patch is per
            #  package, so the whole set can only be set to x.y.0
            if ($version -notmatch '^(\d+)\.(\d+)(\.0)?$') {
                Write-Error "$version isn't a major.minor (like 3.0). The patch is per package, so -version sets only the part every package shares."
                exit 1
            }
            $newMajor = [int]$Matches[1]
            $newMinor = [int]$Matches[2]
        }

        foreach ($name in $packageNames) {
            $newPatches[$name] = 0
        }
    }

    # 2. What moves
    $moved = @()
    foreach ($name in $packageNames) {
        $from = Get-AJutPackageVersion -Versions $versions -Name $name
        $to = "$newMajor.$newMinor.$($newPatches[$name])"
        if ($from -ne $to) {
            $moved += [pscustomobject]@{
                Package = $name
                From = $from
                To = $to
            }
        }
    }

    if ($moved.Count -eq 0) {
        Write-Host " ... Nothing to change: every package is already at that version"
        return $moved
    }

    $verb = if ($dryRun -eq $true) { "would go" } else { "goes" }
    foreach ($entry in $moved) {
        Write-Host " ... $($entry.Package) $verb from $($entry.From) to $($entry.To)"
    }

    # 3. Write only the numbers that changed, so the rest of the file stays byte for byte as it was. Later places in
    #  the text go first, so every earlier match still points at the right characters. UTF-8 without a byte order
    #  mark, matching how the file is kept.
    if ($dryRun -ne $true) {
        $edits = @()
        if (($newMajor -ne $versions.Major) -or ($newMinor -ne $versions.Minor)) {
            $edits += [pscustomobject]@{
                Index = $versions.MajorMinorMatch.Index
                Length = $versions.MajorMinorMatch.Length
                Text = "<AJutVersionMajorMinor>$newMajor.$newMinor</AJutVersionMajorMinor>"
            }
        }
        foreach ($name in $packageNames) {
            $number = $versions.Patches[$name].Groups[2]
            if ([int]$number.Value -ne $newPatches[$name]) {
                $edits += [pscustomobject]@{
                    Index = $number.Index
                    Length = $number.Length
                    Text = "$($newPatches[$name])"
                }
            }
        }

        $newText = $text
        foreach ($edit in ($edits | Sort-Object -Property Index -Descending)) {
            $newText = $newText.Substring(0, $edit.Index) + $edit.Text + $newText.Substring($edit.Index + $edit.Length)
        }
        [System.IO.File]::WriteAllText($propsPath, $newText, (New-Object System.Text.UTF8Encoding $false))
    }

    return $moved
} catch {
    Write-Error $_.Exception.Message
    exit 1
}
