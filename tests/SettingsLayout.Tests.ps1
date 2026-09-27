$assembly = [System.Reflection.Assembly]::LoadFrom($env:DESKTOP_COUNTDOWN_TEST_EXE)
$windowType = $assembly.GetType('DesktopCountdown.SettingsWindow', $true)
$flags = [System.Reflection.BindingFlags]'Instance,NonPublic'

Describe 'DesktopCountdown settings layout' {
    It 'keeps the public settings window contract' {
        $constructor = @($windowType.GetConstructors() | Where-Object { $_.GetParameters().Count -eq 3 })[0]
        $constructor | Should Not BeNullOrEmpty
        $windowType.GetProperty('Result').PropertyType.FullName | Should Be 'DesktopCountdown.AppSettings'
        $windowType.GetMethod('OpenOnScreen') | Should Not BeNullOrEmpty
        $windowType.GetEvent('MotionTrialRequested') | Should Not BeNullOrEmpty
    }

    It 'splits grouped layout and bindings out of the constructor' {
        foreach ($name in @('BuildFormSections', 'BuildWindowShell', 'WireInteractions')) {
            $windowType.GetMethod($name, $flags) | Should Not BeNullOrEmpty
        }
        $source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\src\DesktopCountdown\SettingsWindow.cs') -Raw
        $sections = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\src\DesktopCountdown\SettingsSections.cs') -Raw
        $source | Should Match 'BuildFormSections\(form, clockStatus\)'
        $source | Should Match 'BuildWindowShell\(root, form, dark\)'
        $source | Should Match 'WireInteractions\(root, dark\)'
        $sections | Should Match 'tryMotion.Click \+= delegate \{ TryMotion\(\); \};'
        $sections | Should Match 'save.Click \+= SaveClicked;'
    }

    It 'renders both light and dark settings screenshots' {
        $process = Start-Process -FilePath $env:DESKTOP_COUNTDOWN_TEST_EXE -ArgumentList '--render-settings' -Wait -PassThru -WindowStyle Hidden
        $process.ExitCode | Should Be 0
        $directory = Join-Path (Split-Path -Parent $env:DESKTOP_COUNTDOWN_TEST_EXE) 'settings-previews'
        [System.IO.File]::ReadAllText((Join-Path $directory 'result.txt')).StartsWith('OK') | Should Be $true
        $hashes = @()
        foreach ($name in @('light.png', 'dark.png')) {
            $path = Join-Path $directory $name
            [System.IO.File]::Exists($path) | Should Be $true
            (Get-Item -LiteralPath $path).Length -gt 10000 | Should Be $true
            $bitmap = [System.Drawing.Bitmap]::new($path)
            try {
                $bitmap.Width | Should Be 950
                $bitmap.Height | Should Be 810
            }
            finally { $bitmap.Dispose() }
            $hashes += (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        }
        $hashes[0] | Should Not Be $hashes[1]
    }
}
