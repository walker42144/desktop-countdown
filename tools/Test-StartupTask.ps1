[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$ApplicationPath)

$ErrorActionPreference = 'Stop'
$application = [System.IO.Path]::GetFullPath($ApplicationPath)
$taskName = 'RefactorTest_' + [Guid]::NewGuid().ToString('N')
$xmlPath = Join-Path ([System.IO.Path]::GetTempPath()) ($taskName + '.xml')
$preflightClear = $false

if ($taskName -cnotmatch '^RefactorTest_[0-9a-f]{32}$') { throw 'Temporary task name is not unique and scoped.' }

function Test-TemporaryTaskExists {
    param([object]$folder, [string]$name)
    try {
        $task = $folder.GetTask($name)
        if ($null -ne $task -and [System.Runtime.InteropServices.Marshal]::IsComObject($task)) {
            [System.Runtime.InteropServices.Marshal]::ReleaseComObject($task) | Out-Null
        }
        return $true
    }
    catch {
        $exception = $_.Exception
        while ($null -ne $exception) {
            if ($exception.HResult -eq -2147024894) { return $false }
            $exception = $exception.InnerException
        }
        throw
    }
}

try {
    $service = New-Object -ComObject 'Schedule.Service'
    $service.Connect()
    $folder = $service.GetFolder('\')
    if (Test-TemporaryTaskExists $folder $taskName) { throw "Temporary task already exists; refusing to overwrite: $taskName" }
    $preflightClear = $true
    Write-Output 'TASK_PREFLIGHT_CLEAR'
    Write-Output "TEST_TASK_NAME=$taskName"

    $assembly = [System.Reflection.Assembly]::LoadFrom($application)
    $type = $assembly.GetType('DesktopCountdown.StartupService', $true)
    $flags = [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Static
    $writeXml = $type.GetMethod('WriteTaskXml', $flags)
    if ($null -eq $writeXml) { throw 'Task XML writer was not found.' }
    $invokeArgs = New-Object 'System.Object[]' 2
    $invokeArgs[0] = [string]$xmlPath
    $invokeArgs[1] = [string](Join-Path $env:WINDIR 'System32\notepad.exe')
    $writeXml.Invoke($null, $invokeArgs) | Out-Null

    $bytes = [System.IO.File]::ReadAllBytes($xmlPath)
    if ($bytes.Length -lt 2 -or $bytes[0] -ne 0xFF -or $bytes[1] -ne 0xFE) {
        throw 'Task XML has no UTF-16 LE byte order mark.'
    }
    $xml = [System.IO.File]::ReadAllText($xmlPath, [System.Text.Encoding]::Unicode)
    if (-not $xml.StartsWith('<?xml version="1.0" encoding="utf-16"?>')) {
        throw 'Task XML encoding declaration is incorrect.'
    }

    $definition = $service.NewTask(0)
    $definition.XmlText = $xml
    Write-Output 'TASK_XML_PARSE_OK'

    $createOutput = & schtasks.exe /Create /TN $taskName /XML $xmlPath 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Temporary task creation failed: $createOutput" }
    $queryOutput = & schtasks.exe /Query /TN $taskName 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Temporary task query failed: $queryOutput" }
    if (-not (Test-TemporaryTaskExists $folder $taskName)) { throw 'Temporary task was not found after creation.' }
    Write-Output 'TASK_REGISTER_OK'
}
finally {
    try {
        if ($preflightClear) {
            if (Test-TemporaryTaskExists $folder $taskName) {
                $deleteOutput = & schtasks.exe /Delete /F /TN $taskName 2>&1
                if ($LASTEXITCODE -ne 0) { throw "Temporary task cleanup failed: $deleteOutput" }
            }
            if (Test-TemporaryTaskExists $folder $taskName) { throw "Temporary task remains after cleanup: $taskName" }
            if (Test-TemporaryTaskExists $folder $taskName) { throw "Temporary task failed second cleanup check: $taskName" }
            Write-Output 'TASK_CLEANUP_VERIFIED_TWICE'
        }
    }
    finally {
        if ([System.IO.File]::Exists($xmlPath)) { [System.IO.File]::Delete($xmlPath) }
    }
}
