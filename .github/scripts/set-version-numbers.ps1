[CmdletBinding()]
param (
    [int]$prNumber,
    [string]$prHeadSHA = "",
    [string]$prBaseSHA = "",
    [string]$targetProjects = "",
    [switch]$dryRun
)

try {        
    # Determine changed projects
    $projectOrder = Get-Content -Raw -Path "ProjectOrder.json" | ConvertFrom-Json
    $changedProjects = @()

    if ($prBaseSHA -and $prHeadSHA) {
        Write-Host "Attempting to diff the provided SHAs to see if we can automatically determine changes via:"
        Write-Host "git diff --name-only `"$($prBaseSHA)`" `"$($prHeadSHA)`"" -ForegroundColor Cyan
        $changedFiles = git diff --name-only $prBaseSHA $prHeadSHA
        Write-Host "Searching: $changedFiles"
        
        foreach ($projectName in $projectOrder) {
            if ($changedFiles -like "libs/$projectName/*") {
                if (-not $changedProjects.Contains($projectName)) {
                    $changedProjects += $projectName
                }
            }
        }
    } else {
        if ($targetProjects -eq "") {
            $changedProjects = Get-Content -Raw -Path "target_projects.json"
        } else {
            $changedProjects = ConvertFrom-Json $targetProjects
        }
    }

    if ($changedProjects.Count -eq 0) {
        Write-Host "No projects changed, skipping version update."
        return $changedProjects
    }

    Write-Host "Found projects to update: $changedProjects"

    foreach ($projectName in $changedProjects) {
        $projectPath = "libs/$projectName/$projectName.csproj"
        [xml]$xml = Get-Content -Path $projectPath
        $rootGroup = $xml.Project.PropertyGroup | Select-Object -First 1

        if ($rootGroup) {
            $versionPrefix = $rootGroup.VersionPrefix
            $versionSuffix = $rootGroup.VersionSuffix
            
            if ($versionSuffix) {
                $fullVersion = "${versionPrefix}.${prNumber}-${versionSuffix}"
            } else {
                $fullVersion = "${versionPrefix}.${prNumber}"
            }
        } else {
            Write-Error "CSProj is not configured in an expected manner, cannot write Version"
            exit 1
        }

        if ($dryRun -eq $true) {
            Write-Host " ... Version would be set to $fullVersion for $projectName"
        } else {
            $newVersionElement = $xml.CreateElement("Version")
            $newVersionElement.InnerText = $fullVersion
            if ($rootGroup.SelectSingleNode("Version")) {
                $rootGroup.SelectSingleNode("Version").ParentNode.ReplaceChild($newVersionElement, $rootGroup.SelectSingleNode("Version"))
            } else {
                [void]$rootGroup.AppendChild($newVersionElement)
            }
            $xml.Save($projectPath)
            Write-Host " ... Version set successfully to $fullVersion for $projectName"
        }
    }

    return $changedProjects
} catch {
    Write-Error $_.Exception.Message
    exit 1
}
