[CmdletBinding()]
param (
    [ValidateSet("major", "minor", "patch")]
    [string]$bump = "",
    [string]$version = "",
    [switch]$dryRun
)

# Sets the one version every AJut package shares, held in libs/Directory.Build.props. Run from the repository root.
#
#   ./.github/scripts/set-version-numbers.ps1 -bump minor        2.0.0 becomes 2.1.0
#   ./.github/scripts/set-version-numbers.ps1 -bump patch        2.1.0 becomes 2.1.1
#   ./.github/scripts/set-version-numbers.ps1 -version 3.0.0     sets it outright
#
# A bump zeroes everything to its right, the way SemVer does, so a minor bump from 2.1.4 is 2.2.0. Every publish moves
#  the whole set, so there's no choosing projects. -dryRun says what the version would become and writes nothing. The
#  new version is the script's output, so a caller can take it: $version = ./.github/scripts/set-version-numbers.ps1 ...

$kPropsPath = "libs/Directory.Build.props"

try {
    if (($bump -eq "") -eq ($version -eq "")) {
        Write-Error "Pass one of -bump (major, minor, or patch) or -version (three parts, like 2.1.0), not both or neither"
        exit 1
    }

    $propsPath = (Resolve-Path -Path $kPropsPath -ErrorAction Stop).Path
    $text = [System.IO.File]::ReadAllText($propsPath)

    # Exactly one three part <Version>, or there's no telling which one MSBuild would use
    $found = [regex]::Matches($text, '<Version>(\d+)\.(\d+)\.(\d+)</Version>')
    if ($found.Count -ne 1) {
        Write-Error "Expected exactly one <Version>major.minor.patch</Version> in $kPropsPath, found $($found.Count)"
        exit 1
    }

    $current = $found[0]
    $major = [int]$current.Groups[1].Value
    $minor = [int]$current.Groups[2].Value
    $patch = [int]$current.Groups[3].Value
    $currentVersion = "$major.$minor.$patch"

    switch ($bump) {
        "major" { $newVersion = "$($major + 1).0.0" }
        "minor" { $newVersion = "$major.$($minor + 1).0" }
        "patch" { $newVersion = "$major.$minor.$($patch + 1)" }
        default { $newVersion = $version }
    }

    if ($newVersion -notmatch '^\d+\.\d+\.\d+$') {
        Write-Error "$newVersion is not a three part version (major.minor.patch, like 2.1.0)"
        exit 1
    }

    if ($dryRun -eq $true) {
        Write-Host " ... Version would go from $currentVersion to $newVersion"
    } else {
        # A plain text swap of the one element, so the rest of the file stays byte for byte as it was. UTF-8 without
        #  a byte order mark, matching how the file is kept.
        $newText = $text.Substring(0, $current.Index) + "<Version>$newVersion</Version>" + $text.Substring($current.Index + $current.Length)
        [System.IO.File]::WriteAllText($propsPath, $newText, (New-Object System.Text.UTF8Encoding $false))
        Write-Host " ... Version set from $currentVersion to $newVersion"
    }

    return $newVersion
} catch {
    Write-Error $_.Exception.Message
    exit 1
}
