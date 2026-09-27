$assembly = [System.Reflection.Assembly]::LoadFrom($env:DESKTOP_COUNTDOWN_TEST_EXE)
$sourceType = $assembly.GetType('DesktopCountdown.ClockSource', $true)
$clockType = $assembly.GetType('DesktopCountdown.AccurateClock', $true)
$syncCore = $clockType.GetMethod('SyncCore', [System.Reflection.BindingFlags]'Instance,NonPublic')

function Set-NtpTimestamp([byte[]]$data, [int]$index, [DateTime]$utc) {
    $epoch = [DateTime]::new(1900, 1, 1, 0, 0, 0, [DateTimeKind]::Utc)
    $ticks = ($utc - $epoch).Ticks
    [uint64]$seconds = [math]::Floor($ticks / [double][TimeSpan]::TicksPerSecond)
    [uint64]$fraction = [math]::Floor(($ticks % [TimeSpan]::TicksPerSecond) * 4294967296.0 / [TimeSpan]::TicksPerSecond)
    for ($i = 3; $i -ge 0; $i--) {
        $data[$index + $i] = [byte]($seconds -band 255)
        $seconds = $seconds -shr 8
        $data[$index + 4 + $i] = [byte]($fraction -band 255)
        $fraction = $fraction -shr 8
    }
}

function New-FakeClock {
    $state = @{
        Utc = [DateTime]::new(2026, 9, 27, 0, 0, 0, [DateTimeKind]::Utc)
        Tick = [long]0
        Mode = 'valid'
        Calls = 0
    }
    $utcNow = [Func[DateTime]]({ return $state.Utc }.GetNewClosure())
    $timestamp = [Func[long]]({ return $state.Tick }.GetNewClosure())
    $writeTimestamp = ${function:Set-NtpTimestamp}
    $exchange = [Func[string, int, byte[], byte[]]]({
        param($hostName, $timeout, $request)
        $state.Calls++
        if ($state.Mode -eq 'timeout') { throw [TimeoutException]::new('injected timeout') }
        if ($state.Mode -eq 'short') { return ,([byte[]](1, 2, 3)) }
        $response = [byte[]]::new(48)
        $response[0] = 0x24
        $response[1] = 2
        [Array]::Copy($request, 40, $response, 24, 8)
        $extra = if ($state.Mode -eq 'huge') { 172800 } else { 5 }
        & $writeTimestamp $response 32 ($state.Utc.AddSeconds($extra).AddMilliseconds(20))
        & $writeTimestamp $response 40 ($state.Utc.AddSeconds($extra).AddMilliseconds(30))
        $state.Utc = $state.Utc.AddMilliseconds(100)
        $state.Tick += 1000000
        if ($state.Mode -eq 'badMode') { $response[0] = 0x23 }
        if ($state.Mode -eq 'badStratum') { $response[1] = 0 }
        if ($state.Mode -eq 'badOrigin') { $response[24] = $response[24] -bxor 1 }
        return ,$response
    }.GetNewClosure())

    $sourceConstructor = @($sourceType.GetConstructors([System.Reflection.BindingFlags]'Instance,Public,NonPublic') |
        Where-Object { $_.GetParameters().Count -eq 4 })[0]
    $sourceArgs = [object[]]::new(4)
    $sourceArgs[0] = $utcNow
    $sourceArgs[1] = $timestamp
    $sourceArgs[2] = [long]10000000
    $sourceArgs[3] = $exchange
    $source = $sourceConstructor.Invoke($sourceArgs)

    $clockConstructor = @($clockType.GetConstructors([System.Reflection.BindingFlags]'Instance,Public,NonPublic') |
        Where-Object { $_.GetParameters().Count -eq 2 })[0]
    $clockArgs = [object[]]::new(2)
    $clockArgs[0] = [string]'test.invalid'
    $clockArgs[1] = $source
    $clock = $clockConstructor.Invoke($clockArgs)
    return [pscustomobject]@{ Clock = $clock; State = $state }
}

function Invoke-FakeSync([object]$fixture) {
    $syncCore.Invoke($fixture.Clock, [object[]]::new(0)) | Out-Null
}

Describe 'DesktopCountdown accurate clock' {
    It 'accepts a valid response and records a bounded offset' {
        $fixture = New-FakeClock
        try {
            Invoke-FakeSync $fixture
            $fixture.State.Calls | Should Be 3
            ($fixture.Clock.Offset.TotalSeconds -gt 4.9) | Should Be $true
            ($fixture.Clock.Offset.TotalSeconds -lt 5.1) | Should Be $true
            ($null -ne $fixture.Clock.LastSyncUtc) | Should Be $true
            $fixture.Clock.Status | Should Match '^已校准'
        }
        finally { $fixture.Clock.Dispose() }
    }

    It 'rejects a short response without changing the offset' {
        $fixture = New-FakeClock
        try {
            $fixture.State.Mode = 'short'
            Invoke-FakeSync $fixture
            $fixture.Clock.Offset | Should Be ([TimeSpan]::Zero)
            ($null -ne $fixture.Clock.LastSyncUtc) | Should Be $false
            $fixture.Clock.Status | Should Match '网络校准暂不可用'
        }
        finally { $fixture.Clock.Dispose() }
    }

    It 'rejects invalid server mode and stratum' {
        foreach ($mode in @('badMode', 'badStratum')) {
            $fixture = New-FakeClock
            try {
                $fixture.State.Mode = $mode
                Invoke-FakeSync $fixture
                $fixture.Clock.Offset | Should Be ([TimeSpan]::Zero)
                ($null -ne $fixture.Clock.LastSyncUtc) | Should Be $false
            }
            finally { $fixture.Clock.Dispose() }
        }
    }

    It 'rejects a response that does not match the request' {
        $fixture = New-FakeClock
        try {
            $fixture.State.Mode = 'badOrigin'
            Invoke-FakeSync $fixture
            $fixture.Clock.Offset | Should Be ([TimeSpan]::Zero)
            ($null -ne $fixture.Clock.LastSyncUtc) | Should Be $false
        }
        finally { $fixture.Clock.Dispose() }
    }

    It 'keeps the prior calibration after a timeout' {
        $fixture = New-FakeClock
        try {
            Invoke-FakeSync $fixture
            $previous = $fixture.Clock.Offset
            $fixture.State.Mode = 'timeout'
            Invoke-FakeSync $fixture
            $fixture.Clock.Offset | Should Be $previous
            $fixture.Clock.Status | Should Match '继续使用上次校准时间'
        }
        finally { $fixture.Clock.Dispose() }
    }

    It 'rejects an excessive offset' {
        $fixture = New-FakeClock
        try {
            $fixture.State.Mode = 'huge'
            Invoke-FakeSync $fixture
            $fixture.Clock.Offset | Should Be ([TimeSpan]::Zero)
            ($null -ne $fixture.Clock.LastSyncUtc) | Should Be $false
        }
        finally { $fixture.Clock.Dispose() }
    }

    It 'keeps calibrated time stable across a system clock jump' {
        $fixture = New-FakeClock
        try {
            Invoke-FakeSync $fixture
            $before = $fixture.Clock.UtcNow
            $fixture.State.Utc = $fixture.State.Utc.AddHours(3)
            $fixture.State.Tick += 20000000
            $after = $fixture.Clock.UtcNow
            (($after - $before).TotalSeconds -ge 1.9) | Should Be $true
            (($after - $before).TotalSeconds -le 2.1) | Should Be $true
        }
        finally { $fixture.Clock.Dispose() }
    }
}
