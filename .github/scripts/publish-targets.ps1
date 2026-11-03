[CmdletBinding()]
param (
    [string]$apiKey,
    [string]$publishTargets = "",
    [string]$packageDirectory = "",
    [switch]$dryRun
)

# Pushes the AJut packages at the version in libs/Directory.Build.props to nuget.org. Run from the repository root.
#
# Every package shares that version and every publish moves the whole set, so by default this pushes every packable
#  library under libs/. -publishTargets, a json list of library names, narrows it. A target_projects.json from
#  build-targets.ps1 deliberately does not: it lists only what one machine could build (a CI runner has no Android or
#  Apple workloads), and a stale one left in the repository would quietly publish part of the set.
#
# Each package is looked for in -packageDirectory, or by default in libs/<Name>/bin/Release where dotnet pack leaves
#  it. Each .nupkg and its .snupkg are found before anything is pushed, so a missing one stops the publish rather than
#  leaving half the set on nuget.org. dotnet nuget push uploads the .snupkg beside a .nupkg with it. A failed push
#  stops the run and says what already went up.

$kPropsPath = "libs/Directory.Build.props"
$kNuGetSource = "https://api.nuget.org/v3/index.json"

try {
    Write-Host "Starting publish process..."

    if ((-not $dryRun) -and (-not $apiKey)) {
        Write-Error "Pass -apiKey, or -dryRun to see what would be pushed"
        exit 1
    }

    # The one version, read the way set-version-numbers.ps1 writes it
    $props = Get-Content -Raw -Path $kPropsPath -ErrorAction Stop
    $found = [regex]::Matches($props, '<Version>(\d+\.\d+\.\d+)</Version>')
    if ($found.Count -ne 1) {
        Write-Error "Expected exactly one <Version>major.minor.patch</Version> in $kPropsPath, found $($found.Count)"
        exit 1
    }
    $version = $found[0].Groups[1].Value
    Write-Host "Version: $version"

    if ($publishTargets -ne "") {
        # ConvertFrom-Json hands a json array down the pipeline as one object in Windows PowerShell 5.1, and as its
        #  elements in PowerShell 7. Unrolling it gives the same flat list in both.
        $targetProjects = @((ConvertFrom-Json $publishTargets) | ForEach-Object { $_ })
    } else {
        # Every libs/<Name>/<Name>.csproj that doesn't opt out of packing. AJut.Core sorts before AJut.UX, and AJut.UX
        #  before every stack, so name order is also dependency order.
        $targetProjects = @(
            Get-ChildItem -Path "libs" -Directory |
                Sort-Object -Property Name |
                Where-Object { Test-Path "libs/$($_.Name)/$($_.Name).csproj" } |
                Where-Object {
                    [xml]$project = Get-Content -Raw -Path "libs/$($_.Name)/$($_.Name).csproj"
                    @($project.SelectNodes("//IsPackable") | Where-Object { $_.InnerText.Trim() -eq "false" }).Count -eq 0
                } |
                ForEach-Object { $_.Name }
        )
    }

    if ($targetProjects.Count -eq 0) {
        Write-Error "No projects to publish"
        exit 1
    }

    # 1. Find every package first
    $packages = @()
    $missing = @()
    foreach ($projectName in $targetProjects) {
        $directory = if ($packageDirectory -ne "") { $packageDirectory } else { "libs/$projectName/bin/Release" }
        $nupkg = Join-Path $directory "$projectName.$version.nupkg"
        $snupkg = Join-Path $directory "$projectName.$version.snupkg"
        if (-not (Test-Path $nupkg)) {
            $missing += $nupkg
        }
        if (-not (Test-Path $snupkg)) {
            $missing += $snupkg
        }
        $packages += $nupkg
    }

    if ($missing.Count -gt 0) {
        Write-Error "Nothing was pushed. Missing for $version`: $($missing -join ', ')"
        exit 1
    }

    # 2. Then push them, in order, stopping at the first failure
    $pushed = @()
    foreach ($nupkg in $packages) {
        Write-Host "=========================================================================================================="
        Write-Host "--> Publishing $nupkg (and its .snupkg)"
        if ($dryRun -eq $true) {
            Write-Host "DRY RUN: The following command would be executed:" -ForegroundColor Yellow
            Write-Host "dotnet nuget push `"$nupkg`" --api-key API_KEY --source $kNuGetSource" -ForegroundColor Cyan
        } else {
            dotnet nuget push "$nupkg" --api-key $apiKey --source $kNuGetSource
            if ($LASTEXITCODE -ne 0) {
                $already = if ($pushed.Count -gt 0) { $pushed -join ', ' } else { "nothing" }
                Write-Error "Push failed: $nupkg. Already pushed: $already"
                exit 1
            }
            $pushed += $nupkg
        }
    }

    Write-Host "Publish process complete."
} catch {
    Write-Error $_.Exception.Message
    exit 1
}
