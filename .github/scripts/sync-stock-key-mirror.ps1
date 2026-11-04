[CmdletBinding()]
param (
    [switch]$check
)

# Rewrites the stock key block of AJut.UX's AJutThemeKeys.cs from AJut.UX.WinUI3's SystemColorAliasing.xaml.
#
# SystemColorAliasing.xaml is where the AJut theme maps WinUI's stock keys (ButtonBackground, CheckBoxForegroundChecked
#  and the rest) onto the AJut palette, and it stays the source of truth. AJutThemeKeys.cs carries the same mappings for
#  the stacks with no XAML (AppKit, UIKit, Android), in the same order, in the block that starts at the
#  "// ------ SystemColorAliasing.xaml" line. Change the XAML, run this from the repo root, and the C# follows:
#      pwsh .github/scripts/sync-stock-key-mirror.ps1
#  With -check it writes nothing and fails (exit 1) when the C# is out of step, which is what AJut.UX.Tests'
#  AJutPaletteTests also catch, key by key.
#
# Each top-level entry becomes one line:
#      <StaticResource x:Key="K" ResourceKey="X"/>                   { "K", Alias("X") },
#      <SolidColorBrush x:Key="K" Color="{ThemeResource X}"/>        { "K", Alias("X") },
#      <SolidColorBrush x:Key="K" Color="#AARRGGBB"/>                { "K", Solid(0xAARRGGBB) },
#      <Color x:Key="K">#AARRGGBB</Color>                            { "K", Solid(0xAARRGGBB) },
#  A color of Transparent is 0x00FFFFFF, as XAML reads it, and an Opacity other than 1 is passed on as the second
#  argument. A CornerRadius is skipped, since the palette tables carry colors and numbers only, and anything else stops
#  the script so it can be taught the new form.

$xamlPath = "libs/AJut.UX.WinUI3/Resources/SystemColorAliasing.xaml"
$mirrorPath = "libs/AJut.UX/Theming/AJutThemeKeys.cs"
$blockMarker = "// ------ SystemColorAliasing.xaml"
$entryIndent = "                "
$xamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml"
$markupReference = [regex]'^\{(?:ThemeResource|StaticResource)\s+([A-Za-z0-9_]+)\s*\}$'

# 0xAARRGGBB from a XAML color: #RGB, #ARGB, #RRGGBB, #AARRGGBB or Transparent, which XAML reads as #00FFFFFF
function ConvertTo-ArgbLiteral ([string]$text) {
    $text = $text.Trim()
    if ($text -eq "Transparent") {
        return "0x00FFFFFF"
    }

    $hex = $text.TrimStart('#')
    switch ($hex.Length) {
        3 { $hex = "FF" + (($hex.ToCharArray() | ForEach-Object { "$_$_" }) -join "") }
        4 { $hex = ($hex.ToCharArray() | ForEach-Object { "$_$_" }) -join "" }
        6 { $hex = "FF" + $hex }
        8 { }
        default { throw "Can't read the color '$text'" }
    }

    return "0x" + $hex.ToUpperInvariant()
}

# The second argument for a brush opacity, when it isn't 1
function Get-OpacitySuffix ([System.Xml.XmlElement]$element) {
    $opacity = $element.GetAttribute("Opacity")
    if ([string]::IsNullOrEmpty($opacity) -or [double]::Parse($opacity, [Globalization.CultureInfo]::InvariantCulture) -eq 1.0) {
        return ""
    }

    return ", " + $opacity
}

try {
    # 1. One C# line per entry, in the XAML's order
    $doc = New-Object System.Xml.XmlDocument
    $doc.Load((Resolve-Path $xamlPath))
    $lines = New-Object System.Collections.Generic.List[string]
    foreach ($element in $doc.DocumentElement.ChildNodes) {
        # A continue inside the switch below would only leave the switch, so the skips happen here
        if (($element.NodeType -ne [System.Xml.XmlNodeType]::Element) -or ($element.LocalName -eq "CornerRadius")) {
            continue
        }

        $key = $element.GetAttribute("Key", $xamlNamespace)
        switch ($element.LocalName) {
            "StaticResource" {
                $entry = "Alias(""$($element.GetAttribute("ResourceKey"))"")"
            }
            "SolidColorBrush" {
                $color = $element.GetAttribute("Color")
                $reference = $markupReference.Match($color)
                $entry = if ($reference.Success) {
                    "Alias(""$($reference.Groups[1].Value)""$(Get-OpacitySuffix $element))"
                }
                else {
                    "Solid($(ConvertTo-ArgbLiteral $color)$(Get-OpacitySuffix $element))"
                }
            }
            "Color" {
                $entry = "Solid($(ConvertTo-ArgbLiteral $element.InnerText))"
            }
            default {
                throw "$key is a <$($element.LocalName)>, which this script doesn't know how to mirror yet"
            }
        }

        $lines.Add("$entryIndent{ ""$key"", $entry },")
    }

    # 2. Swap the block: from the line after the marker to the line before the dictionary's closing brace
    $text = [System.IO.File]::ReadAllText((Resolve-Path $mirrorPath))
    $newline = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $mirror = [System.Collections.Generic.List[string]]::new([string[]]($text -split "`r?`n"))
    $start = $mirror.FindIndex([Predicate[string]] { param($line) $line.Trim() -eq $blockMarker })
    if ($start -lt 0) {
        throw "Couldn't find '$blockMarker' in $mirrorPath"
    }

    $end = $mirror.FindIndex($start, [Predicate[string]] { param($line) $line.Trim() -eq "};" })
    $current = $mirror.GetRange($start + 1, $end - $start - 1)
    if ([string]::Join("`n", $current) -eq [string]::Join("`n", $lines)) {
        Write-Host "$mirrorPath is in step with $xamlPath ($($lines.Count) entries)"
        exit 0
    }

    if ($check) {
        Write-Host "$mirrorPath is out of step with $xamlPath; run .github/scripts/sync-stock-key-mirror.ps1"
        exit 1
    }

    $mirror.RemoveRange($start + 1, $end - $start - 1)
    $mirror.InsertRange($start + 1, $lines)
    [System.IO.File]::WriteAllText((Resolve-Path $mirrorPath), [string]::Join($newline, $mirror), (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "Rewrote the stock key block of $mirrorPath ($($lines.Count) entries)"
    exit 0
}
catch {
    Write-Host $_
    exit 1
}
