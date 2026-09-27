[CmdletBinding()]
param(
    [switch]$SkipSmokeTest,
    [switch]$TreatWarningsAsErrors,
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceRoot = Join-Path $projectRoot 'src\DesktopCountdown'
$artifactRoot = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    Join-Path $projectRoot 'artifacts'
} else {
    [System.IO.Path]::GetFullPath($OutputDirectory)
}
$frameworkRoot = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpfRoot = Join-Path $frameworkRoot 'WPF'
$compiler = Join-Path $frameworkRoot 'csc.exe'
$brandGenerator = Join-Path $projectRoot 'tools\Generate-BrandAssets.ps1'
$applicationIcon = Join-Path $projectRoot 'assets\DesktopCountdown.ico'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw '未找到系统 C# 编译器。请安装 .NET Framework 4.8 开发工具或 .NET SDK。'
}

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
& $brandGenerator -ProjectRoot $projectRoot
if (-not (Test-Path -LiteralPath $applicationIcon)) { throw '应用图标生成失败。' }
$output = Join-Path $artifactRoot 'DesktopCountdown.exe'
$pdb = Join-Path $artifactRoot 'DesktopCountdown.pdb'
$manifest = Join-Path $sourceRoot 'app.manifest'
$sources = Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' | Sort-Object Name | ForEach-Object FullName

$references = @(
    (Join-Path $frameworkRoot 'System.dll'),
    (Join-Path $frameworkRoot 'System.Core.dll'),
    (Join-Path $frameworkRoot 'System.Drawing.dll'),
    (Join-Path $frameworkRoot 'System.Runtime.Serialization.dll'),
    (Join-Path $frameworkRoot 'System.Windows.Forms.dll'),
    (Join-Path $frameworkRoot 'System.Xaml.dll'),
    (Join-Path $wpfRoot 'WindowsBase.dll'),
    (Join-Path $wpfRoot 'PresentationCore.dll'),
    (Join-Path $wpfRoot 'PresentationFramework.dll')
)

$compilerArguments = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/debug:pdbonly',
    "/out:$output",
    "/pdb:$pdb",
    "/win32manifest:$manifest"
    "/win32icon:$applicationIcon"
)
if ($TreatWarningsAsErrors) { $compilerArguments += '/warnaserror+' }
$compilerArguments += $references | ForEach-Object { "/reference:$_" }
$compilerArguments += $sources

& $compiler @compilerArguments
if ($LASTEXITCODE -ne 0) { throw "编译失败，退出代码：$LASTEXITCODE" }

if (-not $SkipSmokeTest) {
    $smokePath = Join-Path $artifactRoot 'smoke-test.txt'
    if (Test-Path -LiteralPath $smokePath) { Remove-Item -LiteralPath $smokePath -Force }
    $process = Start-Process -FilePath $output -ArgumentList '--smoke-test' -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) { throw "冒烟测试失败，退出代码：$($process.ExitCode)" }
    if (-not (Test-Path -LiteralPath $smokePath)) { throw '冒烟测试未生成结果文件。' }
    $smoke = Get-Content -LiteralPath $smokePath -Raw
    if (-not $smoke.StartsWith('OK')) { throw "冒烟测试未通过：`n$smoke" }
    $previewResult = Join-Path $artifactRoot 'previews\render-result.txt'
    $previewProcess = Start-Process -FilePath $output -ArgumentList '--render-previews' -Wait -PassThru -WindowStyle Hidden
    if ($previewProcess.ExitCode -ne 0) { throw "主题预览渲染失败，退出代码：$($previewProcess.ExitCode)" }
    if (-not (Test-Path -LiteralPath $previewResult)) { throw '主题预览未生成验证结果。' }
}

$file = Get-Item -LiteralPath $output
Write-Output "BUILD_OK=$($file.FullName)"
Write-Output "SIZE_BYTES=$($file.Length)"
if (-not $SkipSmokeTest) { Get-Content -LiteralPath (Join-Path $artifactRoot 'smoke-test.txt') }
if (-not $SkipSmokeTest) { Get-Content -LiteralPath (Join-Path $artifactRoot 'previews\render-result.txt') }
