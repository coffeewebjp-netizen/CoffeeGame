param(
    [Parameter(Mandatory = $true)]
    [int]$ProcessId,
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class CoffeeGameWindowCapture
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr handle, out Rect rect);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr handle);

    [DllImport("user32.dll")]
    public static extern bool SetProcessDPIAware();
}
'@

# GetWindowRect and CopyFromScreen must use the same physical-pixel coordinate
# space. Without DPI awareness Windows virtualizes only the former on displays
# above 100%, which clips the right and bottom of the captured game window.
[CoffeeGameWindowCapture]::SetProcessDPIAware() | Out-Null

$process = Get-Process -Id $ProcessId -ErrorAction Stop
$process.Refresh()
$handle = $process.MainWindowHandle
if ($handle -eq [IntPtr]::Zero) {
    throw "Process $ProcessId has no main window."
}

[CoffeeGameWindowCapture]::SetForegroundWindow($handle) | Out-Null
Start-Sleep -Milliseconds 250

$rect = New-Object CoffeeGameWindowCapture+Rect
if (-not [CoffeeGameWindowCapture]::GetWindowRect($handle, [ref]$rect)) {
    throw "Could not read the CoffeeGAME window rectangle."
}

$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
if ($width -le 0 -or $height -le 0) {
    throw "CoffeeGAME window has invalid dimensions ${width}x${height}."
}

$bitmap = New-Object System.Drawing.Bitmap $width, $height
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
try {
    $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
    $directory = Split-Path -Parent $OutputPath
    if ($directory) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    $bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $graphics.Dispose()
    $bitmap.Dispose()
}

Write-Output "Captured $width x $height to $OutputPath"
