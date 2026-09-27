[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$ApplicationPath)

$ErrorActionPreference = 'Stop'
$application = [System.IO.Path]::GetFullPath($ApplicationPath)
$taskName = 'DesktopCountdown_EncodingTest_' + [Guid]::NewGuid().ToString('N')
$xmlPath = Join-Path ([System.IO.Path]::GetTempPath()) ($taskName + '.xml')
$created = $false

try {
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

    $service = New-Object -ComObject 'Schedule.Service'
    $service.Connect()
    $definition = $service.NewTask(0)
    $definition.XmlText = $xml
    Write-Output 'TASK_XML_PARSE_OK'

    $createOutput = & schtasks.exe /Create /F /TN $taskName /XML $xmlPath 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Temporary task creation failed: $createOutput" }
    $created = $true
    $queryOutput = & schtasks.exe /Query /TN $taskName 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Temporary task query failed: $queryOutput" }
    Write-Output 'TASK_REGISTER_OK'
}
finally {
    if ($created) {
        $deleteOutput = & schtasks.exe /Delete /F /TN $taskName 2>&1
        if ($LASTEXITCODE -ne 0) { Write-Warning "Temporary task cleanup failed: $deleteOutput" }
        else { Write-Output 'TASK_CLEANUP_OK' }
    }
    if ([System.IO.File]::Exists($xmlPath)) { [System.IO.File]::Delete($xmlPath) }
}
