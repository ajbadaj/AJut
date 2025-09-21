[CmdletBinding()]
param (
    [string]$projectsToBuild = "",
    [string]$prHeadSHA = "",
    [string]$prBaseSHA = ""
)

try {
    # Read and parse the ProjectOrder.json file to get the deterministic build order
    $projectOrder = Get-Content -Raw -Path "ProjectOrder.json" | ConvertFrom-Json
    
    # Determine which projects to build based on input
    $targetProjects = @()
    if ($projectsToBuild) {
        $inputProjects = ConvertFrom-Json $projectsToBuild
        
        # Create a lowercase version of the input object keys for case-insensitive lookup
        $lowerCaseInputs = @{}
        $inputProjects.psobject.Properties | ForEach-Object {
            $lowerCaseInputs.Add($_.Name, $_.Value)
        }
        
        Write-Host "Normalized inputs for lookup:"
        $lowerCaseInputs.GetEnumerator() | ForEach-Object {
            Write-Host "  $($_.Key) = $($_.Value)"
        }

        foreach ($project in $projectOrder) {
            # Normalize the project name to remove dots, matching the YML input parameter name
            $normalizedKey = ("build" + ($project -replace '\.', '')).ToLower()
            Write-Host "Checking: $project via '$normalizedKey'"
            
            if ($lowerCaseInputs.ContainsKey($normalizedKey) -and $lowerCaseInputs[$normalizedKey] -eq $true) {
                if (-not $targetProjects.Contains($project)) {
                    $targetProjects += $project
                }
            }
        }
    } else {
        git fetch origin main

        # We're trying instead to use the diffs to determine what has changed
        if ($prBaseSHA -and $prHeadSHA) {
            Write-Host "Pull Request Diff: comparing $prBaseSHA to $prHeadSHA"
            $changedFiles = git diff --name-only $prBaseSHA $prHeadSHA
            Write-Host "Git diff returned: $changedFiles"

            foreach ($projectName in $projectOrder) {
                $projectPath = "libs/$projectName/$projectName.csproj"
                if ($changedFiles -like "libs/$projectName/*") {
                    if (-not $targetProjects.Contains($projectName)) {
                        $targetProjects += $projectName
                    }
                }
            }
        }
        else {
            Write-Host "Building all"
            $targetProjects = $projectOrder
        }
    }

    Write-Host "Found projects list: $targetProjects"
    if (!$targetProjects) {
        Write-Error "Could not determine any projects to build"
        exit 2
    }

    foreach ($projectName in $targetProjects) {
        $projectPath = "libs/$projectName/$projectName.csproj"

        Write-Host "--> Building $projectName..."
        dotnet build $projectPath --configuration Release
    }

    # Save the list of target projects to a file so it can be used by other scripts
    $targetProjects | ConvertTo-Json | Set-Content "target_projects.json"
    Write-Host "Build process complete."
} catch {
    Write-Error $_.Exception.Message
    exit 1
}