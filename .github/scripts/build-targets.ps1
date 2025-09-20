[CmdletBinding()]
param (
    [int]$prNumber,
    [string]$projectsToBuild = "",
    [string]$prHeadSHA = "",
    [string]$prBaseSHA = ""
)

try {
    Write-Host "Starting build process with PR Number: $prNumber"

    # Read and parse the ProjectOrder.json file to get the deterministic build order
    $projectOrder = Get-Content -Raw -Path "ProjectOrder.json" | ConvertFrom-Json
    
    # Determine which projects to build based on input
    $builtProjects = @()
    if ($projectsToBuild) {
        $inputProjects = ConvertFrom-Json $projectsToBuild
        
        # Create a lowercase version of the input object keys for case-insensitive lookup
        $lowerCaseInputs = @{}
        $inputProjects.psobject.Properties | ForEach-Object {
            $lowerCaseInputs.Add($_.Name.ToLower(), $_.Value)
        }
        
        Write-Host "Normalized inputs for lookup:"
        $lowerCaseInputs.GetEnumerator() | ForEach-Object {
            Write-Host "  $($_.Key) = $($_.Value)"
        }

        foreach ($project in $projectOrder) {
            # Normalize the project name to remove dots, matching the YML input parameter name
            $normalizedKey = ("build" + ($project -replace '\.', '')).ToLower()
            Write-Host "Checking: $project against '$normalizedKey'"
            
            if ($lowerCaseInputs.ContainsKey($normalizedKey) -and $lowerCaseInputs[$normalizedKey] -eq $true) {
                $builtProjects += $project
            }
        }
    } else {
        git fetch origin main

        # We're trying instead to use the diffs to determine what has changed
        if ($prBaseSHA -and $prHeadSHA) {
            Write-Host "Pull Request Diff: comparing $prBaseSHA to $prHeadSHA"
            $changedFiles = git diff --name-only $prBaseSHA $prHeadSHA
            Write-Host "Git diff returned: $changedFiles"
        }
        else {
            Write-Host "Pull Request Diff: comparing this branch to head"
            $changedFiles = git diff --name-only origin/main HEAD
        }

        foreach ($projectName in $projectOrder) {
            $projectPath = "libs/$projectName/$projectName.csproj"
            if ($changedFiles -like "libs/$projectName/*") {
                $builtProjects += $projectName
            }
        }
    }

    Write-Host "Found projects list: $builtProjects"
    if (!$builtProjects) {
        Write-Error "Could not determine any projects to build"
        exit 2
    }

    foreach ($projectName in $builtProjects) {
        $projectPath = "libs/$projectName/$projectName.csproj"
        
        Write-Host "--> Building $projectName..."

        $csproj = ([xml](Get-Content $projectPath)).Project.PropertyGroup
        $versionPrefix = $csproj.VersionPrefix
        $versionSuffix = $csproj.VersionSuffix
        
        if ($versionSuffix) {
            $fullVersion = "${versionPrefix}.${prNumber}-${versionSuffix}"
        } else {
            $fullVersion = "${versionPrefix}.${prNumber}"
        }

        Write-Host "Building with full version: $fullVersion"
        dotnet build $projectPath --configuration Release /p:Version=$fullVersion
    }

    # Save the list of built projects to a file so it can be used by other scripts
    $builtProjects | ConvertTo-Json | Set-Content "built_projects.json"
    Write-Host "Build process complete."
} catch {
    Write-Error $_.Exception.Message
    exit 1
}
