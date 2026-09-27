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
    $expectedFiles = @('AccurateClock.Tests.ps1', 'Baseline.Tests.ps1', 'BuildInventory.Tests.ps1', 'DiagnosticLog.Tests.ps1', 'DisplayFormats.Tests.ps1', 'MotionSession.Tests.ps1', 'SettingsDefaults.Tests.ps1', 'SettingsLayout.Tests.ps1', 'SettingsStore.Tests.ps1', 'StartupService.Tests.ps1', 'TargetTime.Tests.ps1', 'TestDiscovery.Tests.ps1', 'ThemeContract.Tests.ps1', 'WallpaperColor.Tests.ps1', 'WindowBehavior.Tests.ps1')
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
        'uses the project compile list for every source file',
        'rejects a source file omitted from the project list',
        'rejects duplicate project compile entries',
        'rejects project entries whose source file is missing',
        'removes desktop parenting while retaining click-through',
        'applies and clears click-through bits without changing unrelated styles',
        'rolls back a failed click-through write and ignores an unreadable window',
        'normalizes the legacy desktop-mode setting to ordinary mode',
        'detects a window outside both monitor working areas',
        'rotates into one bounded archive',
        'does not persist exception messages or path-shaped contexts',
        'returns failure without throwing when the directory is unwritable',
        'does not create a file for a missing exception',
        'preserves ten theme keys and display names',
        'keeps one public definition without a reverse catalog dependency',
        'keeps fixed-image colors and reuses the same region without decoding',
        'reuses the image but resamples after the widget moves',
        'reloads after wallpaper contents change or preferences invalidate',
        'keeps default countdown text and its three display parts',
        'keeps elapsed sign and day padding at the boundary',
        'hides seconds only for the default format and changes refresh need',
        'uses rendered tokens for custom refresh including quoted and escaped text',
        'keeps custom sign and total-hours formatting',
        'keeps the public planner contract and owns window motion state',
        'starts pauses and resumes only when motion is active',
        'preserves the anchor while applying and resets after a trial',
        'keeps step order and interpolates a move',
        'keeps all six planner routes independent of the mutable public catalog',
        'preserves default JSON fields and values',
        'preserves legacy JSON values and supplies new defaults',
        'keeps public constants and removes display dependencies from settings',
        'validates motion identifiers independently of the public mode array',
        'keeps internal motion labels and selection stable when public arrays change',
        'keeps the public settings window contract',
        'splits grouped layout and bindings out of the constructor',
        'closes the nonmodal preview from its cancel button',
        'renders both light and dark settings screenshots',
        'uses defaults only when settings and recovery files are absent',
        'loads a valid file and creates a byte-identical backup on save',
        'reports corrupt settings without changing original or backup bytes',
        'refuses to save over a corrupt primary and preserves an existing backup',
        'reports interrupted first save instead of hiding the temporary file',
        'preserves original and backup bytes if replacement fails',
        'does not treat an unreadable existing file as first run',
        'keeps the public Apply signature unchanged',
        'retains the legacy registry-only startup signature without touching tasks',
        'distinguishes a missing task from other query failures',
        'switches between task and registry without duplicate entries',
        'restores registry state when task creation fails',
        'restores task state when task deletion fails',
        'restores task state when registry write fails',
        'restores startup state when configuration save fails',
        'accepts a valid response and records a bounded offset',
        'rejects a short response without changing the offset',
        'rejects invalid server mode and stratum',
        'rejects a response that does not match the request',
        'keeps the prior calibration after a timeout',
        'rejects an excessive offset',
        'keeps calibrated time stable across a system clock jump',
        'preserves a valid exact local target and its JSON',
        'continues to parse a legacy culture-specific target',
        'marks a configured empty target for repair without rewriting the file',
        'marks invalid text for repair rather than returning the current time',
        'keeps an unconfigured legacy empty target as a first-run prompt',
        'clears the repair state after a valid target is supplied',
        'retains the repair prompt after saving unrelated settings',
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
