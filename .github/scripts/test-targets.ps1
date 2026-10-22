[CmdletBinding()]
param (
    [string]$testTargets = ""
)

# Test projects live under test/, one folder each, named <Name>.Tests with <Name>.Tests.csproj inside.
#  Each belongs to the longest library in ProjectOrder.json its name starts with, so
#  AJut.Core.Tests and AJut.Core.SourceGenerators.Tests run for AJut.Core, and AJut.UX.WinUI3.Tests
#  runs for AJut.UX.WinUI3 rather than AJut.UX. A suite runs when its library or any library that one is
#  built on is a target. Folders under test/ not named *.Tests (the bench, the
#  crash host, the trim probe) are tools, not suites, and never run here.
#
# Every suite runs even after one fails, and the script exits 1 if any failed, so a failing test
#  fails the check. dotnet test builds each test project itself: the build step only builds the
#  libraries.

# ConvertFrom-Json hands a json array down the pipeline as one object in Windows PowerShell 5.1, and
#  as its elements in PowerShell 7. Unrolling it here gives the same flat list in both.
function ConvertTo-FlatList ([string]$json) {
    (ConvertFrom-Json $json) | ForEach-Object { $_ }
}

# The libraries in ProjectOrder.json that a library is built on, directly or through another one, read from the
#  ProjectReferences in its csproj
function Get-LibraryDependencies ([string]$library, [string[]]$libraries) {
    $found = @()
    $pending = @($library)
    while ($pending.Count -gt 0) {
        $current = $pending[0]
        $pending = @($pending | Select-Object -Skip 1)
        [xml]$project = Get-Content -Raw -Path "libs/$current/$current.csproj"
        foreach ($reference in @($project.SelectNodes("//ProjectReference"))) {
            $name = [System.IO.Path]::GetFileNameWithoutExtension($reference.Include)
            if (($libraries -contains $name) -and ($found -notcontains $name)) {
                $found += $name
                $pending += $name
            }
        }
    }

    $found
}

try {
    Write-Host "Starting test process..."

    $projectOrder = @(ConvertTo-FlatList (Get-Content -Raw -Path "ProjectOrder.json"))

    # If test targets was unset, test whatever the build step built, or everything
    if ($testTargets -eq "") {
        if (Test-Path "target_projects.json") {
            $testTargets = Get-Content -Raw -Path "target_projects.json"
        }
        if (-not $testTargets) {
            $testTargets = Get-Content -Raw -Path "ProjectOrder.json"
        }
    }

    $targetProjects = @(ConvertTo-FlatList $testTargets)
    if ($targetProjects.Count -eq 0) {
        Write-Host "No projects to test. Skipping."
        exit 0
    }

    $testProjects = @(
        Get-ChildItem -Path "test" -Directory |
            Where-Object { $_.Name -like "*.Tests" } |
            ForEach-Object { "test/$($_.Name)/$($_.Name).csproj" } |
            Where-Object { Test-Path $_ }
    )

    # Finding no suites at all means discovery is broken, which is how CI came to pass while running nothing
    if ($testProjects.Count -eq 0) {
        Write-Error "No test projects found under test/ (expected test/<Name>.Tests/<Name>.Tests.csproj)"
        exit 1
    }

    $ran = @()
    $failed = @()
    foreach ($testProject in $testProjects) {
        $testName = [System.IO.Path]::GetFileNameWithoutExtension($testProject)
        $owner = $projectOrder |
            Where-Object { $testName.StartsWith("$_.") } |
            Sort-Object -Property Length -Descending |
            Select-Object -First 1

        if (-not $owner) {
            Write-Host "$testName matches no library in ProjectOrder.json. Skipping."
            continue
        }

        # A suite also runs when a library its owner is built on is a target: AJut.UX.Wpf.Tests runs for a change to
        #  AJut.UX, since the WPF controls sit on AJut.UX's models
        $covered = @($owner) + @(Get-LibraryDependencies $owner $projectOrder)
        $changedCovered = @($covered | Where-Object { $targetProjects -contains $_ })
        if ($changedCovered.Count -eq 0) {
            Write-Host "$testName tests $($covered -join ', '), none of which is a target. Skipping."
            continue
        }

        Write-Host "--> Running $testName (tests for $owner)..."
        dotnet test $testProject --configuration Release --verbosity normal
        $ran += $testName
        if ($LASTEXITCODE -ne 0) {
            $failed += $testName
        }
    }

    Write-Host "Ran $($ran.Count) test project(s): $($ran -join ', ')"
    if ($failed.Count -gt 0) {
        Write-Error "Test project(s) failed: $($failed -join ', ')"
        exit 1
    }

    Write-Host "Test process complete."
} catch {
    Write-Error $_.Exception.Message
    exit 1
}
