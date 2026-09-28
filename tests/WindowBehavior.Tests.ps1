$assembly = [System.Reflection.Assembly]::LoadFrom($env:DESKTOP_COUNTDOWN_TEST_EXE)
$serviceType = $assembly.GetType('DesktopCountdown.ClickThroughService', $true)
$flags = [System.Reflection.BindingFlags]'Instance,NonPublic'
$constructor = @($serviceType.GetConstructors($flags) | Where-Object { $_.GetParameters().Count -eq 2 })[0]
$apply = $serviceType.GetMethod('TryApply', $flags)

function New-FakeClickThrough {
    $state = @{ Style = 0x4000; Writes = 0; FailFirst = $false; Missing = $false }
    $read = [Func[IntPtr,Nullable[int]]]({
        param($handle)
        if ($state.Missing) { return $null }
        return [Nullable[int]]::new($state.Style)
    }.GetNewClosure())
    $write = [Func[IntPtr,int,bool]]({
        param($handle, $value)
        $state.Style = $value
        $state.Writes++
        return -not ($state.FailFirst -and $state.Writes -eq 1)
    }.GetNewClosure())
    return [pscustomobject]@{ Service = $constructor.Invoke([object[]]@($read, $write)); State = $state }
}

function Invoke-ClickThrough([object]$fake, [bool]$enabled) {
    return $apply.Invoke($fake.Service, [object[]]@([IntPtr]100, $enabled))
}

Describe 'DesktopCountdown ordinary window behavior' {
    It 'removes desktop parenting while retaining click-through' {
        $assembly.GetType('DesktopCountdown.DesktopHostService', $false) | Should BeNullOrEmpty
        $assembly.GetType('DesktopCountdown.NativeWindowOps', $false) | Should BeNullOrEmpty
        $serviceType.GetMethod('Apply') | Should Not BeNullOrEmpty
        $main = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\src\DesktopCountdown\MainWindow.cs') -Raw
        $settings = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\src\DesktopCountdown\SettingsWindow.cs') -Raw
        $sections = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\src\DesktopCountdown\SettingsSections.cs') -Raw
        $main | Should Not Match 'desktopTrayItem|ApplyDesktopMode|ToggleDesktopMode'
        $main | Should Not Match '\bDesktopMode\b'
        $settings | Should Not Match 'desktopModeBox'
        $settings | Should Match 'working.DesktopMode = false;'
        $sections | Should Not Match 'desktopModeBox'
    }

    It 'applies and clears click-through bits without changing unrelated styles' {
        $fake = New-FakeClickThrough
        (Invoke-ClickThrough $fake $true) | Should Be $true
        ($fake.State.Style -band 0x080000A0) | Should Be 0x080000A0
        ($fake.State.Style -band 0x4000) | Should Be 0x4000
        (Invoke-ClickThrough $fake $false) | Should Be $true
        ($fake.State.Style -band 0x08000020) | Should Be 0
        ($fake.State.Style -band 0x80) | Should Be 0x80
        ($fake.State.Style -band 0x4000) | Should Be 0x4000
    }

    It 'rolls back a failed click-through write and ignores an unreadable window' {
        $fake = New-FakeClickThrough
        $fake.State.FailFirst = $true
        (Invoke-ClickThrough $fake $true) | Should Be $false
        $fake.State.Style | Should Be 0x4000
        $fake.State.Writes | Should Be 2
        $fake.State.Missing = $true
        (Invoke-ClickThrough $fake $true) | Should Be $false
        $fake.State.Writes | Should Be 2
    }

    It 'normalizes the legacy desktop-mode setting to ordinary mode' {
        $settingsType = $assembly.GetType('DesktopCountdown.AppSettings', $true)
        $serializer = [System.Runtime.Serialization.Json.DataContractJsonSerializer]::new($settingsType)
        $legacy = [System.IO.MemoryStream]::new([System.Text.Encoding]::UTF8.GetBytes('{"DesktopMode":true}'))
        try { $settings = $serializer.ReadObject($legacy) }
        finally { $legacy.Dispose() }
        $settings.DesktopMode | Should Be $true
        $settings.Validate()
        $settings.DesktopMode | Should Be $false
    }

    It 'detects a window outside both monitor working areas' {
        $windowType = $assembly.GetType('DesktopCountdown.MainWindow', $true)
        $method = $windowType.GetMethod('IsOnWorkingScreen', [System.Reflection.BindingFlags]'Static,NonPublic')
        $screens = [System.Drawing.Rectangle[]]@(
            [System.Drawing.Rectangle]::new(0, 0, 1707, 1019),
            [System.Drawing.Rectangle]::new(-1493, 0, 1493, 885))
        $arguments = [object[]]::new(2)
        $arguments[1] = $screens
        $arguments[0] = [System.Drawing.Rectangle]::new(-2200, 80, 500, 160)
        $method.Invoke($null, $arguments) | Should Be $false
        $arguments[0] = [System.Drawing.Rectangle]::new(-1450, 80, 500, 160)
        $method.Invoke($null, $arguments) | Should Be $true
    }

    It 'treats out-of-memory as fatal and rearms recoverable countdown ticks' {
        $program = $assembly.GetType('DesktopCountdown.Program', $true)
        $classifier = $program.GetMethod('IsFatalUiException', [System.Reflection.BindingFlags]'Static,NonPublic')
        $classifier.Invoke($null, [object[]]@([OutOfMemoryException]::new())) | Should Be $true
        $classifier.Invoke($null, [object[]]@([InvalidOperationException]::new())) | Should Be $false
        $source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\src\DesktopCountdown\MainWindow.cs') -Raw
        $source | Should Match 'finally \{ if \(!fatal && !exiting\) ScheduleNextTick\(\); \}'
        $programSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\src\DesktopCountdown\Program.cs') -Raw
        $programSource | Should Match 'Environment.FailFast\('
    }
}
