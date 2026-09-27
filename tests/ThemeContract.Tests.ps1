$assembly = [System.Reflection.Assembly]::LoadFrom($env:DESKTOP_COUNTDOWN_TEST_EXE)
$catalogType = $assembly.GetType('DesktopCountdown.ThemeCatalog', $true)
$definitionType = $assembly.GetType('DesktopCountdown.ThemeDefinition', $true)
$decorationType = $assembly.GetType('DesktopCountdown.ThemeDecorations', $true)

Describe 'DesktopCountdown theme contract' {
    It 'preserves ten theme keys and display names' {
        $themes = @($catalogType.GetMethod('All').Invoke($null, @()))
        $themes.Count | Should Be 10
        ($themes | ForEach-Object { $_.Key + ':' + $_.DisplayName }) -join '|' |
            Should Be 'MinimalGlass:极简玻璃|Air:现代留白|Editorial:编辑部海报|Dusk:暮色星轨|Ink:东方墨韵|DeepSea:深海夜航|Porcelain:青瓷晨雾|Neon:霓虹夜行|AmberFilm:琥珀胶片|Blueprint:蓝图刻度'
        $catalogType.GetMethod('Get').Invoke($null, [object[]]@('unknown')).Key | Should Be 'MinimalGlass'
    }

    It 'keeps one public definition without a reverse catalog dependency' {
        $definitionType.IsPublic | Should Be $true
        $definitionType.GetProperty('Key').PropertyType | Should Be ([string])
        $definitionType.GetProperty('DigitWeight').PropertyType.FullName | Should Be 'System.Windows.FontWeight'
        $parameters = $decorationType.GetMethod('Apply').GetParameters()
        $parameters[1].ParameterType | Should Be $definitionType
        $catalogSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\src\DesktopCountdown\ThemeCatalog.cs') -Raw
        $decorationsSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\src\DesktopCountdown\ThemeDecorations.cs') -Raw
        $definitionSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\src\DesktopCountdown\ThemeDefinition.cs') -Raw
        $catalogSource | Should Not Match 'class ThemeDefinition'
        $decorationsSource | Should Not Match 'ThemeCatalog\.'
        $definitionSource | Should Match 'public sealed class ThemeDefinition'
    }
}
