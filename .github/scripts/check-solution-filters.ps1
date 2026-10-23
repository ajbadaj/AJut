[CmdletBinding()]
param (
    [string]$solutionPath = "AJut.slnx"
)

# AJut.slnx holds every project, and the solution filters beside it (AJut.Windows.slnf, AJut.Mac.slnf)
#  each pick the projects one kind of machine can build. A filter only lists project paths, so a new
#  project has to be added to a filter by hand, and one that never is gets opened and built by nobody.
#
# This fails (exit 1) when a project in the solution is in no filter, or a filter names a project the
#  solution does not have. A filter project with no file on disk is only reported: a branch that does
#  not carry a toolkit package yet still lists it.

function Get-NormalizedPath ([string]$path) {
    $path.Replace('\', '/')
}

try {
    $solutionDirectory = Split-Path -Parent (Resolve-Path $solutionPath)
    [xml]$solution = Get-Content -Raw -Path $solutionPath
    $solutionProjects = @($solution.SelectNodes("//Project") | ForEach-Object { Get-NormalizedPath $_.Path })

    $filterFiles = @(Get-ChildItem -Path $solutionDirectory -Filter "*.slnf")
    if ($filterFiles.Count -eq 0) {
        Write-Host "No solution filters found beside $solutionPath"
        exit 1
    }

    $failed = $false
    $filtered = @{}
    foreach ($filterFile in $filterFiles) {
        $filter = Get-Content -Raw -Path $filterFile.FullName | ConvertFrom-Json
        foreach ($project in @($filter.solution.projects | ForEach-Object { Get-NormalizedPath $_ })) {
            $filtered[$project] = $true
            if ($solutionProjects -notcontains $project) {
                Write-Host "$($filterFile.Name) names $project, which $solutionPath does not have"
                $failed = $true
            }
            elseif (-not (Test-Path (Join-Path $solutionDirectory $project))) {
                Write-Host "[note] $($filterFile.Name): $project is not on this branch"
            }
        }
    }

    foreach ($project in $solutionProjects) {
        if (-not $filtered.ContainsKey($project)) {
            Write-Host "$project is in $solutionPath but in no solution filter"
            $failed = $true
        }
    }

    if ($failed) {
        exit 1
    }

    Write-Host "Every project in $solutionPath is in a solution filter, and every filter project is in $solutionPath"
    exit 0
}
catch {
    Write-Host "Checking the solution filters failed: $_"
    exit 1
}
