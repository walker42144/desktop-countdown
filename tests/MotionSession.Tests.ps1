$assembly = [System.Reflection.Assembly]::LoadFrom($env:DESKTOP_COUNTDOWN_TEST_EXE)
$sessionType = $assembly.GetType('DesktopCountdown.MotionSession', $true)
$plannerType = $assembly.GetType('DesktopCountdown.MotionPlanner', $true)

function New-MotionSession {
    return [System.Activator]::CreateInstance($sessionType, $true)
}

Describe 'DesktopCountdown motion session' {
    It 'keeps the public planner contract and owns window motion state' {
        $plannerType.GetMethod('Next') | Should Not BeNullOrEmpty
        $plannerType.GetMethod('Constrain') | Should Not BeNullOrEmpty
        $source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\src\DesktopCountdown\MainWindow.cs') -Raw
        $source | Should Match 'private readonly MotionSession motion'
        $source | Should Not Match 'private (Point motionOffset|DateTime motionStarted|int motionStep|bool internalMotion)'
    }

    It 'starts pauses and resumes only when motion is active' {
        $session = New-MotionSession
        $now = [DateTime]::new(2026, 1, 1, 12, 0, 0)
        $session.Configure($true, 120, $true, $now)
        $session.StepTimer.IsEnabled | Should Be $true
        $session.NextAt | Should Be ($now.AddSeconds(120))
        $session.Pause()
        $session.StepTimer.IsEnabled | Should Be $false
        $session.Configure($true, 120, $false, $now)
        $session.NextAt | Should Be ([DateTime]::MinValue)
        $session.Configure($false, 120, $true, $now)
        $session.StepTimer.IsEnabled | Should Be $false
        $session.Configure($true, 30, $true, $now)
        $session.NextAt | Should Be ($now.AddSeconds(30))
        $session.Pause()
    }

    It 'preserves the anchor while applying and resets after a trial' {
        $session = New-MotionSession
        $session.SetAnchor(100, 200)
        $script:motionPosition = [System.Windows.Point]::new(100, 200)
        $apply = [Action]{
            $session.IsApplying | Should Be $true
            $script:motionPosition = [System.Windows.Point]::new(106, 194)
        }
        $position = [Func[System.Windows.Point]]{ $script:motionPosition }
        $session.ApplyPosition($apply, $position)
        $session.IsApplying | Should Be $false
        $session.Anchor | Should Be ([System.Windows.Point]::new(100, 200))
        $session.Offset | Should Be ([System.Windows.Point]::new(6, -6))
        $session.ResetPosition([Action]{
            $session.IsApplying | Should Be $true
            $script:motionPosition = $session.Anchor
        })
        $session.Offset | Should Be ([System.Windows.Point]::new(0, 0))
        $script:motionPosition | Should Be $session.Anchor
        $session.IsApplying | Should Be $false
    }

    It 'keeps step order and interpolates a move' {
        $session = New-MotionSession
        $session.TakeStep() | Should Be 0
        $session.TakeStep() | Should Be 1
        $started = [DateTime]::new(2026, 1, 1, 12, 0, 0, [DateTimeKind]::Utc)
        $target = [System.Windows.Point]::new(10, -10)
        $session.BeginMove($target, 1000, $started)
        $complete = $false
        $middle = $session.Interpolate($started.AddMilliseconds(500), [ref]$complete)
        $middle | Should Be ([System.Windows.Point]::new(5, -5))
        $complete | Should Be $false
        $end = $session.Interpolate($started.AddMilliseconds(1000), [ref]$complete)
        $end | Should Be $target
        $complete | Should Be $true
        $session.StopAnimation()
    }

    It 'keeps all six planner routes independent of the mutable public catalog' {
        $modes = $plannerType.GetField('Modes').GetValue($null)
        $original = $modes[0]
        try {
            $modes[0] = 'Injected'
            $random = [Random]::new(7)
            $zero = [System.Windows.Point]::new(0, 0)
            foreach ($mode in @('NineGrid', 'Orbit', 'Wander', 'Tide', 'EdgeWalk', 'FarNear')) {
                $point = $plannerType.GetMethod('Next').Invoke($null, @($mode, 0, 10.0, $random, $zero, $false, $false))
                [double]::IsNaN($point.X) | Should Be $false
                [double]::IsNaN($point.Y) | Should Be $false
            }
            $plannerType.GetMethod('Next').Invoke($null, @('NineGrid', 0, 10.0, $random, $zero, $false, $false)) |
                Should Be ([System.Windows.Point]::new(-10, -10))
        }
        finally { $modes[0] = $original }
    }
}
