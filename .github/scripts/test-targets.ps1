[CmdletBinding()]
param (
    [string]$testTargets = ""
)

# Test projects live under test/, one folder each, named <Name>.Tests with <Name>.Tests.csproj inside.
#  Each belongs to the longest library in ProjectOrder.json its name starts with, so
#  AJut.Core.Tests and AJut.Core.SourceGenerators.Tests run for AJut.Core, and AJut.UX.WinUI3.Tests
#  runs for AJut.UX.WinUI3 rather than AJut.UX. Folders under test/ not named *.Tests (the bench, the
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

        if (-not ($targetProjects -contains $owner)) {
            Write-Host "$testName tests $owner, which is not a target. Skipping."
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
