[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('lint', 'typecheck', 'test', 'build', 'check')]
    [string]$Command = 'check'
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw 'PowerShell 7 or newer is required. Run: pwsh -File .\dev.ps1 check'
}
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildScript = Join-Path $projectRoot 'build.ps1'
$artifactRoot = Join-Path $projectRoot 'artifacts'

function Invoke-Lint {
    $paths = @(
        Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -File -Recurse
        Get-ChildItem -LiteralPath (Join-Path $projectRoot 'assets') -File -Recurse
        Get-ChildItem -LiteralPath (Join-Path $projectRoot 'tools') -File -Recurse -Filter '*.ps1'
        Get-ChildItem -LiteralPath (Join-Path $projectRoot 'tests') -File -Recurse -Filter '*.ps1'
        Get-Item -LiteralPath $buildScript
        Get-Item -LiteralPath (Join-Path $projectRoot 'dev.ps1')
        Get-Item -LiteralPath (Join-Path $projectRoot 'DesktopCountdown.csproj')
    ) | Where-Object { $_.Extension -in @('.cs', '.ps1', '.csproj', '.xaml', '.xml', '.manifest', '.svg') }

    foreach ($file in $paths) {
        $content = [System.IO.File]::ReadAllText($file.FullName)
        if ([regex]::IsMatch($content, '(?m)[ \t]+(?=\r?$)')) {
            throw "Trailing whitespace: $($file.FullName)"
        }
        if ($content.Length -gt 0 -and -not $content.EndsWith("`n")) {
            throw "Missing final newline: $($file.FullName)"
        }
        if ($file.Extension -eq '.ps1') {
            $tokens = $null
            $errors = $null
            [System.Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$errors) | Out-Null
            if ($errors.Count -gt 0) { throw "PowerShell parse error in $($file.FullName): $($errors[0])" }
        }
        if ($file.Extension -in @('.csproj', '.xaml', '.xml', '.manifest', '.svg')) {
            $reader = [System.Xml.XmlReader]::Create($file.FullName)
            try { while ($reader.Read()) { } }
            finally { $reader.Dispose() }
        }
    }
    Write-Output "LINT_OK=$($paths.Count) files"
}

function Invoke-Typecheck {
    & $buildScript -SkipSmokeTest -TreatWarningsAsErrors -OutputDirectory (Join-Path $artifactRoot 'baseline-typecheck')
    if ($LASTEXITCODE -ne 0) { throw 'Typecheck failed.' }
    Write-Output 'TYPECHECK_OK'
}

function Invoke-Test {
    $testDirectory = if ($env:DESKTOP_COUNTDOWN_TEST_MISSING_STARTUP -eq '1') { 'negative-startup-test' } else { 'baseline-test' }
    $testOutput = Join-Path $artifactRoot $testDirectory
    & $buildScript -SkipSmokeTest -OutputDirectory $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    Import-Module Pester -MinimumVersion 3.4 -ErrorAction Stop
    $expectedFiles = @('Baseline.Tests.ps1', 'TestDiscovery.Tests.ps1')
    $testFiles = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'tests') -File -Recurse -Filter '*.Tests.ps1' | Sort-Object FullName)
    $actualFiles = @($testFiles | ForEach-Object Name)
    $missingFiles = @($expectedFiles | Where-Object { $actualFiles -notcontains $_ })
    $unexpectedFiles = @($actualFiles | Where-Object { $expectedFiles -notcontains $_ })
    if ($actualFiles.Count -ne $expectedFiles.Count -or $missingFiles.Count -gt 0 -or $unexpectedFiles.Count -gt 0) {
        throw "测试文件清单不一致。缺少：$($missingFiles -join ', ')；意外出现：$($unexpectedFiles -join ', ')。"
    }
    $expectedTests = @(
        'runs the built-in smoke checks',
        'renders all theme previews',
        'writes task XML accepted by the Task Scheduler parser',
        'fails when the required startup module is missing',
        'returns a nonzero test command exit code when startup is missing'
    )
    $previous = $env:DESKTOP_COUNTDOWN_TEST_EXE
    try {
        $env:DESKTOP_COUNTDOWN_TEST_EXE = Join-Path $testOutput 'DesktopCountdown.exe'
        $result = Invoke-Pester -Script $testFiles.FullName -PassThru
        $actualTests = @($result.TestResult | ForEach-Object Name)
        $missingTests = @($expectedTests | Where-Object { $actualTests -notcontains $_ })
        $unexpectedTests = @($actualTests | Where-Object { $expectedTests -notcontains $_ })
        if ($result.FailedCount -gt 0 -or $result.PassedCount -ne $expectedTests.Count -or
            $result.TotalCount -ne $expectedTests.Count -or $actualTests.Count -ne $expectedTests.Count -or
            $missingTests.Count -gt 0 -or $unexpectedTests.Count -gt 0) {
            throw "测试失败或用例清单发生变化：通过 $($result.PassedCount)，失败 $($result.FailedCount)，总数 $($result.TotalCount)。缺少：$($missingTests -join ', ')；意外出现：$($unexpectedTests -join ', ')。"
        }
        Write-Output "TEST_OK=$($result.TotalCount)"
    }
    finally {
        $env:DESKTOP_COUNTDOWN_TEST_EXE = $previous
    }
}

function Invoke-Build {
    & $buildScript -OutputDirectory (Join-Path $artifactRoot 'baseline-build')
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    Write-Output 'BUILD_STAGE_OK'
}

switch ($Command) {
    'lint' { Invoke-Lint }
    'typecheck' { Invoke-Typecheck }
    'test' { Invoke-Test }
    'build' { Invoke-Build }
    'check' {
        Invoke-Lint
        Invoke-Typecheck
        Invoke-Test
        Invoke-Build
        Write-Output 'CHECK_OK'
    }
}
