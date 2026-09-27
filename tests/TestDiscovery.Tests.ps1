$application = $env:DESKTOP_COUNTDOWN_TEST_EXE

Describe 'DesktopCountdown test discovery' {
    It 'fails when the required startup module is missing' {
        $target = if ($env:DESKTOP_COUNTDOWN_TEST_MISSING_STARTUP -eq '1') {
            [System.Linq.Enumerable].Assembly.Location
        } else {
            $application
        }
        $assembly = [System.Reflection.Assembly]::LoadFrom($target)
        $assembly.GetType('DesktopCountdown.StartupService', $false) | Should Not BeNullOrEmpty
    }

    It 'returns a nonzero test command exit code when startup is missing' {
        if ($env:DESKTOP_COUNTDOWN_TEST_MISSING_STARTUP -eq '1') { return }

        $runner = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\dev.ps1')).Path
        $outputDirectory = Join-Path $PSScriptRoot '..\artifacts\negative-startup-test'
        New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
        $standardOutput = Join-Path $outputDirectory 'stdout.txt'
        $standardError = Join-Path $outputDirectory 'stderr.txt'
        $previous = $env:DESKTOP_COUNTDOWN_TEST_MISSING_STARTUP
        try {
            $env:DESKTOP_COUNTDOWN_TEST_MISSING_STARTUP = '1'
            $process = Start-Process -FilePath (Get-Command pwsh).Source -ArgumentList @('-NoProfile', '-File', "`"$runner`"", 'test') `
                -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput $standardOutput -RedirectStandardError $standardError
            $process.ExitCode | Should Not Be 0
            $output = [System.IO.File]::ReadAllText($standardOutput) + [System.IO.File]::ReadAllText($standardError)
            $output | Should Match 'fails when the required startup module is missing'
            $output | Should Match 'Failed: 1'
        }
        finally {
            $env:DESKTOP_COUNTDOWN_TEST_MISSING_STARTUP = $previous
        }
    }
}
