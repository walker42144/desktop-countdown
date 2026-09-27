$assembly = [System.Reflection.Assembly]::LoadFrom($env:DESKTOP_COUNTDOWN_TEST_EXE)
$serviceType = $assembly.GetType('DesktopCountdown.DesktopHostService', $true)
$opsType = $assembly.GetType('DesktopCountdown.NativeWindowOps', $true)
$snapshotType = $assembly.GetType('DesktopCountdown.NativeWindowSnapshot', $true)
$flags = [System.Reflection.BindingFlags]'Instance,NonPublic'
$opsConstructor = @($opsType.GetConstructors($flags) | Where-Object { $_.GetParameters().Count -eq 6 })[0]
$serviceConstructor = @($serviceType.GetConstructors($flags) | Where-Object { $_.GetParameters().Count -eq 1 })[0]
$snapshotConstructor = @($snapshotType.GetConstructors($flags) | Where-Object { $_.GetParameters().Count -eq 7 })[0]
$applyMethod = $serviceType.GetMethod('ApplyWithHandle', $flags)
$clickMethod = $serviceType.GetMethod('TryApplyClickThrough', $flags)

function New-FakeDesktopHost {
    $snapshotCtor = $snapshotConstructor
    $state = @{
        Parent = [IntPtr]::Zero; Style = [int]::MinValue; ExStyle = 0
        Left = 100; Top = 200; Width = 300; Height = 120
        HostAvailable = $true; CaptureAvailable = $true
        FailStep = ''; FailureUsed = $false
    }
    $find = [Func[IntPtr]]({
        if ($state.HostAvailable) { return [IntPtr]200 }
        return [IntPtr]::Zero
    }.GetNewClosure())
    $capture = [Func[IntPtr,object]]({
        param($handle)
        if (-not $state.CaptureAvailable) { return $null }
        return $snapshotCtor.Invoke([object[]]@($state.Parent, $state.Style, $state.ExStyle,
            $state.Left, $state.Top, $state.Width, $state.Height))
    }.GetNewClosure())
    $setStyle = [Func[IntPtr,int,bool]]({
        param($handle, $value)
        $state.Style = $value
        if ($state.FailStep -eq 'style' -and -not $state.FailureUsed) {
            $state.FailureUsed = $true
            return $false
        }
        return $true
    }.GetNewClosure())
    $setExStyle = [Func[IntPtr,int,bool]]({
        param($handle, $value)
        $state.ExStyle = $value
        if ($state.FailStep -eq 'exstyle' -and -not $state.FailureUsed) {
            $state.FailureUsed = $true
            return $false
        }
        return $true
    }.GetNewClosure())
    $setParent = [Func[IntPtr,IntPtr,bool]]({
        param($handle, $value)
        $state.Parent = $value
        if ($state.FailStep -eq 'parent' -and -not $state.FailureUsed) {
            $state.FailureUsed = $true
            return $false
        }
        return $true
    }.GetNewClosure())
    $setPosition = [Func[IntPtr,IntPtr,int,int,int,int,bool]]({
        param($handle, $parent, $left, $top, $width, $height)
        $state.Left = $left; $state.Top = $top; $state.Width = $width; $state.Height = $height
        if ($state.FailStep -eq 'position' -and -not $state.FailureUsed) {
            $state.FailureUsed = $true
            return $false
        }
        return $true
    }.GetNewClosure())
    $ops = $opsConstructor.Invoke([object[]]@($find, $capture, $setStyle, $setExStyle,
        $setParent, $setPosition))
    return [pscustomobject]@{ Service = $serviceConstructor.Invoke([object[]]@($ops)); State = $state }
}

function Invoke-DesktopMode([object]$fake, [bool]$enabled) {
    return $applyMethod.Invoke($fake.Service, [object[]]@([IntPtr]100, $enabled))
}

function Invoke-ClickThrough([object]$fake, [bool]$enabled) {
    return $clickMethod.Invoke($fake.Service, [object[]]@([IntPtr]100, $enabled))
}

Describe 'DesktopCountdown desktop host rollback' {
    It 'keeps the public desktop-host signatures' {
        $serviceType.GetConstructor([Type[]]@()) | Should Not BeNullOrEmpty
        $serviceType.GetMethod('ApplyDesktopMode').ReturnType | Should Be ([bool])
        $serviceType.GetMethod('ApplyClickThrough').ReturnType | Should Be ([void])
    }

    It 'attaches only after all operations and restores the original state on detach' {
        $fake = New-FakeDesktopHost
        (Invoke-DesktopMode $fake $true) | Should Be $true
        $fake.Service.IsAttached | Should Be $true
        $fake.State.Parent | Should Be ([IntPtr]200)
        ($fake.State.Style -band 0x40000000) | Should Be 0x40000000
        $fake.State.Left = 140
        (Invoke-DesktopMode $fake $false) | Should Be $false
        $fake.Service.IsAttached | Should Be $false
        $fake.State.Parent | Should Be ([IntPtr]::Zero)
        $fake.State.Style | Should Be ([int]::MinValue)
        $fake.State.Left | Should Be 140
    }

    It 'rolls back each side-effectful attach failure' {
        foreach ($step in @('style', 'parent', 'position')) {
            $fake = New-FakeDesktopHost
            $fake.State.FailStep = $step
            (Invoke-DesktopMode $fake $true) | Should Be $false
            $fake.Service.IsAttached | Should Be $false
            $fake.State.Parent | Should Be ([IntPtr]::Zero)
            $fake.State.Style | Should Be ([int]::MinValue)
            $fake.State.ExStyle | Should Be 0
            $fake.State.Left | Should Be 100
            $fake.State.Top | Should Be 200
        }
    }

    It 'does not mutate a window when host or capture is unavailable' {
        $fake = New-FakeDesktopHost
        $fake.State.HostAvailable = $false
        (Invoke-DesktopMode $fake $true) | Should Be $false
        $fake.State.Style | Should Be ([int]::MinValue)
        $fake.State.HostAvailable = $true
        $fake.State.CaptureAvailable = $false
        (Invoke-DesktopMode $fake $true) | Should Be $false
        $fake.State.Parent | Should Be ([IntPtr]::Zero)
    }

    It 'restores the attached state when detach fails' {
        $fake = New-FakeDesktopHost
        (Invoke-DesktopMode $fake $true) | Should Be $true
        $attachedStyle = $fake.State.Style
        $fake.State.FailStep = 'position'
        (Invoke-DesktopMode $fake $false) | Should Be $true
        $fake.Service.IsAttached | Should Be $true
        $fake.State.Parent | Should Be ([IntPtr]200)
        $fake.State.Style | Should Be $attachedStyle
    }

    It 'recovers after the wallpaper worker disappears' {
        $fake = New-FakeDesktopHost
        (Invoke-DesktopMode $fake $true) | Should Be $true
        $fake.State.Parent = [IntPtr]::Zero
        (Invoke-DesktopMode $fake $true) | Should Be $true
        $fake.Service.IsAttached | Should Be $true
        $fake.State.Parent | Should Be ([IntPtr]200)
        (Invoke-DesktopMode $fake $false) | Should Be $false
        $fake.State.Style | Should Be ([int]::MinValue)
    }

    It 'restores the extended style when click-through fails' {
        $fake = New-FakeDesktopHost
        $fake.State.FailStep = 'exstyle'
        (Invoke-ClickThrough $fake $true) | Should Be $false
        $fake.State.ExStyle | Should Be 0
        (Invoke-ClickThrough $fake $true) | Should Be $true
        ($fake.State.ExStyle -band 0x080000A0) | Should Be 0x080000A0
    }
}
