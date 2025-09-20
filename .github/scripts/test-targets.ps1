[CmdletBinding()]
param ()

try {
    Write-Host "Starting test process..."
    
    # Load the list of built projects from the file
    $builtProjects = Get-Content -Raw -Path "built_projects.json" | ConvertFrom-Json
    
    if ($null -eq $builtProjects -or $builtProjects.Count -eq 0) {
        Write-Host "No projects to test. Skipping."
        exit 0
    }
    
    foreach ($projectName in $builtProjects) {
        $testProjectPath = "libs/$projectName/$projectName.Test.csproj"
        if (Test-Path $testProjectPath) {
            Write-Host "--> Running tests for $projectName..."
            dotnet test $testProjectPath --configuration Release
        } else {
            Write-Host "No test project found for $projectName. Skipping tests."
        }
    }
    Write-Host "Test process complete."
} catch {
    Write-Error $_.Exception.Message
    exit 1
}
