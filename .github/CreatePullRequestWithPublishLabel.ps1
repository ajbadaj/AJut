# This script automates the process of pushing changes, creating a pull request, and adding the 'publish-nuget' label we use to signal that when the pr is merged, it should merge and publish to nuget.
# It requires the GitHub CLI ('gh') to be installed and authenticated.
# You can download 'gh' from: https://cli.github.com/

# --- Variables to configure ---
# The name of the label to add to the pull request.
$LabelName = "publish-nuget"
# The base branch to create the pull request against.
$BaseBranch = "main"

# --- Script starts here ---
try {
    # Check if the GitHub CLI is installed.
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "GitHub CLI ('gh') is not installed. Please install it to use this script."
    }

    Write-Host "Starting the automated pull request workflow..."

    # Get the current branch name
    $CurrentBranch = git rev-parse --abbrev-ref HEAD
    if ($CurrentBranch -eq $BaseBranch) {
        throw "You are on the '$BaseBranch' branch. Please switch to a feature branch before running this script."
    }

    # Push changes to the current branch
    Write-Host "Pushing changes to '$CurrentBranch'..."
    gh push origin $CurrentBranch

    # Create the pull request
    Write-Host "Creating pull request from '$CurrentBranch' to '$BaseBranch'..."
    $prResult = gh pr create --base $BaseBranch --head $CurrentBranch --fill --label $LabelName --json number,url,title

    if ($prResult) {
        $prData = $prResult | ConvertFrom-Json
        $prNumber = $prData.number
        $prUrl = $prData.url
        $prTitle = $prData.title

        Write-Host "Successfully created pull request #$prNumber:"
        Write-Host "Title: $prTitle"
        Write-Host "URL: $prUrl"
        Write-Host "Label '$LabelName' has been added."
    } else {
        throw "Failed to create pull request. Please check for errors above."
    }

} catch {
    Write-Error $_.Exception.Message
    Write-Host "The script has stopped due to an error."
    exit 1
}

