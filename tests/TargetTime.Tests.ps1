$assembly = [System.Reflection.Assembly]::LoadFrom($env:DESKTOP_COUNTDOWN_TEST_EXE)
$settingsType = $assembly.GetType('DesktopCountdown.AppSettings', $true)
$fileOpsType = $assembly.GetType('DesktopCountdown.SettingsFileOps', $true)
$windowType = $assembly.GetType('DesktopCountdown.MainWindow', $true)
$repairProperty = $settingsType.GetProperty('TargetNeedsRepair', [System.Reflection.BindingFlags]'Instance,NonPublic')
$headingMethod = $windowType.GetMethod('TargetHeading', [System.Reflection.BindingFlags]'Static,NonPublic')

function New-TargetFileOps([string]$path) {
    $constructor = @($fileOpsType.GetConstructors([System.Reflection.BindingFlags]'Instance,Public,NonPublic') |
        Where-Object { $_.GetParameters().Count -eq 1 })[0]
    $arguments = [object[]]::new(1)
    $arguments[0] = [string]$path
    return $constructor.Invoke($arguments)
}

function Read-TargetSettings([string]$path) {
    return (New-TargetFileOps $path).Load()
}

function Get-TargetHeading([object]$settings) {
    $arguments = [object[]]::new(1)
    $arguments[0] = $settings
    return $headingMethod.Invoke($null, $arguments)
}

function Get-TargetError([object]$settings) {
    try { $null = $settings.GetTargetLocal(); return $null }
    catch { return $_.Exception }
}

Describe 'DesktopCountdown target time boundary' {
    BeforeEach {
        $directory = Join-Path ([System.IO.Path]::GetTempPath()) ('DesktopCountdown-Target-' + [Guid]::NewGuid().ToString('N'))
        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
        $path = Join-Path $directory 'settings.json'
    }

    AfterEach {
        [System.IO.Directory]::Delete($directory, $true)
    }

    It 'preserves a valid exact local target and its JSON' {
        $json = '{"HasConfiguredTarget":true,"Title":"有效目标","TargetLocal":"2030-01-02 03:04:05"}'
        [System.IO.File]::WriteAllText($path, $json)
        $settings = Read-TargetSettings $path
        $settings.HasConfiguredTarget | Should Be $true
        $repairProperty.GetValue($settings) | Should Be $false
        $settings.GetTargetLocal().ToString('yyyy-MM-dd HH:mm:ss') | Should Be '2030-01-02 03:04:05'
        $settings.GetTargetLocal().Kind | Should Be ([DateTimeKind]::Local)
        (Get-TargetHeading $settings) | Should Be '有效目标'
        [System.IO.File]::ReadAllText($path) | Should Be $json
    }

    It 'continues to parse a legacy culture-specific target' {
        $previousCulture = [System.Threading.Thread]::CurrentThread.CurrentCulture
        try {
            [System.Threading.Thread]::CurrentThread.CurrentCulture = [System.Globalization.CultureInfo]::GetCultureInfo('en-US')
            [System.IO.File]::WriteAllText($path,
                '{"HasConfiguredTarget":true,"TargetLocal":"1/2/2030 3:04 AM"}')
            $settings = Read-TargetSettings $path
            $settings.HasConfiguredTarget | Should Be $true
            $settings.GetTargetLocal().ToString('yyyy-MM-dd HH:mm:ss') | Should Be '2030-01-02 03:04:00'
        }
        finally { [System.Threading.Thread]::CurrentThread.CurrentCulture = $previousCulture }
    }

    It 'marks a configured empty target for repair without rewriting the file' {
        $json = '{"HasConfiguredTarget":true,"TargetLocal":""}'
        [System.IO.File]::WriteAllText($path, $json)
        $settings = Read-TargetSettings $path
        $settings.HasConfiguredTarget | Should Be $true
        $repairProperty.GetValue($settings) | Should Be $true
        $settings.TargetLocal | Should Be ''
        (Get-TargetHeading $settings) | Should Be '目标时间无效，请重新设置'
        (Get-TargetError $settings) | Should Not BeNullOrEmpty
        [System.IO.File]::ReadAllText($path) | Should Be $json
    }

    It 'marks invalid text for repair rather than returning the current time' {
        [System.IO.File]::WriteAllText($path,
            '{"HasConfiguredTarget":true,"TargetLocal":"not-a-date"}')
        $settings = Read-TargetSettings $path
        $settings.HasConfiguredTarget | Should Be $true
        $repairProperty.GetValue($settings) | Should Be $true
        (Get-TargetError $settings) | Should Not BeNullOrEmpty
        $source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\src\DesktopCountdown\MainWindow.cs') -Raw
        $source | Should Match '(?s)private void UpdateCountdown\(\).*?if \(!settings.HasConfiguredTarget \|\| settings.TargetNeedsRepair\).*?dayRun.Text = "---";'
    }

    It 'keeps an unconfigured legacy empty target as a first-run prompt' {
        [System.IO.File]::WriteAllText($path,
            '{"HasConfiguredTarget":false,"TargetLocal":""}')
        $settings = Read-TargetSettings $path
        $settings.HasConfiguredTarget | Should Be $false
        $repairProperty.GetValue($settings) | Should Be $false
        $settings.TargetLocal | Should Not BeNullOrEmpty
        (Get-TargetHeading $settings) | Should Be '双击设置目标时间'
    }

    It 'clears the repair state after a valid target is supplied' {
        [System.IO.File]::WriteAllText($path,
            '{"HasConfiguredTarget":true,"TargetLocal":"not-a-date"}')
        $settings = Read-TargetSettings $path
        $settings.TargetLocal = '2030-01-02 03:04:05'
        $settings.HasConfiguredTarget = $true
        $settings.Validate()
        $settings.HasConfiguredTarget | Should Be $true
        $repairProperty.GetValue($settings) | Should Be $false
        $settings.GetTargetLocal().Year | Should Be 2030
    }

    It 'retains the repair prompt after saving unrelated settings' {
        [System.IO.File]::WriteAllText($path,
            '{"HasConfiguredTarget":true,"TargetLocal":""}')
        $fileOps = New-TargetFileOps $path
        $settings = $fileOps.Load()
        $settings.Title = '更新标题'
        $fileOps.Save($settings)
        $reloaded = $fileOps.Load()
        $reloaded.HasConfiguredTarget | Should Be $true
        $repairProperty.GetValue($reloaded) | Should Be $true
        $reloaded.TargetLocal | Should Be ''
        (Get-TargetHeading $reloaded) | Should Be '目标时间无效，请重新设置'
    }
}
