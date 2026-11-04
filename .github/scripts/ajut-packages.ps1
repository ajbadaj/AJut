# Dot sourced by set-version-numbers.ps1 and publish-targets.ps1, so both read the AJut packages and their versions the
#  same way. Like them, it expects to run from the repository root.
#
# The versions live in libs/Directory.Build.props: <AJutVersionMajorMinor> holds the major and minor every package
#  shares, and one <AJutVersionPatch> line per package holds that package's patch. The patterns below are the shape of
#  those lines. A line written in any other shape stops both scripts rather than being skipped, since a skipped line is
#  a package whose version nobody moves.

$kAJutPropsPath = "libs/Directory.Build.props"
$kAJutMajorMinorPattern = '<AJutVersionMajorMinor>(\d+)\.(\d+)</AJutVersionMajorMinor>'
$kAJutPatchPattern = '<AJutVersionPatch Condition="''\$\(AJutVersionedAs\)'' == ''([\w.]+)''">(\d+)</AJutVersionPatch>'

# Every libs/<Name>/<Name>.csproj that doesn't opt out of packing, each with the AJut packages it references, in
#  dependency order: a package comes after every package it references, and packages that are equally ready go in name
#  order, so the order is the same on every run. A reference to a project that isn't packed (the source generator) is a
#  build time reference, not a package dependency, so it's left out.
function Get-AJutPackages {
    $references = [ordered]@{}
    foreach ($folder in (Get-ChildItem -Path "libs" -Directory | Sort-Object -Property Name)) {
        $csprojPath = "libs/$($folder.Name)/$($folder.Name).csproj"
        if (-not (Test-Path $csprojPath)) {
            continue
        }

        [xml]$project = Get-Content -Raw -Path $csprojPath
        if (@($project.SelectNodes("//IsPackable") | Where-Object { $_.InnerText.Trim() -eq "false" }).Count -gt 0) {
            continue
        }

        # The csproj files write their paths with backslashes, which aren't a separator to .NET outside Windows
        $references[$folder.Name] = @(
            $project.SelectNodes("//ProjectReference") |
                ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension(($_.GetAttribute("Include") -replace '\\', '/')) }
        )
    }

    # Place everything whose references are all placed, a round at a time, until nothing is left
    $ordered = @()
    $placed = @{}
    while ($ordered.Count -lt $references.Count) {
        $ready = @(
            $references.Keys | Where-Object {
                $name = $_
                (-not $placed.ContainsKey($name)) -and (@($references[$name] | Where-Object { $references.Contains($_) -and (-not $placed.ContainsKey($_)) }).Count -eq 0)
            }
        )
        if ($ready.Count -eq 0) {
            $left = @($references.Keys | Where-Object { -not $placed.ContainsKey($_) })
            throw "The packages under libs/ reference each other in a circle, so there's no dependency order: $($left -join ', ')"
        }

        foreach ($name in $ready) {
            $placed[$name] = $true
            $ordered += [pscustomobject]@{
                Name = $name
                References = @($references[$name] | Where-Object { $references.Contains($_) })
            }
        }
    }

    return $ordered
}

# The named packages and every package built on any of them, directly or through another package, in dependency order.
#  These are the packages a fix in the named ones has to move.
function Get-AJutPackagesBuiltOn {
    param(
        [object[]]$Packages,
        [string[]]$Names
    )

    $moves = @{}
    foreach ($name in $Names) {
        $moves[$name] = $true
    }

    # In dependency order, everything a package references is decided before the package itself, so one pass does it
    foreach ($package in $Packages) {
        if (@($package.References | Where-Object { $moves.ContainsKey($_) }).Count -gt 0) {
            $moves[$package.Name] = $true
        }
    }

    return @($Packages | Where-Object { $moves.ContainsKey($_.Name) } | ForEach-Object { $_.Name })
}

# The version table in the props text, which has to hold exactly one major.minor and exactly one patch line for each of
#  -PackageNames, and nothing for anything else. Every number comes back with its regex match, which carries its place
#  in the text, so set-version-numbers.ps1 can rewrite the numbers it changes and leave every other byte as it was.
function Read-AJutVersions {
    param(
        [string]$Text,
        [string[]]$PackageNames
    )

    $majorMinor = [regex]::Matches($Text, $kAJutMajorMinorPattern)
    if ($majorMinor.Count -ne 1) {
        throw "Expected exactly one <AJutVersionMajorMinor>major.minor</AJutVersionMajorMinor> in $kAJutPropsPath, found $($majorMinor.Count)"
    }

    $patchMatches = [regex]::Matches($Text, $kAJutPatchPattern)
    $patchElementCount = [regex]::Matches($Text, '<AJutVersionPatch[\s>]').Count
    if ($patchMatches.Count -ne $patchElementCount) {
        throw "$($patchElementCount - $patchMatches.Count) <AJutVersionPatch> line(s) in $kAJutPropsPath aren't in the shape the others are in, so their numbers can't be read"
    }

    $patches = [ordered]@{}
    foreach ($match in $patchMatches) {
        $name = $match.Groups[1].Value
        if ($patches.Contains($name)) {
            throw "$name has more than one <AJutVersionPatch> line in $kAJutPropsPath"
        }
        $patches[$name] = $match
    }

    $missing = @($PackageNames | Where-Object { -not $patches.Contains($_) })
    if ($missing.Count -gt 0) {
        throw "No <AJutVersionPatch> line in $kAJutPropsPath for: $($missing -join ', ')"
    }

    $unknown = @($patches.Keys | Where-Object { $PackageNames -notcontains $_ })
    if ($unknown.Count -gt 0) {
        throw "$kAJutPropsPath has an <AJutVersionPatch> line for something that isn't a package under libs/: $($unknown -join ', ')"
    }

    return [pscustomobject]@{
        MajorMinorMatch = $majorMinor[0]
        Major = [int]$majorMinor[0].Groups[1].Value
        Minor = [int]$majorMinor[0].Groups[2].Value
        Patches = $patches
    }
}

# A package's whole version, from what Read-AJutVersions read
function Get-AJutPackageVersion {
    param(
        [object]$Versions,
        [string]$Name
    )

    return "$($Versions.Major).$($Versions.Minor).$([int]$Versions.Patches[$Name].Groups[2].Value)"
}
