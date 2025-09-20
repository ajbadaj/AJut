[CmdletBinding()]
param (
    [int]$prNumber,
    [string]$apiKey
)

try {
    Write-Host "Starting publish process..."
    
    # Load the list of built projects from the file created by build command
    # Note: This list is already deterministically sorted from the ProjectOrder.json so we don't need to re-enforce this order for publishing
    $builtProjects = Get-Content -Raw -Path "built_projects.json" | ConvertFrom-Json
    
    if ($null -eq $builtProjects -or $builtProjects.Count -eq 0) {
        Write-Host "No projects to publish. Skipping."
        exit 0
    }

    foreach ($projectName in $builtProjects) {
        $projectPath = "libs/$projectName/$projectName.csproj"
        
        Write-Host "--> Publishing $projectName..."
        
        # Crucially we're trying to build the final version number here by combining:
        #   VersionPrefix.PR#-VersionSuffix(if any)
        #
        # So a project with VersionPrefix set to 1.2.5 and no Version Suffix published for PR# 86 would give us:
        #   1.2.5.86
        #
        # But a project with VersionPrefix set to 2.0.5 and a VersionSuffix of beta would give us:
        #   2.0.5.86-beta


        $csproj = ([xml](Get-Content $projectPath)).Project.PropertyGroup
        $versionPrefix = $csproj.VersionPrefix
        $versionSuffix = $csproj.VersionSuffix
        
        if ($versionSuffix) {
            $fullVersion = "${versionPrefix}.${prNumber}-${versionSuffix}"
        } else {
            $fullVersion = "${versionPrefix}.${prNumber}"
        }

        $nugetPackage = Get-ChildItem -Path "libs/$projectName/bin/Release" -Filter "$projectName.${fullVersion}.nupkg" | Select-Object -First 1
        
        if (-not $nugetPackage) {
            Write-Error "Could not find the .nupkg file for $projectName version $fullVersion."
            exit 1
        }
        
        Write-Host "Found Nuget package: $($nugetPackage.FullName)"
        Write-Host "Publishing..."
        # For testing, uncomment this and comment out the next one
        #Write-Host "Would be doing → dotnet nuget push ""$($nugetPackage.FullName)"" --api-key ""$($apiKey)"" --source https://api.nuget.org/v3/index.json"
        dotnet nuget push "$($nugetPackage.FullName)" --api-key $apiKey --source https://api.nuget.org/v3/index.json
        Write-Host "Publish complete for $projectName!"
    }
    Write-Host "Publish process complete."
} catch {
    Write-Error $_.Exception.Message
    exit 1
}
