[CmdletBinding(SupportsShouldProcess)]
param (
    [string]$testProject = "test/AJut.UX.WinUI3.Tests/AJut.UX.WinUI3.Tests.csproj",
    [string]$architecture = "x64"
)

# Installs the Windows App Runtime the WinUI3 tests run against. The test project is unpackaged and not
#  self-contained, so the runtime has to be installed on the machine. A machine with the Windows App SDK tooling
#  usually has it; a CI runner doesn't.
#
# It installs from the Microsoft.WindowsAppSDK.Runtime NuGet package the test project restores, which carries the
#  runtime's MSIX packages (the framework, DDLM, Main and Singleton, the same four the WindowsAppRuntimeInstall
#  installer adds). So what gets installed is always the version the tests build against, and there is no download
#  link to keep in step with the package version. A package already installed at that version or newer is left alone.
#
# This is meant for CI: it installs packages on the machine, which a test run on a dev machine shouldn't do as a side
#  effect. -WhatIf shows what it would install without installing anything. It runs in Windows PowerShell, since
#  Add-AppxPackage doesn't reliably load in PowerShell 7.

$ErrorActionPreference = "Stop"

try {
    if ($PSVersionTable.PSEdition -ne "Desktop") {
        Write-Error "Run this in Windows PowerShell (powershell.exe): Add-AppxPackage doesn't reliably load in PowerShell 7"
        exit 1
    }

    # 1. Restore, so the runtime package is in a package folder and the assets file says where
    Write-Host "--> Restoring $testProject..."
    dotnet restore $testProject
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Restore failed: $testProject"
        exit 1
    }

    $assetsFile = (dotnet msbuild $testProject -getProperty:ProjectAssetsFile | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $assetsFile)) {
        Write-Error "Couldn't find the assets file for $testProject (got '$assetsFile')"
        exit 1
    }

    # 2. Find Microsoft.WindowsAppSDK.Runtime in it, and which package folder it was restored to
    $assets = Get-Content -Raw -Path $assetsFile | ConvertFrom-Json
    $runtimeLibrary = @($assets.libraries.PSObject.Properties | Where-Object { $_.Name -like "Microsoft.WindowsAppSDK.Runtime/*" }) | Select-Object -First 1
    if (-not $runtimeLibrary) {
        Write-Error "$testProject doesn't reference Microsoft.WindowsAppSDK.Runtime, so there is no runtime to install"
        exit 1
    }

    $runtimePackage = @(
        $assets.packageFolders.PSObject.Properties |
            ForEach-Object { Join-Path $_.Name $runtimeLibrary.Value.path } |
            Where-Object { Test-Path $_ }
    ) | Select-Object -First 1
    if (-not $runtimePackage) {
        Write-Error "$($runtimeLibrary.Name) isn't in any of the package folders $assetsFile lists"
        exit 1
    }

    $msixFolder = Join-Path $runtimePackage "tools/MSIX/win10-$architecture"
    $msixFiles = @(Get-ChildItem -Path $msixFolder -Filter "*.msix" -ErrorAction SilentlyContinue)
    if ($msixFiles.Count -eq 0) {
        Write-Error "$($runtimeLibrary.Name) has no MSIX packages for $architecture in $msixFolder"
        exit 1
    }

    Write-Host "--> Installing the Windows App Runtime from $($runtimeLibrary.Name) ($architecture)..."

    # 3. Each package's identity comes from its own manifest. The package's MSIX.inventory names them too, but not
    #  always as they install: in 2.6.4 it lists the DDLM as Microsoft.WindowsAppRuntime.DDLM.2, which installs as
    #  Microsoft.WinAppRuntime.DDLM.2.6.4.0-x6, and the Singleton as 2.6.4.0, which installs as 8002.6.4.0. Checked
    #  against the inventory, an installed DDLM is never found and a Singleton that needs updating is skipped. The
    #  framework goes first, since the other three depend on it.
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $entries = @(
        $msixFiles |
            ForEach-Object {
                $msix = [System.IO.Compression.ZipFile]::OpenRead($_.FullName)
                try {
                    $reader = New-Object System.IO.StreamReader($msix.GetEntry("AppxManifest.xml").Open())
                    try {
                        [xml]$manifest = $reader.ReadToEnd()
                    } finally {
                        $reader.Dispose()
                    }
                } finally {
                    $msix.Dispose()
                }

                [PSCustomObject]@{
                    File = $_.Name
                    Name = $manifest.Package.Identity.Name
                    Version = [version]$manifest.Package.Identity.Version
                    Architecture = $manifest.Package.Identity.ProcessorArchitecture
                    IsFramework = ($manifest.Package.Properties.Framework -eq "true")
                }
            } |
            Sort-Object -Property @{ Expression = "IsFramework"; Descending = $true }
    )

    foreach ($entry in $entries) {
        $installed = @(
            Get-AppxPackage -Name $entry.Name |
                Where-Object { ($_.Architecture -eq $entry.Architecture) -and ([version]$_.Version -ge $entry.Version) }
        )
        if ($installed.Count -gt 0) {
            Write-Host "$($entry.Name) $($installed[0].Version) is already installed. Skipping."
            continue
        }

        $msixFile = Join-Path $msixFolder $entry.File
        if ($PSCmdlet.ShouldProcess($msixFile, "Add-AppxPackage")) {
            Write-Host "Installing $($entry.Name) $($entry.Version)..."
            Add-AppxPackage -Path $msixFile
        }
    }

    # 4. What is installed now of these four, so a run that still can't find the runtime shows what it had to work with
    $entryNames = @($entries | ForEach-Object { $_.Name })
    Get-AppxPackage |
        Where-Object { $entryNames -contains $_.Name } |
        Sort-Object -Property Name, Version |
        Format-Table -Property Name, Version, Architecture -AutoSize |
        Out-String |
        Write-Host

    Write-Host "Windows App Runtime install complete."
} catch {
    Write-Error $_.Exception.Message
    exit 1
}
