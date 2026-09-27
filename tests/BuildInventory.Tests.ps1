$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$projectFile = Join-Path $projectRoot 'DesktopCountdown.csproj'
$sourceRoot = Join-Path $projectRoot 'src\DesktopCountdown'
$buildScript = Join-Path $projectRoot 'build.ps1'

# Load the inventory function without running the compiler or asset generator.
$tokens = $null
$parseErrors = $null
$syntaxTree = [System.Management.Automation.Language.Parser]::ParseFile($buildScript, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) { throw "Build script parse error: $($parseErrors[0])" }
$definition = @($syntaxTree.FindAll({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Resolve-CompileSources'
}, $true))
if ($definition.Count -ne 1) { throw 'Expected exactly one Resolve-CompileSources function.' }
. ([ScriptBlock]::Create($definition[0].Extent.Text))

Describe 'DesktopCountdown build inventory' {
    It 'uses the project compile list for every source file' {
        $projectXml = New-Object System.Xml.XmlDocument
        $projectXml.Load($projectFile)
        $sources = @(Resolve-CompileSources -ProjectXml $projectXml -ProjectRoot $projectRoot -SourceRoot $sourceRoot)
        $diskFiles = @(Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -File -Recurse -Force | ForEach-Object FullName)
        $sources.Count | Should Be $diskFiles.Count
        foreach ($file in $diskFiles) { ($sources -contains $file) | Should Be $true }
    }

    It 'rejects a source file omitted from the project list' {
        $projectXml = New-Object System.Xml.XmlDocument
        $projectXml.Load($projectFile)
        $namespace = New-Object System.Xml.XmlNamespaceManager($projectXml.NameTable)
        $namespace.AddNamespace('msb', $projectXml.DocumentElement.NamespaceURI)
        $entry = $projectXml.SelectSingleNode('//msb:Compile', $namespace)
        $entry.ParentNode.RemoveChild($entry) | Out-Null
        $message = $null
        try { Resolve-CompileSources -ProjectXml $projectXml -ProjectRoot $projectRoot -SourceRoot $sourceRoot | Out-Null }
        catch { $message = $_.Exception.Message }
        $message | Should Match '未列入'
    }

    It 'rejects duplicate project compile entries' {
        $projectXml = New-Object System.Xml.XmlDocument
        $projectXml.Load($projectFile)
        $namespace = New-Object System.Xml.XmlNamespaceManager($projectXml.NameTable)
        $namespace.AddNamespace('msb', $projectXml.DocumentElement.NamespaceURI)
        $entry = $projectXml.SelectSingleNode('//msb:Compile', $namespace)
        $entry.ParentNode.AppendChild($entry.CloneNode($true)) | Out-Null
        $message = $null
        try { Resolve-CompileSources -ProjectXml $projectXml -ProjectRoot $projectRoot -SourceRoot $sourceRoot | Out-Null }
        catch { $message = $_.Exception.Message }
        $message | Should Match '重复'
    }

    It 'rejects project entries whose source file is missing' {
        $projectXml = New-Object System.Xml.XmlDocument
        $projectXml.Load($projectFile)
        $namespace = New-Object System.Xml.XmlNamespaceManager($projectXml.NameTable)
        $namespace.AddNamespace('msb', $projectXml.DocumentElement.NamespaceURI)
        $entry = $projectXml.SelectSingleNode('//msb:Compile', $namespace)
        $entry.Attributes['Include'].Value = 'src\DesktopCountdown\MissingForInventory.cs'
        $message = $null
        try { Resolve-CompileSources -ProjectXml $projectXml -ProjectRoot $projectRoot -SourceRoot $sourceRoot | Out-Null }
        catch { $message = $_.Exception.Message }
        $message | Should Match '不存在'
    }
}
