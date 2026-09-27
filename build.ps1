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

function Resolve-CompileSources {
    param(
        [System.Xml.XmlDocument]$ProjectXml,
        [string]$ProjectRoot,
        [string]$SourceRoot
    )

    $namespace = New-Object System.Xml.XmlNamespaceManager($ProjectXml.NameTable)
    $namespace.AddNamespace('msb', $ProjectXml.DocumentElement.NamespaceURI)
    $entries = @($ProjectXml.SelectNodes('//msb:Compile', $namespace))
    if ($entries.Count -eq 0) { throw '项目文件没有列出任何 C# 源码。' }

    $sourcePrefix = [System.IO.Path]::GetFullPath($SourceRoot).TrimEnd([char[]]@('\', '/')) + [System.IO.Path]::DirectorySeparatorChar
    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $sources = @()
    foreach ($entry in $entries) {
        $include = $entry.GetAttribute('Include')
        if ([string]::IsNullOrWhiteSpace($include) -or [System.IO.Path]::IsPathRooted($include)) {
            throw "项目源码路径无效：$include"
        }
        $source = [System.IO.Path]::GetFullPath((Join-Path $ProjectRoot $include))
        if (-not $source.StartsWith($sourcePrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
            [System.IO.Path]::GetExtension($source) -ne '.cs') {
            throw "项目源码路径不在源码目录内：$include"
        }
        if (-not $seen.Add($source)) { throw "项目源码清单包含重复项：$include" }
        if (-not [System.IO.File]::Exists($source)) { throw "项目源码文件不存在：$include" }
        $sources += $source
    }

    $diskFiles = @(Get-ChildItem -LiteralPath $SourceRoot -Filter '*.cs' -File -Recurse -Force | ForEach-Object FullName)
    foreach ($file in $diskFiles) {
        if (-not $seen.Contains($file)) { throw "源码文件未列入项目清单：$file" }
    }
    $sources | Sort-Object
}

if (-not (Test-Path -LiteralPath $compiler)) {
    throw '未找到系统 C# 编译器。请安装 .NET Framework 4.8 开发工具或 .NET SDK。'
}

$projectFile = Join-Path $projectRoot 'DesktopCountdown.csproj'
$projectXml = New-Object System.Xml.XmlDocument
$projectXml.Load($projectFile)
$sources = @(Resolve-CompileSources -ProjectXml $projectXml -ProjectRoot $projectRoot -SourceRoot $sourceRoot)

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
& $brandGenerator -ProjectRoot $projectRoot
if (-not (Test-Path -LiteralPath $applicationIcon)) { throw '应用图标生成失败。' }
$output = Join-Path $artifactRoot 'DesktopCountdown.exe'
$pdb = Join-Path $artifactRoot 'DesktopCountdown.pdb'
$manifest = Join-Path $sourceRoot 'app.manifest'

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
    "/resource:$applicationIcon,DesktopCountdown.Brand.ico"
    "/resource:$(Join-Path $projectRoot 'assets\ComboBoxTemplate.xaml'),DesktopCountdown.ComboBoxTemplate.xaml"
    "/resource:$(Join-Path $projectRoot 'assets\CheckBoxStyle.xaml'),DesktopCountdown.CheckBoxStyle.xaml"
    "/resource:$(Join-Path $projectRoot 'assets\ScrollBarStyle.xaml'),DesktopCountdown.ScrollBarStyle.xaml"
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
