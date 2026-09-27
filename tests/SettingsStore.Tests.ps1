$assembly = [System.Reflection.Assembly]::LoadFrom($env:DESKTOP_COUNTDOWN_TEST_EXE)
$opsType = $assembly.GetType('DesktopCountdown.SettingsFileOps', $true)
$settingsType = $assembly.GetType('DesktopCountdown.AppSettings', $true)

function New-FileOps([string]$path, [Action[string, string, string]]$replace = $null) {
    $count = if ($null -eq $replace) { 1 } else { 2 }
    $constructor = @($opsType.GetConstructors([System.Reflection.BindingFlags]'Instance,Public,NonPublic') |
        Where-Object { $_.GetParameters().Count -eq $count })[0]
    $arguments = [object[]]::new($count)
    $arguments[0] = [string]$path
    if ($count -eq 2) { $arguments[1] = $replace }
    return $constructor.Invoke($arguments)
}

function New-Settings {
    return $settingsType.GetMethod('CreateDefault').Invoke($null, @())
}

function Get-Bytes([string]$path) {
    return [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($path))
}

function Get-ThrownException([scriptblock]$action) {
    try { $null = & $action; return $null }
    catch { return $_.Exception }
}

Describe 'DesktopCountdown settings file recovery' {
    BeforeEach {
        $ErrorActionPreference = 'Stop'
        $directory = Join-Path ([System.IO.Path]::GetTempPath()) ('DesktopCountdown-Test-' + [Guid]::NewGuid().ToString('N'))
        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
        $path = Join-Path $directory 'settings.json'
    }

    AfterEach {
        [System.IO.Directory]::Delete($directory, $true)
    }

    It 'uses defaults only when settings and recovery files are absent' {
        $settings = (New-FileOps $path).Load()
        $settings.Title | Should Be '距离目标时刻还有'
        [System.IO.File]::Exists($path) | Should Be $false
    }

    It 'loads a valid file and creates a byte-identical backup on save' {
        $ops = New-FileOps $path
        $first = New-Settings
        $first.Title = '首次设置'
        $ops.Save($first)
        $original = Get-Bytes $path
        $ops.Load().Title | Should Be '首次设置'
        $second = New-Settings
        $second.Title = '更新设置'
        $ops.Save($second)
        $ops.Load().Title | Should Be '更新设置'
        (Get-Bytes ($path + '.bak')) | Should Be $original
    }

    It 'reports corrupt settings without changing original or backup bytes' {
        [System.IO.File]::WriteAllBytes($path, [byte[]](1, 2, 3))
        [System.IO.File]::WriteAllBytes(($path + '.bak'), [byte[]](4, 5, 6))
        $original = Get-Bytes $path
        $backup = Get-Bytes ($path + '.bak')
        (Get-ThrownException { (New-FileOps $path).Load() }) | Should Not BeNullOrEmpty
        (Get-Bytes $path) | Should Be $original
        (Get-Bytes ($path + '.bak')) | Should Be $backup
    }

    It 'refuses to save over a corrupt primary and preserves an existing backup' {
        [System.IO.File]::WriteAllBytes($path, [byte[]](1, 2, 3))
        $source = New-FileOps (Join-Path $directory 'valid.json')
        $source.Save((New-Settings))
        [System.IO.File]::Copy((Join-Path $directory 'valid.json'), ($path + '.bak'))
        $original = Get-Bytes $path
        $backup = Get-Bytes ($path + '.bak')
        (Get-ThrownException { (New-FileOps $path).Save((New-Settings)) }) | Should Not BeNullOrEmpty
        (Get-Bytes $path) | Should Be $original
        (Get-Bytes ($path + '.bak')) | Should Be $backup
    }

    It 'reports interrupted first save instead of hiding the temporary file' {
        [System.IO.File]::WriteAllBytes(($path + '.tmp'), [byte[]](7, 8, 9))
        $temporary = Get-Bytes ($path + '.tmp')
        (Get-ThrownException { (New-FileOps $path).Load() }) | Should Not BeNullOrEmpty
        (Get-Bytes ($path + '.tmp')) | Should Be $temporary
        [System.IO.File]::Exists($path) | Should Be $false
    }

    It 'preserves original and backup bytes if replacement fails' {
        $normal = New-FileOps $path
        $normal.Save((New-Settings))
        [System.IO.File]::WriteAllBytes(($path + '.bak'), [byte[]](10, 11, 12))
        $original = Get-Bytes $path
        $backup = Get-Bytes ($path + '.bak')
        $failure = [Action[string, string, string]] { param($source, $destination, $backupPath) throw 'injected replacement failure' }
        $settings = New-Settings
        $settings.Title = '待保存设置'
        (Get-ThrownException { (New-FileOps $path $failure).Save($settings) }) | Should Not BeNullOrEmpty
        (Get-Bytes $path) | Should Be $original
        (Get-Bytes ($path + '.bak')) | Should Be $backup
        [System.IO.File]::Exists(($path + '.tmp')) | Should Be $true
    }

    It 'does not treat an unreadable existing file as first run' {
        [System.IO.File]::WriteAllBytes($path, [byte[]](1, 2, 3))
        $handle = [System.IO.File]::Open($path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
        try { (Get-ThrownException { (New-FileOps $path).Load() }) | Should Not BeNullOrEmpty }
        finally { $handle.Dispose() }
    }
}
