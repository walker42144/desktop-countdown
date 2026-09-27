$assembly = [System.Reflection.Assembly]::LoadFrom($env:DESKTOP_COUNTDOWN_TEST_EXE)
$backendType = $assembly.GetType('DesktopCountdown.StartupBackend', $true)
$serviceType = $assembly.GetType('DesktopCountdown.StartupService', $true)
$applyMethod = $serviceType.GetMethod('ApplyWithBackend', [System.Reflection.BindingFlags]'Static,NonPublic')

function New-FakeStartup {
    $state = @{ Registry = $null; Task = $null; FailTaskWrite = $false; FailTaskDelete = $false; FailRegistryWrite = $false }
    $readRegistry = [Func[object]]({ return $state.Registry }.GetNewClosure())
    $writeRegistry = [Action[string]]({
        param($command)
        if ($state.FailRegistryWrite -and $null -ne $command) { throw 'injected registry failure' }
        $state.Registry = $command
    }.GetNewClosure())
    $readTask = [Func[object]]({ return $state.Task }.GetNewClosure())
    $writeTask = [Action[string]]({
        param($xml)
        if ($state.FailTaskWrite) { throw 'injected task creation failure' }
        $state.Task = $xml
    }.GetNewClosure())
    $deleteTask = [Action]({
        if ($state.FailTaskDelete) { throw 'injected task deletion failure' }
        $state.Task = $null
    }.GetNewClosure())
    $constructor = @($backendType.GetConstructors([System.Reflection.BindingFlags]'Instance,Public,NonPublic') |
        Where-Object { $_.GetParameters().Count -eq 5 })[0]
    $arguments = [object[]]::new(5)
    $arguments[0] = $readRegistry
    $arguments[1] = $writeRegistry
    $arguments[2] = $readTask
    $arguments[3] = $writeTask
    $arguments[4] = $deleteTask
    return [pscustomobject]@{ Backend = $constructor.Invoke($arguments); State = $state }
}

function Invoke-Startup([object]$fake, [bool]$enabled, [string]$mode, [scriptblock]$save = $null) {
    $saveAction = if ($null -eq $save) { $null } else { [Action]$save }
    $arguments = [object[]]::new(5)
    $arguments[0] = $fake.Backend
    $arguments[1] = $enabled
    $arguments[2] = $mode
    $arguments[3] = [string]'C:\Test\DesktopCountdown.exe'
    $arguments[4] = $saveAction
    $applyMethod.Invoke($null, $arguments) | Out-Null
}

function Get-StartupError([scriptblock]$action) {
    try { $null = & $action; return $null }
    catch { return $_.Exception }
}

Describe 'DesktopCountdown startup consistency' {
    It 'keeps the public Apply signature unchanged' {
        $method = $serviceType.GetMethod('Apply', [System.Reflection.BindingFlags]'Static,Public')
        $method.ReturnType | Should Be ([void])
        $parameters = @($method.GetParameters() | ForEach-Object { $_.ParameterType.FullName })
        ($parameters -join ',') | Should Be 'System.Boolean,System.String,System.String'
    }

    It 'distinguishes a missing task from other query failures' {
        $method = $backendType.GetMethod('IsMissingTask', [System.Reflection.BindingFlags]'Static,NonPublic')
        $missing = [System.IO.FileNotFoundException]::new()
        $denied = [System.UnauthorizedAccessException]::new()
        $arguments = [object[]]::new(1)
        $arguments[0] = $missing
        $method.Invoke($null, $arguments) | Should Be $true
        $arguments[0] = $denied
        $method.Invoke($null, $arguments) | Should Be $false
    }

    It 'switches between task and registry without duplicate entries' {
        $fake = New-FakeStartup
        $fake.State.Registry = '"C:\Old.exe"'
        Invoke-Startup $fake $true 'Task'
        $fake.State.Registry | Should BeNullOrEmpty
        $fake.State.Task | Should Match 'C:\\Test\\DesktopCountdown.exe'
        Invoke-Startup $fake $true 'Registry'
        $fake.State.Task | Should BeNullOrEmpty
        $fake.State.Registry | Should Be '"C:\Test\DesktopCountdown.exe"'
        Invoke-Startup $fake $false 'Registry'
        $fake.State.Task | Should BeNullOrEmpty
        $fake.State.Registry | Should BeNullOrEmpty
    }

    It 'restores registry state when task creation fails' {
        $fake = New-FakeStartup
        $fake.State.Registry = '"C:\Old.exe"'
        $fake.State.FailTaskWrite = $true
        (Get-StartupError { Invoke-Startup $fake $true 'Task' }) | Should Not BeNullOrEmpty
        $fake.State.Registry | Should Be '"C:\Old.exe"'
        $fake.State.Task | Should BeNullOrEmpty
    }

    It 'restores task state when task deletion fails' {
        $fake = New-FakeStartup
        Invoke-Startup $fake $true 'Task'
        $original = $fake.State.Task
        $fake.State.FailTaskDelete = $true
        (Get-StartupError { Invoke-Startup $fake $true 'Registry' }) | Should Not BeNullOrEmpty
        $fake.State.Task | Should Be $original
        $fake.State.Registry | Should BeNullOrEmpty
    }

    It 'restores task state when registry write fails' {
        $fake = New-FakeStartup
        Invoke-Startup $fake $true 'Task'
        $original = $fake.State.Task
        $fake.State.FailRegistryWrite = $true
        (Get-StartupError { Invoke-Startup $fake $true 'Registry' }) | Should Not BeNullOrEmpty
        $fake.State.Task | Should Be $original
        $fake.State.Registry | Should BeNullOrEmpty
    }

    It 'restores startup state when configuration save fails' {
        $fake = New-FakeStartup
        $fake.State.Registry = '"C:\Old.exe"'
        $error = Get-StartupError { Invoke-Startup $fake $true 'Task' { throw 'injected settings failure' } }
        $error | Should Not BeNullOrEmpty
        $error.ToString() | Should Match 'injected settings failure'
        $fake.State.Registry | Should Be '"C:\Old.exe"'
        $fake.State.Task | Should BeNullOrEmpty
    }
}
