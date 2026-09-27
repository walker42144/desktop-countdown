$application = $env:DESKTOP_COUNTDOWN_TEST_EXE

Describe 'DesktopCountdown baseline' {
    It 'runs the built-in smoke checks' {
        $process = Start-Process -FilePath $application -ArgumentList '--smoke-test' -Wait -PassThru -WindowStyle Hidden
        $process.ExitCode | Should Be 0
        $resultPath = Join-Path (Split-Path -Parent $application) 'smoke-test.txt'
        (Get-Content -LiteralPath $resultPath -Raw).StartsWith('OK') | Should Be $true
    }

    It 'renders all theme previews' {
        $process = Start-Process -FilePath $application -ArgumentList '--render-previews' -Wait -PassThru -WindowStyle Hidden
        $process.ExitCode | Should Be 0
        $resultPath = Join-Path (Split-Path -Parent $application) 'previews\render-result.txt'
        (Get-Content -LiteralPath $resultPath -Raw).StartsWith('OK') | Should Be $true
    }

    It 'writes task XML accepted by the Task Scheduler parser' {
        $xmlPath = Join-Path ([System.IO.Path]::GetTempPath()) ('DesktopCountdown-Baseline-' + [Guid]::NewGuid().ToString('N') + '.xml')
        try {
            $assembly = [System.Reflection.Assembly]::LoadFrom($application)
            $type = $assembly.GetType('DesktopCountdown.StartupService', $true)
            $flags = [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Static
            $writeXml = $type.GetMethod('WriteTaskXml', $flags)
            $arguments = New-Object 'System.Object[]' 2
            $arguments[0] = [string]$xmlPath
            $arguments[1] = [string](Join-Path $env:WINDIR 'System32\notepad.exe')
            $writeXml.Invoke($null, $arguments) | Out-Null
            $bytes = [System.IO.File]::ReadAllBytes($xmlPath)
            $bytes[0] | Should Be 255
            $bytes[1] | Should Be 254
            $xml = [System.IO.File]::ReadAllText($xmlPath, [System.Text.Encoding]::Unicode)
            $xml.StartsWith('<?xml version="1.0" encoding="utf-16"?>') | Should Be $true
            $service = New-Object -ComObject 'Schedule.Service'
            $service.Connect()
            $definition = $service.NewTask(0)
            $definition.XmlText = $xml
            $definition.XmlText.Length -gt 0 | Should Be $true
        }
        finally {
            if ([System.IO.File]::Exists($xmlPath)) { [System.IO.File]::Delete($xmlPath) }
        }
    }
}
