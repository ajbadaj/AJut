[CmdletBinding()]
param (
    [int]$prNumber,
    [string]$prHeadSHA = "",
    [string]$prBaseSHA = ""
)

try {        
    # Determine changed projects
    $projectOrder = Get-Content -Raw -Path "ProjectOrder.json" | ConvertFrom-Json
    $changedProjects = @()

    if ($prBaseSHA -and $prHeadSHA) {
        $changedFiles = git diff --name-only $prBaseSHA $prHeadSHA
        
        foreach ($projectName in $projectOrder) {
            if ($changedFiles -like "libs/$projectName/*") {
                if (-not $changedProjects.Contains($projectName)) {
                    $changedProjects += $projectName
                }
            }
        }
    } else {
        $changedProjects = $projectOrder
    }

    if ($changedProjects.Count -eq 0) {
        Write-Host "No projects changed, skipping version update."
        exit 0
    }

    Write-Host "Projects to update: $changedProjects"

    foreach ($projectName in $changedProjects) {
        $projectPath = "libs/$projectName/$projectName.csproj"
        [xml]$xml = Get-Content -Path $projectPath
        $rootGroup = $xml.Project.PropertyGroup | Select-Object -First 1

        if ($rootGroup) {
            $versionPrefix = $rootGroup.VersionPrefix
            $versionSuffix = $rootGroup.VersionSuffix
            $fullVersion = "${versionPrefix}.${prNumber}"
            if ($versionSuffix) {
                $fullVersion += "-${versionSuffix}"
            }

            $newVersionElement = $xml.CreateElement("Version")
            $newVersionElement.InnerText = $fullVersion
            if ($rootGroup.SelectSingleNode("Version")) {
                $rootGroup.SelectSingleNode("Version").ParentNode.ReplaceChild($newVersionElement, $rootGroup.SelectSingleNode("Version"))
            } else {
                [void]$rootGroup.AppendChild($newVersionElement)
            }
            $xml.Save($projectPath)
            Write-Host " ... Version set successfully to $fullVersion for $projectName"
        } else {
            Write-Error "CSProj is not configured in an expected manner, cannot write Version"
            exit 1
        }
    }
} catch {
    Write-Error $_.Exception.Message
    exit 1
}
