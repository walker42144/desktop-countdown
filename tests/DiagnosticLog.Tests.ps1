$assembly = [System.Reflection.Assembly]::LoadFrom($env:DESKTOP_COUNTDOWN_TEST_EXE)
$logType = $assembly.GetType('DesktopCountdown.DiagnosticLog', $true)
$constructor = $logType.GetConstructor([System.Reflection.BindingFlags]'Instance,NonPublic',
    $null, [Type[]]@([string], [int]), $null)
$writeMethod = $logType.GetMethod('Write', [System.Reflection.BindingFlags]'Instance,NonPublic')

function New-TestDiagnosticLog([string]$directory, [int]$limitBytes) {
    return $constructor.Invoke([object[]]@($directory, $limitBytes))
}

function Write-TestDiagnosticLog([object]$log, [string]$context, [Exception]$error) {
    return $writeMethod.Invoke($log, [object[]]@($context, $error))
}

Describe 'DesktopCountdown diagnostic log' {
    BeforeEach {
        $directory = Join-Path ([System.IO.Path]::GetTempPath()) ('DesktopCountdown-Log-' + [Guid]::NewGuid().ToString('N'))
        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    }

    AfterEach {
        [System.IO.Directory]::Delete($directory, $true)
    }

    It 'rotates into one bounded archive' {
        $log = New-TestDiagnosticLog $directory 512
        for ($index = 0; $index -lt 40; $index++) {
            (Write-TestDiagnosticLog $log 'Test.Rotation' ([InvalidOperationException]::new('ignored'))) | Should Be $true
        }
        $current = Join-Path $directory 'diagnostics.log'
        $archive = $current + '.1'
        [System.IO.File]::Exists($current) | Should Be $true
        [System.IO.File]::Exists($archive) | Should Be $true
        (Get-Item -LiteralPath $current).Length -le 512 | Should Be $true
        (Get-Item -LiteralPath $archive).Length -le 512 | Should Be $true
        (Get-ChildItem -LiteralPath $directory -File).Count | Should Be 2
    }

    It 'does not persist exception messages or path-shaped contexts' {
        $log = New-TestDiagnosticLog $directory 512
        $secret = 'C:\Users\Private\sensitive-token.txt'
        (Write-TestDiagnosticLog $log $secret ([InvalidOperationException]::new($secret))) | Should Be $true
        $content = [System.IO.File]::ReadAllText((Join-Path $directory 'diagnostics.log'))
        $content | Should Not Match 'Private|sensitive-token|C:\\Users'
        $content | Should Match 'Unknown System.InvalidOperationException 0x'
    }

    It 'returns failure without throwing when the directory is unwritable' {
        $file = Join-Path $directory 'not-a-directory'
        [System.IO.File]::WriteAllText($file, 'occupied')
        $log = New-TestDiagnosticLog $file 512
        (Write-TestDiagnosticLog $log 'Test.Failure' ([Exception]::new('ignored'))) | Should Be $false
        [System.IO.File]::ReadAllText($file) | Should Be 'occupied'
    }

    It 'does not create a file for a missing exception' {
        $log = New-TestDiagnosticLog $directory 512
        (Write-TestDiagnosticLog $log 'Test.Null' $null) | Should Be $false
        (Get-ChildItem -LiteralPath $directory -File).Count | Should Be 0
    }
}
