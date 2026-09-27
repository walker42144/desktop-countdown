$assembly = [System.Reflection.Assembly]::LoadFrom($env:DESKTOP_COUNTDOWN_TEST_EXE)
$formatsType = $assembly.GetType('DesktopCountdown.DisplayFormats', $true)
$renderMethod = $formatsType.GetMethod('RenderCountdown', [System.Reflection.BindingFlags]'Static,NonPublic')
$needsSecondsMethod = $formatsType.GetMethod('CountdownNeedsSeconds', [System.Reflection.BindingFlags]'Static,NonPublic')
$defaultCountdown = $formatsType.GetField('DefaultCountdown').GetRawConstantValue()

function Get-CountdownDisplay([string]$format, [TimeSpan]$duration, [bool]$elapsed, [bool]$showSeconds) {
    return $renderMethod.Invoke($null, [object[]]@($format, $duration, $elapsed, $showSeconds))
}

function Test-CountdownNeedsSeconds([string]$format, [bool]$showSeconds) {
    return $needsSecondsMethod.Invoke($null, [object[]]@($format, $showSeconds))
}

function Get-DisplayValue([object]$display, [string]$name) {
    $flags = [System.Reflection.BindingFlags]'Instance,Public,NonPublic'
    $field = $display.GetType().GetField($name, $flags)
    if ($null -ne $field) { return $field.GetValue($display) }
    return $display.GetType().GetProperty($name, $flags).GetValue($display)
}

Describe 'DesktopCountdown display formats' {
    It 'keeps default countdown text and its three display parts' {
        $duration = [TimeSpan]::new(83, 12, 36, 20)
        $display = Get-CountdownDisplay $defaultCountdown $duration $false $true
        (Get-DisplayValue $display 'Days') | Should Be '083'
        (Get-DisplayValue $display 'Unit') | Should Be ' 天  '
        (Get-DisplayValue $display 'Time') | Should Be '12:36:20'
        (Get-DisplayValue $display 'Text') | Should Be '083 天  12:36:20'
        $formatsType.GetMethod('Countdown').Invoke($null,
            [object[]]@($defaultCountdown, $duration, $false, $true)) | Should Be (Get-DisplayValue $display 'Text')
    }

    It 'keeps elapsed sign and day padding at the boundary' {
        $short = Get-CountdownDisplay $defaultCountdown ([TimeSpan]::new(2, 3, 4, 5)) $true $true
        (Get-DisplayValue $short 'Text') | Should Be '+002 天  03:04:05'
        $long = Get-CountdownDisplay $defaultCountdown ([TimeSpan]::new(1000, 3, 4, 5)) $true $true
        (Get-DisplayValue $long 'Text') | Should Be '+1000 天  03:04:05'
    }

    It 'hides seconds only for the default format and changes refresh need' {
        $duration = [TimeSpan]::new(83, 12, 36, 20)
        $hidden = Get-CountdownDisplay $defaultCountdown $duration $false $false
        (Get-DisplayValue $hidden 'Text') | Should Be '083 天  12:36'
        (Get-DisplayValue $hidden 'NeedsSeconds') | Should Be $false
        (Test-CountdownNeedsSeconds $defaultCountdown $false) | Should Be $false
        (Test-CountdownNeedsSeconds $defaultCountdown $true) | Should Be $true
        $custom = '{seconds:2} 秒'
        (Get-DisplayValue (Get-CountdownDisplay $custom $duration $false $false) 'Text') | Should Be '20 秒'
        (Test-CountdownNeedsSeconds $custom $false) | Should Be $true
    }

    It 'uses rendered tokens for custom refresh including quoted and escaped text' {
        $duration = [TimeSpan]::new(2, 3, 4, 5)
        $format = "'{seconds:2}' \{minutes:2}"
        $display = Get-CountdownDisplay $format $duration $false $false
        (Get-DisplayValue $display 'Text') | Should Be "'05' \04"
        (Get-DisplayValue $display 'Unit') | Should Be ''
        (Get-DisplayValue $display 'Time') | Should Be ''
        (Get-DisplayValue $display 'NeedsSeconds') | Should Be $true
        (Test-CountdownNeedsSeconds "'{minutes:2}' \{hours:2}" $true) | Should Be $false
    }

    It 'keeps custom sign and total-hours formatting' {
        $display = Get-CountdownDisplay '{sign}{totalHours:4}小时 {minutes:2}分' ([TimeSpan]::new(2, 3, 4, 5)) $true $true
        (Get-DisplayValue $display 'Text') | Should Be '+0051小时 04分'
        (Get-DisplayValue $display 'NeedsSeconds') | Should Be $false
    }
}
