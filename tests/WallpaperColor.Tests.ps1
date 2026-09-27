$assembly = [System.Reflection.Assembly]::LoadFrom($env:DESKTOP_COUNTDOWN_TEST_EXE)
$serviceType = $assembly.GetType('DesktopCountdown.WallpaperColorService', $true)
$flags = [System.Reflection.BindingFlags]'Static,NonPublic'
$analyzeMethod = $serviceType.GetMethod('AnalyzeFile', $flags)
$invalidateMethod = $serviceType.GetMethod('Invalidate', $flags)
$decodeProperty = $serviceType.GetProperty('DecodeCount', $flags)

function New-TestWallpaper([string]$path, [System.Drawing.Color]$left, [System.Drawing.Color]$right) {
    $bitmap = [System.Drawing.Bitmap]::new(2, 1)
    try {
        $bitmap.SetPixel(0, 0, $left)
        $bitmap.SetPixel(1, 0, $right)
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $bitmap.Dispose() }
}

function Get-TestAppearance([string]$path, [System.Drawing.Rectangle]$widget) {
    $destination = [System.Drawing.Rectangle]::new(0, 0, 2, 1)
    return $analyzeMethod.Invoke($null, [object[]]@($path, $widget, $destination, '2', $false))
}

Describe 'DesktopCountdown wallpaper sampling cache' {
    BeforeEach {
        $directory = Join-Path ([System.IO.Path]::GetTempPath()) ('DesktopCountdown-Wallpaper-' + [Guid]::NewGuid().ToString('N'))
        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
        $path = Join-Path $directory 'wallpaper.png'
        $invalidateMethod.Invoke($null, @()) | Out-Null
    }

    AfterEach {
        $invalidateMethod.Invoke($null, @()) | Out-Null
        [System.IO.Directory]::Delete($directory, $true)
    }

    It 'keeps fixed-image colors and reuses the same region without decoding' {
        New-TestWallpaper $path ([System.Drawing.Color]::Black) ([System.Drawing.Color]::White)
        $left = [System.Drawing.Rectangle]::new(0, 0, 1, 1)
        $before = $decodeProperty.GetValue($null)
        $clock = [System.Diagnostics.Stopwatch]::StartNew()
        $dark = Get-TestAppearance $path $left
        $coldMs = $clock.Elapsed.TotalMilliseconds
        $clock.Restart()
        $again = Get-TestAppearance $path $left
        $hotMs = $clock.Elapsed.TotalMilliseconds
        Write-Host ('WallpaperCache ColdMs={0:0.###} HotMs={1:0.###}' -f $coldMs, $hotMs)
        $dark.Luminance | Should Be 0
        $dark.Foreground.R | Should Be 245
        $again.Luminance | Should Be $dark.Luminance
        $decodeProperty.GetValue($null) | Should Be ($before + 1)
        $dark.Description = 'modified by caller'
        (Get-TestAppearance $path $left).Description | Should Not Be 'modified by caller'
    }

    It 'reuses the image but resamples after the widget moves' {
        New-TestWallpaper $path ([System.Drawing.Color]::Black) ([System.Drawing.Color]::White)
        $before = $decodeProperty.GetValue($null)
        $dark = Get-TestAppearance $path ([System.Drawing.Rectangle]::new(0, 0, 1, 1))
        $light = Get-TestAppearance $path ([System.Drawing.Rectangle]::new(1, 0, 1, 1))
        $dark.Luminance | Should Be 0
        $light.Luminance | Should Be 1
        $light.Foreground.R | Should Be 18
        $decodeProperty.GetValue($null) | Should Be ($before + 1)
    }

    It 'reloads after wallpaper contents change or preferences invalidate' {
        New-TestWallpaper $path ([System.Drawing.Color]::Black) ([System.Drawing.Color]::Black)
        $widget = [System.Drawing.Rectangle]::new(0, 0, 1, 1)
        $before = $decodeProperty.GetValue($null)
        (Get-TestAppearance $path $widget).Luminance | Should Be 0
        $firstWrite = [System.IO.File]::GetLastWriteTimeUtc($path)
        New-TestWallpaper $path ([System.Drawing.Color]::White) ([System.Drawing.Color]::White)
        [System.IO.File]::SetLastWriteTimeUtc($path, $firstWrite.AddSeconds(2))
        (Get-TestAppearance $path $widget).Luminance | Should Be 1
        $decodeProperty.GetValue($null) | Should Be ($before + 2)
        $invalidateMethod.Invoke($null, @()) | Out-Null
        (Get-TestAppearance $path $widget).Luminance | Should Be 1
        $decodeProperty.GetValue($null) | Should Be ($before + 3)
    }
}
