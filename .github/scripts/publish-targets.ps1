[CmdletBinding()]
param (
    [string]$apiKey,
    [string]$publishTargets = "",
    [string]$packageDirectory = "",
    [switch]$dryRun
)

# Pushes to nuget.org every AJut package whose version in libs/Directory.Build.props isn't on nuget.org yet. Run from
#  the repository root.
#
# A minor or major release gives every package a new version, so all seven go. A fix goes out as the packages
#  set-version-numbers.ps1 bumped for it, and every other package is skipped, since nuget.org already has its version.
#  Which versions nuget.org has comes from its package index (the flat container). -publishTargets, a json list of
#  library names, narrows the packages looked at. A target_projects.json from build-targets.ps1 deliberately does not:
#  it lists only what one machine could build (a CI runner has no Android or Apple workloads).
#
# Each package to push is looked for in -packageDirectory, or by default in libs/<Name>/bin/Release where dotnet pack
#  leaves it. Every .nupkg and .snupkg to push is found before anything is pushed, so a missing one stops the publish
#  rather than leaving part of a release on nuget.org. dotnet nuget push uploads the .snupkg beside a .nupkg with it.
#  Packages go in dependency order, so nothing goes up before the packages it needs, and a failed push stops the run and
#  says what already went up.
#
# A version nuget.org has accepted is validated for a few minutes before its index lists it, so a run straight after a
#  publish can try to push it again. --skip-duplicate turns nuget.org's refusal of a version it already has into a skip,
#  so that rerun goes through.
#
# At the end it prints the git tags this clone doesn't have yet for the versions now on nuget.org (or about to be, in a
#  dry run): <PackageId>/v<version> for each package, and v<major>.<minor>.0 when every package is at <major>.<minor>.0.
#  They go on the commit that was packed, once the upload has gone through.

. "$PSScriptRoot/ajut-packages.ps1"

$kNuGetSource = "https://api.nuget.org/v3/index.json"
$kFlatContainer = "https://api.nuget.org/v3-flatcontainer"

# The versions nuget.org has for a package. A package nuget.org has never had has no index at all (a 404), which means
#  no versions rather than a failure. Anything else going wrong stops the publish, since there'd be no telling what's
#  already up.
function Get-PublishedVersions {
    param([string]$PackageId)

    $url = "$kFlatContainer/$($PackageId.ToLowerInvariant())/index.json"
    try {
        return @((Invoke-RestMethod -Uri $url -UseBasicParsing -ErrorAction Stop).versions)
    } catch {
        $response = $_.Exception.Response
        if (($null -ne $response) -and ([int]$response.StatusCode -eq 404)) {
            return @()
        }
        throw "Couldn't read which versions of $PackageId nuget.org has from $url`: $($_.Exception.Message)"
    }
}

try {
    Write-Host "Starting publish process..."

    if ((-not $dryRun) -and (-not $apiKey)) {
        Write-Error "Pass -apiKey, or -dryRun to see what would be pushed"
        exit 1
    }

    $packages = Get-AJutPackages
    $packageNames = @($packages | ForEach-Object { $_.Name })
    $versions = Read-AJutVersions -Text (Get-Content -Raw -Path $kAJutPropsPath -ErrorAction Stop) -PackageNames $packageNames

    $targetProjects = $packageNames
    if ($publishTargets -ne "") {
        # ConvertFrom-Json hands a json array down the pipeline as one object in Windows PowerShell 5.1, and as its
        #  elements in PowerShell 7. Unrolling it gives the same flat list in both.
        $requested = @((ConvertFrom-Json $publishTargets) | ForEach-Object { $_ })
        $unknown = @($requested | Where-Object { $packageNames -notcontains $_ })
        if ($unknown.Count -gt 0) {
            Write-Error "Not packages under libs/: $($unknown -join ', '). The packages are: $($packageNames -join ', ')"
            exit 1
        }

        # Kept in dependency order whatever order the list gave them in
        $targetProjects = @($packageNames | Where-Object { $requested -contains $_ })
    }

    if ($targetProjects.Count -eq 0) {
        Write-Error "No projects to publish"
        exit 1
    }

    # 1. Which versions nuget.org doesn't have yet
    $toPush = @()
    foreach ($projectName in $targetProjects) {
        $version = Get-AJutPackageVersion -Versions $versions -Name $projectName
        if (@(Get-PublishedVersions -PackageId $projectName) -contains $version) {
            Write-Host "$projectName $version is already on nuget.org. Skipping."
        } else {
            $toPush += [pscustomobject]@{
                Name = $projectName
                Version = $version
            }
        }
    }

    if ($toPush.Count -eq 0) {
        Write-Host "Nothing to push: nuget.org has every version."
    } else {
        Write-Host "To push: $(($toPush | ForEach-Object { "$($_.Name) $($_.Version)" }) -join ', ')"
    }

    # 2. Find every package to push before pushing any
    $missing = @()
    foreach ($entry in $toPush) {
        $directory = if ($packageDirectory -ne "") { $packageDirectory } else { "libs/$($entry.Name)/bin/Release" }
        $entry | Add-Member -NotePropertyName Nupkg -NotePropertyValue (Join-Path $directory "$($entry.Name).$($entry.Version).nupkg")
        $snupkg = Join-Path $directory "$($entry.Name).$($entry.Version).snupkg"
        if (-not (Test-Path $entry.Nupkg)) {
            $missing += $entry.Nupkg
        }
        if (-not (Test-Path $snupkg)) {
            $missing += $snupkg
        }
    }

    if ($missing.Count -gt 0) {
        Write-Error "Nothing was pushed. Missing: $($missing -join ', ')"
        exit 1
    }

    # 3. Push them, in order, stopping at the first failure
    $pushed = @()
    foreach ($entry in $toPush) {
        Write-Host "=========================================================================================================="
        Write-Host "--> Publishing $($entry.Nupkg) (and its .snupkg)"
        if ($dryRun -eq $true) {
            Write-Host "DRY RUN: The following command would be executed:" -ForegroundColor Yellow
            Write-Host "dotnet nuget push `"$($entry.Nupkg)`" --api-key API_KEY --source $kNuGetSource --skip-duplicate" -ForegroundColor Cyan
        } else {
            dotnet nuget push "$($entry.Nupkg)" --api-key $apiKey --source $kNuGetSource --skip-duplicate
            if ($LASTEXITCODE -ne 0) {
                $already = if ($pushed.Count -gt 0) { $pushed -join ', ' } else { "nothing" }
                Write-Error "Push failed: $($entry.Nupkg). Already pushed: $already"
                exit 1
            }
            $pushed += $entry.Nupkg
        }
    }

    # 4. The tags still to make. A tag only another clone has shows up here too, and git push says so when it's pushed.
    $tags = @()
    foreach ($projectName in $targetProjects) {
        $version = Get-AJutPackageVersion -Versions $versions -Name $projectName
        $tags += [pscustomobject]@{
            Name = "$projectName/v$version"
            Message = "$projectName $version"
        }
    }

    $wholeSet = "$($versions.Major).$($versions.Minor).0"
    $everyPackageAtWholeSet = @($packageNames | Where-Object { (Get-AJutPackageVersion -Versions $versions -Name $_) -ne $wholeSet }).Count -eq 0
    if ($everyPackageAtWholeSet -and ($targetProjects.Count -eq $packageNames.Count)) {
        $tags += [pscustomobject]@{
            Name = "v$wholeSet"
            Message = "AJut $wholeSet"
        }
    }

    $newTags = @($tags | Where-Object { -not (& git tag --list $_.Name) })
    if ($newTags.Count -gt 0) {
        $commit = (& git rev-parse HEAD)
        Write-Host "=========================================================================================================="
        Write-Host "Once the upload has gone through, tag the commit the packages were packed from (HEAD here is $commit) and push the tags:"
        foreach ($tag in $newTags) {
            Write-Host "git tag -a $($tag.Name) $commit -m `"$($tag.Message)`"" -ForegroundColor Cyan
        }
        Write-Host "git push origin $(($newTags | ForEach-Object { $_.Name }) -join ' ')" -ForegroundColor Cyan
    }

    Write-Host "Publish process complete."
} catch {
    Write-Error $_.Exception.Message
    exit 1
}
