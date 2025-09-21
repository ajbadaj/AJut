[CmdletBinding()]
param (
    [string]$apiKey,
    [string]$publishTargets = "",
    [switch]$dryRun
)

try {
    Write-Host "Starting publish process..."
    
    if ($publishTargets -eq "") {
        $publishTargets = Get-Content -Raw -Path "target_projects.json"
        if ($publishTargets -eq "") {
            $publishTargets = Get-Content -Raw -Path "ProjectOrder.json"
        }
    }

    # Load the list of built projects from the file created by build command
    $targetProjects = ConvertFrom-Json $publishTargets
    
    if ($null -eq $targetProjects -or $targetProjects.Count -eq 0) {
        Write-Host "No projects to publish. Skipping."
        exit 0
    }

    foreach ($projectName in $targetProjects) {
        $projectPath = "libs/$projectName/$projectName.csproj"
        
        Write-Host "=========================================================================================================="
        Write-Host "--> Publishing $projectName..."
        
        # Read the version directly from the project file
        $csproj = ([xml](Get-Content $projectPath)).Project.PropertyGroup
        $fullVersion = $csproj.Version

        if (-not $fullVersion) {
            Write-Error "Could not find a <Version> element in $projectPath. Skipping publish."
            continue
        }

        $nugetPackage = Get-ChildItem -Path "libs/$projectName/bin/Release" -Filter "$projectName.$fullVersion.nupkg" | Select-Object -First 1
        
        if (-not $nugetPackage) {
            Write-Error "Could not find the .nupkg file for $projectName version $fullVersion."
            exit 1
        }
        
        Write-Host "Found Nuget package: $($nugetPackage.FullName)"
        Write-Host "----------------------------------------------------------------------------------------------------------"
        if ($dryRun -eq $true) {
           Write-Host "DRY RUN: The following command would be executed:" -ForegroundColor Yellow
           Write-Host "dotnet nuget push `"$($nugetPackage.FullName)`" --api-key API_KEY --source https://api.nuget.org/v3/index.json" -ForegroundColor Cyan
        } else {
          Write-Host "Publishing..."
          dotnet nuget push "$($nugetPackage.FullName)" --api-key $apiKey --source https://api.nuget.org/v3/index.json
          Write-Host "Publish complete for $projectName"
        }
        Write-Host "--------------------------------------------"
    }
    Write-Host "Publish process complete."
} catch {
    Write-Error $_.Exception.Message
    exit 1
}