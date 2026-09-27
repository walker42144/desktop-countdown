$application = $env:DESKTOP_COUNTDOWN_TEST_EXE
$assembly = [System.Reflection.Assembly]::LoadFrom($application)
$settingsType = $assembly.GetType('DesktopCountdown.AppSettings', $true)
$serializer = [System.Runtime.Serialization.Json.DataContractJsonSerializer]::new($settingsType)

function New-DefaultSettings {
    return $settingsType.GetMethod('CreateDefault').Invoke($null, @())
}

Describe 'DesktopCountdown settings defaults' {
    It 'preserves default JSON fields and values' {
        $before = [DateTime]::Now.Date.AddDays(30).AddHours(9)
        $settings = New-DefaultSettings
        $after = [DateTime]::Now.Date.AddDays(30).AddHours(9)
        $stream = [System.IO.MemoryStream]::new()
        try {
            $serializer.WriteObject($stream, $settings)
            $json = [System.Text.Encoding]::UTF8.GetString($stream.ToArray()) | ConvertFrom-Json -AsHashtable
        }
        finally { $stream.Dispose() }

        $names = @('HasConfiguredTarget', 'Title', 'TargetLocal', 'TitleFontFamily', 'DigitFontFamily',
            'TitleFontSize', 'DigitFontSize', 'ShowSeconds', 'AutoColor', 'ManualForeground', 'Locked',
            'AlwaysOnTop', 'DesktopMode', 'StartWithWindows', 'Left', 'Top', 'NtpServer', 'ThemeName',
            'CountdownFormat', 'ClockFormat', 'MotionEnabled', 'MotionMode', 'MotionIntervalSeconds',
            'MotionAmplitudePixels', 'MotionTransitionMilliseconds', 'VisualBreathing', 'StartupMode')
        (@($json.Keys) -join ',') | Should Be ($names -join ',')

        $expected = @{
            HasConfiguredTarget = $false; Title = '距离目标时刻还有'; TitleFontFamily = 'MiSans'
            DigitFontFamily = 'Bahnschrift'; TitleFontSize = 22; DigitFontSize = 84; ShowSeconds = $true
            AutoColor = $true; ManualForeground = '#FFF5F7FA'; Locked = $false; AlwaysOnTop = $false
            DesktopMode = $false; StartWithWindows = $false; Left = 120; Top = 120
            NtpServer = 'time.windows.com'; ThemeName = 'MinimalGlass'
            CountdownFormat = '{days:3} 天  {hours:2}:{minutes:2}:{seconds:2}'
            ClockFormat = 'yyyy-MM-dd  HH:mm:ss'; MotionEnabled = $false; MotionMode = 'NineGrid'
            MotionIntervalSeconds = 120; MotionAmplitudePixels = 6; MotionTransitionMilliseconds = 0
            VisualBreathing = $false; StartupMode = 'Registry'
        }
        foreach ($name in $expected.Keys) { $json[$name] | Should Be $expected[$name] }
        $target = [DateTime]::ParseExact($json['TargetLocal'], 'yyyy-MM-dd HH:mm:ss',
            [System.Globalization.CultureInfo]::InvariantCulture)
        (($target -eq $before) -or ($target -eq $after)) | Should Be $true
    }

    It 'preserves legacy JSON values and supplies new defaults' {
        $legacyJson = '{"HasConfiguredTarget":true,"Title":"旧目标","TargetLocal":"2030-01-02 03:04:05","ShowSeconds":false,"ThemeName":"Air"}'
        $stream = [System.IO.MemoryStream]::new([System.Text.Encoding]::UTF8.GetBytes($legacyJson))
        try { $settings = $serializer.ReadObject($stream) }
        finally { $stream.Dispose() }
        $settings.Validate()
        $settings.HasConfiguredTarget | Should Be $true
        $settings.Title | Should Be '旧目标'
        $settings.TargetLocal | Should Be '2030-01-02 03:04:05'
        $settings.ShowSeconds | Should Be $false
        $settings.ThemeName | Should Be 'Air'
        $settings.CountdownFormat | Should Be '{days:3} 天  {hours:2}:{minutes:2}:{seconds:2}'
        $settings.ClockFormat | Should Be 'yyyy-MM-dd  HH:mm:ss'
        $settings.MotionMode | Should Be 'NineGrid'
        $settings.StartupMode | Should Be 'Registry'
    }

    It 'keeps public constants and removes display dependencies from settings' {
        $assembly.GetType('DesktopCountdown.ThemeCatalog', $true).GetField('DefaultTheme').GetRawConstantValue() | Should Be 'MinimalGlass'
        $formats = $assembly.GetType('DesktopCountdown.DisplayFormats', $true)
        $formats.GetField('DefaultCountdown').GetRawConstantValue() | Should Be '{days:3} 天  {hours:2}:{minutes:2}:{seconds:2}'
        $formats.GetField('DefaultClock').GetRawConstantValue() | Should Be 'yyyy-MM-dd  HH:mm:ss'
        $planner = $assembly.GetType('DesktopCountdown.MotionPlanner', $true)
        $planner.GetField('NineGrid').GetRawConstantValue() | Should Be 'NineGrid'
        $source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\src\DesktopCountdown\AppSettings.cs') -Raw
        $source | Should Not Match '\bThemeCatalog\b|\bDisplayFormats\b|\bMotionPlanner\b'
    }

    It 'validates motion identifiers independently of the public mode array' {
        $planner = $assembly.GetType('DesktopCountdown.MotionPlanner', $true)
        $modes = $planner.GetField('Modes').GetValue($null)
        $original = $modes[0]
        try {
            $modes[0] = 'Injected'
            $settings = New-DefaultSettings
            $settings.MotionMode = 'Injected'
            $settings.Validate()
            $settings.MotionMode | Should Be 'NineGrid'
        }
        finally { $modes[0] = $original }
    }
}
