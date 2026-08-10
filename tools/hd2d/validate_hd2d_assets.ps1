param(
    [string]$WorkspaceRoot = "C:\work\CoffeeGAME"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$artRoot = Join-Path $WorkspaceRoot "art\hd2d\frames"
$resourcesRoot = Join-Path $WorkspaceRoot "unity\CoffeeGame\Assets\CoffeeGame\Resources"
$manifestRoot = Join-Path $resourcesRoot "Art\HD2D"

$contracts = @(
    @{
        Name = "Hero"
        Art = Join-Path $artRoot "hero"
        Unity = Join-Path $manifestRoot "Hero\Frames"
        Width = 768
        Height = 768
        Count = 50
        Manifest = Join-Path $manifestRoot "hero-hd2d.json"
        RequiredActions = 15
        GroundY = 720
        GroundTolerance = 2
        GroundedFrames = @(
            "hero_run_down_a_v3.png",
            "hero_run_down_b_v3.png",
            "hero_run_right_a_v3.png",
            "hero_run_right_b_v3.png",
            "hero_run_up_a_v3.png",
            "hero_run_up_b_v3.png"
        )
    },
    @{
        Name = "Slime"
        Art = Join-Path $artRoot "slime"
        Unity = Join-Path $manifestRoot "Slime\Frames"
        Width = 512
        Height = 512
        Count = 6
        Manifest = Join-Path $manifestRoot "slime-hd2d.json"
        RequiredActions = 7
    }
)

function Assert-Condition {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

foreach ($contract in $contracts) {
    $artFiles = @(Get-ChildItem -LiteralPath $contract.Art -Filter "*.png" | Sort-Object Name)
    $unityFiles = @(Get-ChildItem -LiteralPath $contract.Unity -Filter "*.png" | Sort-Object Name)
    Assert-Condition ($artFiles.Count -eq $contract.Count) `
        "$($contract.Name) art frame count is $($artFiles.Count), expected $($contract.Count)."
    Assert-Condition ($unityFiles.Count -eq $contract.Count) `
        "$($contract.Name) Unity frame count is $($unityFiles.Count), expected $($contract.Count)."

    foreach ($artFile in $artFiles) {
        $unityPath = Join-Path $contract.Unity $artFile.Name
        Assert-Condition (Test-Path -LiteralPath $unityPath) `
            "$($contract.Name) Unity frame is missing: $($artFile.Name)"
        $artHash = (Get-FileHash -LiteralPath $artFile.FullName -Algorithm SHA256).Hash
        $unityHash = (Get-FileHash -LiteralPath $unityPath -Algorithm SHA256).Hash
        Assert-Condition ($artHash -eq $unityHash) `
            "$($contract.Name) frame differs between art and Unity: $($artFile.Name)"

        $bitmap = [System.Drawing.Bitmap]::FromFile($artFile.FullName)
        try {
            $dimensionMessage = "$($contract.Name) frame has invalid dimensions: " +
                "$($artFile.Name) ($($bitmap.Width)x$($bitmap.Height))"
            Assert-Condition (
                $bitmap.Width -eq $contract.Width -and $bitmap.Height -eq $contract.Height) `
                $dimensionMessage
        }
        finally {
            $bitmap.Dispose()
        }
    }

    if ($contract.ContainsKey("GroundedFrames")) {
        foreach ($frameName in $contract.GroundedFrames) {
            $framePath = Join-Path $contract.Art $frameName
            $bitmap = [System.Drawing.Bitmap]::FromFile($framePath)
            try {
                $maxOpaqueY = -1
                for ($y = 0; $y -lt $bitmap.Height; $y++) {
                    for ($x = 0; $x -lt $bitmap.Width; $x++) {
                        if ($bitmap.GetPixel($x, $y).A -gt 16) {
                            $maxOpaqueY = $y
                        }
                    }
                }

                $contactDelta = [Math]::Abs($maxOpaqueY - $contract.GroundY)
                Assert-Condition ($contactDelta -le $contract.GroundTolerance) `
                    "$($contract.Name) grounded frame misses y=$($contract.GroundY): $frameName ends at y=$maxOpaqueY."
            }
            finally {
                $bitmap.Dispose()
            }
        }
    }

    $manifest = Get-Content -LiteralPath $contract.Manifest -Encoding utf8 -Raw | ConvertFrom-Json
    $required = @($manifest.requiredActions)
    $actions = @($manifest.clips | ForEach-Object action)
    Assert-Condition ($required.Count -eq $contract.RequiredActions) `
        "$($contract.Name) required action count is $($required.Count), expected $($contract.RequiredActions)."
    Assert-Condition (($actions | Select-Object -Unique).Count -eq $actions.Count) `
        "$($contract.Name) manifest contains duplicate actions."
    foreach ($requiredAction in $required) {
        Assert-Condition ($actions -contains $requiredAction) `
            "$($contract.Name) manifest is missing required action: $requiredAction"
    }

    foreach ($clip in $manifest.clips) {
        foreach ($direction in @("all", "down", "side", "up")) {
            $strip = $clip.$direction
            if ($null -eq $strip) { continue }
            foreach ($resourcePath in @($strip.resourcePaths)) {
                if ([string]::IsNullOrWhiteSpace($resourcePath)) { continue }
                $assetPath = Join-Path $resourcesRoot (($resourcePath -replace '/', '\') + ".png")
                Assert-Condition (Test-Path -LiteralPath $assetPath) `
                    "$($contract.Name) manifest resource is missing: $resourcePath"
            }
        }
    }

    Write-Host "$($contract.Name): $($artFiles.Count) frames, $($required.Count) required actions, contract OK."
}

Write-Host "HD-2D asset validation passed without starting Unity."
