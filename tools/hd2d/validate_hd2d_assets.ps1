param(
    [string]$WorkspaceRoot = "C:\work\CoffeeGAME"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public sealed class CoffeeGameHd2dOpaqueMetrics
{
    public int MinX;
    public int MinY;
    public int MaxX;
    public int MaxY;
    public int Width;
    public int Height;
    public int TopOpaqueWidth;
}

public static class CoffeeGameHd2dAlphaInspector
{
    public static CoffeeGameHd2dOpaqueMetrics Analyze(string path, byte alphaThreshold)
    {
        using (var source = new Bitmap(path))
        using (var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
        {
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.DrawImageUnscaled(source, 0, 0);
            }

            var bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = Math.Abs(data.Stride);
                var pixels = new byte[stride * bitmap.Height];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                int minX = bitmap.Width;
                int minY = bitmap.Height;
                int maxX = -1;
                int maxY = -1;
                for (int y = 0; y < bitmap.Height; y++)
                {
                    int storageY = data.Stride < 0 ? bitmap.Height - 1 - y : y;
                    int row = storageY * stride;
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        if (pixels[row + (x * 4) + 3] <= alphaThreshold) continue;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }

                int topOpaqueWidth = 0;
                if (maxY >= minY)
                {
                    int storageY = data.Stride < 0 ? bitmap.Height - 1 - minY : minY;
                    int row = storageY * stride;
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        if (pixels[row + (x * 4) + 3] > alphaThreshold) topOpaqueWidth++;
                    }
                }

                return new CoffeeGameHd2dOpaqueMetrics
                {
                    MinX = minX,
                    MinY = minY,
                    MaxX = maxX,
                    MaxY = maxY,
                    Width = maxX - minX + 1,
                    Height = maxY - minY + 1,
                    TopOpaqueWidth = topOpaqueWidth
                };
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
    }
}
"@ -ReferencedAssemblies System.Drawing

$artRoot = Join-Path $WorkspaceRoot "art\hd2d\frames"
$resourcesRoot = Join-Path $WorkspaceRoot "unity\CoffeeGame\Assets\CoffeeGame\Resources"
$manifestRoot = Join-Path $resourcesRoot "Art\HD2D"

$contracts = @(
    @{
        Name = "Hero"
        Art = Join-Path $artRoot "hero"
        Unity = Join-Path $manifestRoot "Hero\Frames"
        AtlasArt = Join-Path $WorkspaceRoot "art\hd2d\atlases\hero"
        UnityAtlases = Join-Path $manifestRoot "Hero\Atlases"
        Width = 768
        Height = 768
        Count = 220
        UnityFrameCount = 140
        AtlasCount = 15
        Manifest = Join-Path $manifestRoot "hero-hd2d.json"
        RequiredActions = 15
        GroundY = 720
        GroundTolerance = 4
        GeneratedFramePatterns = @(
            "hero_walk_*_v4.png",
            "hero_run_*_v4.png",
            "hero_sword_*_v4.png",
            "hero_magic_charge_*_v4.png",
            "hero_magic_release_*_v4.png",
            "hero_walk_*_v5.png",
            "hero_run_*_v5.png",
            "hero_jump_*_v5.png"
        )
        GeneratedFrameCount = 170
        GroundedFramePatterns = @(
            "hero_walk_*_v4.png",
            "hero_run_*_v4.png",
            "hero_sword_*_v4.png",
            "hero_walk_*_v5.png",
            "hero_run_*_v5.png"
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

function Get-OpaqueMetrics {
    param([Parameter(Mandatory)] [string]$Path, [byte]$AlphaThreshold = 16)

    $metrics = [CoffeeGameHd2dAlphaInspector]::Analyze($Path, $AlphaThreshold)
    Assert-Condition ($metrics.MaxX -ge $metrics.MinX -and $metrics.MaxY -ge $metrics.MinY) `
        "Frame contains no visible alpha: $Path"
    return $metrics
}

foreach ($contract in $contracts) {
    $artFiles = @(Get-ChildItem -LiteralPath $contract.Art -Filter "*.png" | Sort-Object Name)
    $unityFiles = @(Get-ChildItem -LiteralPath $contract.Unity -Filter "*.png" | Sort-Object Name)
    Assert-Condition ($artFiles.Count -eq $contract.Count) `
        "$($contract.Name) art frame count is $($artFiles.Count), expected $($contract.Count)."
    $expectedUnityFrameCount = if ($contract.ContainsKey("UnityFrameCount")) {
        $contract.UnityFrameCount
    } else {
        $contract.Count
    }
    Assert-Condition ($unityFiles.Count -eq $expectedUnityFrameCount) `
        "$($contract.Name) Unity frame count is $($unityFiles.Count), expected $expectedUnityFrameCount."

    foreach ($artFile in $artFiles) {
        if ($artFile.Name -notmatch '_v5\.png$') {
            $unityPath = Join-Path $contract.Unity $artFile.Name
            Assert-Condition (Test-Path -LiteralPath $unityPath) `
                "$($contract.Name) Unity frame is missing: $($artFile.Name)"
            $artHash = (Get-FileHash -LiteralPath $artFile.FullName -Algorithm SHA256).Hash
            $unityHash = (Get-FileHash -LiteralPath $unityPath -Algorithm SHA256).Hash
            Assert-Condition ($artHash -eq $unityHash) `
                "$($contract.Name) frame differs between art and Unity: $($artFile.Name)"
        }

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

    if ($contract.ContainsKey("AtlasCount")) {
        $artAtlases = @(Get-ChildItem -LiteralPath $contract.AtlasArt -Filter "*.png" | Sort-Object Name)
        $unityAtlases = @(Get-ChildItem -LiteralPath $contract.UnityAtlases -Filter "*.png" | Sort-Object Name)
        Assert-Condition ($artAtlases.Count -eq $contract.AtlasCount) `
            "$($contract.Name) atlas count is $($artAtlases.Count), expected $($contract.AtlasCount)."
        Assert-Condition ($unityAtlases.Count -eq $contract.AtlasCount) `
            "$($contract.Name) Unity atlas count is $($unityAtlases.Count), expected $($contract.AtlasCount)."
        foreach ($atlas in $artAtlases) {
            $unityAtlasPath = Join-Path $contract.UnityAtlases $atlas.Name
            Assert-Condition (Test-Path -LiteralPath $unityAtlasPath) `
                "$($contract.Name) Unity atlas is missing: $($atlas.Name)"
            Assert-Condition (
                (Get-FileHash -LiteralPath $atlas.FullName -Algorithm SHA256).Hash -eq
                (Get-FileHash -LiteralPath $unityAtlasPath -Algorithm SHA256).Hash) `
                "$($contract.Name) atlas differs between art and Unity: $($atlas.Name)"
            $bitmap = [System.Drawing.Bitmap]::FromFile($atlas.FullName)
            try {
                $isJumpAtlas = $atlas.Name.StartsWith("hero_jump_", [System.StringComparison]::OrdinalIgnoreCase)
                $expectedWidth = if ($isJumpAtlas) { 1536 } else { 2304 }
                $expectedHeight = 1536
                Assert-Condition ($bitmap.Width -eq $expectedWidth -and $bitmap.Height -eq $expectedHeight) `
                    "$($contract.Name) atlas has invalid dimensions: $($atlas.Name) ($($bitmap.Width)x$($bitmap.Height))"
            }
            finally {
                $bitmap.Dispose()
            }
        }
    }

    if ($contract.ContainsKey("GeneratedFramePatterns")) {
        $generatedFrames = @(
            foreach ($pattern in $contract.GeneratedFramePatterns) {
                Get-ChildItem -LiteralPath $contract.Art -Filter $pattern
            }
        ) | Sort-Object Name -Unique
        Assert-Condition ($generatedFrames.Count -eq $contract.GeneratedFrameCount) `
            "$($contract.Name) generated frame count is $($generatedFrames.Count), expected $($contract.GeneratedFrameCount)."
        foreach ($frame in $generatedFrames) {
            $metrics = Get-OpaqueMetrics -Path $frame.FullName
            Assert-Condition ($metrics.MinX -ge 16 -and $metrics.MaxX -le 751) `
                "$($contract.Name) generated frame exceeds the 16px horizontal safe area: $($frame.Name) spans x=$($metrics.MinX)..$($metrics.MaxX)."
            Assert-Condition ($metrics.MinY -ge 16) `
                "$($contract.Name) generated frame exceeds the 16px top safe area: $($frame.Name) begins at y=$($metrics.MinY)."
        }

        $groundedFrames = @(
            foreach ($pattern in $contract.GroundedFramePatterns) {
                Get-ChildItem -LiteralPath $contract.Art -Filter $pattern
            }
        ) | Sort-Object Name -Unique
        foreach ($frame in $groundedFrames) {
            $metrics = Get-OpaqueMetrics -Path $frame.FullName
            $contactDelta = [Math]::Abs($metrics.MaxY - $contract.GroundY)
            Assert-Condition ($contactDelta -le $contract.GroundTolerance) `
                "$($contract.Name) grounded frame misses y=$($contract.GroundY): $($frame.Name) ends at y=$($metrics.MaxY)."
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
        foreach ($direction in @("all", "down", "downSide", "side", "upSide", "up")) {
            $strip = $clip.$direction
            if ($null -eq $strip) { continue }
            if (-not [string]::IsNullOrWhiteSpace($strip.resourcePath)) {
                $assetPath = Join-Path $resourcesRoot (($strip.resourcePath -replace '/', '\') + ".png")
                Assert-Condition (Test-Path -LiteralPath $assetPath) `
                    "$($contract.Name) manifest atlas resource is missing: $($strip.resourcePath)"
            }
            foreach ($resourcePath in @($strip.resourcePaths)) {
                if ([string]::IsNullOrWhiteSpace($resourcePath)) { continue }
                $assetPath = Join-Path $resourcesRoot (($resourcePath -replace '/', '\') + ".png")
                Assert-Condition (Test-Path -LiteralPath $assetPath) `
                    "$($contract.Name) manifest resource is missing: $resourcePath"
            }
        }
    }

    if ($contract.Name -eq "Hero") {
        Assert-Condition ($manifest.version -ge 3) "Hero manifest must use the v3 eight-direction contract."
        Assert-Condition ($manifest.directional -and $manifest.eightDirectional) `
            "Hero manifest must enable directional and eightDirectional rendering."
        Assert-Condition ([Math]::Abs([double]$manifest.pixelsPerUnit - 540.0) -lt 0.001) `
            "Hero manifest pixelsPerUnit must be the shared 540 scale."
        Assert-Condition ([Math]::Abs([double]$manifest.pivotY - 0.0625) -lt 0.0001) `
            "Hero manifest pivotY must remain aligned to the 48px ground pivot."

        $directionNames = @("down", "downSide", "side", "upSide", "up")
        $smoothActionContracts = @{
            Walk = @{ Count = 6; Atlas = $true; Suffix = "_v5"; Fps = 7.5 }
            Run = @{ Count = 6; Atlas = $true; Suffix = "_v5"; Fps = 15.0 }
            Jump = @{ Count = 4; Atlas = $true; Suffix = "_v5"; Fps = 10.0 }
            Sword = @{ Count = 4; Atlas = $false; Suffix = "_v4"; Fps = 12.0 }
            MagicCharge = @{ Count = 3; Atlas = $false; Suffix = "_v4"; Fps = 7.5 }
            MagicRelease = @{ Count = 3; Atlas = $false; Suffix = "_v4"; Fps = 12.0 }
        }
        foreach ($entry in $smoothActionContracts.GetEnumerator()) {
            $clip = @($manifest.clips | Where-Object action -eq $entry.Key)[0]
            Assert-Condition ($null -ne $clip) "Hero manifest is missing $($entry.Key)."
            Assert-Condition ([Math]::Abs([double]$clip.framesPerSecond - [double]$entry.Value.Fps) -lt 0.001) `
                "Hero $($entry.Key) uses $($clip.framesPerSecond) fps, expected $($entry.Value.Fps)."
            Assert-Condition ($null -eq $clip.all) `
                "Hero $($entry.Key) must use directional strips instead of one all strip."
            foreach ($direction in $directionNames) {
                $strip = $clip.$direction
                Assert-Condition ($null -ne $strip) `
                    "Hero $($entry.Key) is missing its $direction strip."
                $contractDefinition = $entry.Value
                $stripPpu = if ($strip.PSObject.Properties.Name -contains "pixelsPerUnit") {
                    [double]$strip.pixelsPerUnit
                } else {
                    0.0
                }
                Assert-Condition ($stripPpu -le 1.0) `
                    "Hero $($entry.Key) $direction must inherit the shared manifest pixelsPerUnit."
                if ($contractDefinition.Atlas) {
                    $columns = @($strip.frameColumns)
                    $rows = @($strip.frameRows)
                    Assert-Condition ($columns.Count -eq $contractDefinition.Count) `
                        "Hero $($entry.Key) $direction has $($columns.Count) atlas frames, expected $($contractDefinition.Count)."
                    Assert-Condition ($rows.Count -eq $contractDefinition.Count) `
                        "Hero $($entry.Key) $direction must declare one row for every atlas frame."
                    Assert-Condition (-not [string]::IsNullOrWhiteSpace($strip.resourcePath)) `
                        "Hero $($entry.Key) $direction is missing its atlas resourcePath."
                    Assert-Condition ($strip.resourcePath.EndsWith($contractDefinition.Suffix)) `
                        "Hero $($entry.Key) $direction uses the wrong atlas generation: $($strip.resourcePath)"
                }
                else {
                    $paths = @($strip.resourcePaths)
                    Assert-Condition ($paths.Count -eq $contractDefinition.Count) `
                        "Hero $($entry.Key) $direction has $($paths.Count) frames, expected $($contractDefinition.Count)."
                    foreach ($resourcePath in $paths) {
                        Assert-Condition ($resourcePath.EndsWith($contractDefinition.Suffix)) `
                            "Hero $($entry.Key) $direction uses the wrong frame generation: $resourcePath"
                    }
                }
            }
        }
    }

    Write-Host "$($contract.Name): $($artFiles.Count) frames, $($required.Count) required actions, contract OK."
}

Write-Host "HD-2D asset validation passed without starting Unity."
