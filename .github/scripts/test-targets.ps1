[CmdletBinding()]
param (
    [string]$testTargets = ""
)

try {
    Write-Host "Starting test process..."
    
    # If test targets was unset, we're testing them all
    if ($testTargets -eq "") {
        $testTargets = Get-Content -Raw -Path "ProjectOrder.json"
    }

    $targetProjects = ConvertFrom-Json $testTargets

    if ($null -eq $targetProjects -or $targetProjects.Count -eq 0) {
        Write-Host "No projects to test. Skipping."
        exit 0
    }
    
    foreach ($projectName in $targetProjects) {
        $testProjectPath = "libs/$projectName/$projectName.Test.csproj"
        if (Test-Path $testProjectPath) {
            Write-Host "--> Running tests for $projectName..."
            dotnet test --no-build --verbosity normal $testProjectPath --configuration Release
        } else {
            Write-Host "No test project found for $projectName. Skipping tests."
        }
    }
    Write-Host "Test process complete."
} catch {
    Write-Error $_.Exception.Message
    exit 1
}
