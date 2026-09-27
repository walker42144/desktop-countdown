# 项目开发约定

## 技术栈与依赖

- Windows 桌面应用：C#、WPF、.NET Framework 4.8；项目文件为旧式 `DesktopCountdown.csproj`。
- 构建：Windows 自带的 .NET Framework `csc.exe`，入口为 `build.ps1`。
- 命令入口要求 PowerShell 7；测试使用本机可导入的 Pester 3.4 或兼容版本，加上应用内置的冒烟检查。旧版 Windows PowerShell 5.1 无法可靠解析现有无 BOM 中文脚本。
- 当前没有 NuGet、npm 或其他包管理器依赖；不要为小改动引入新的包管理体系。
- 当前开发环境没有 .NET SDK。`.editorconfig` 供编辑器格式化使用，`lint` 只强制空白字符、文件结尾、PowerShell 语法与 XML 结构，不等同于完整 C# 分析器。

## 目录

- `src/DesktopCountdown/`：应用入口、窗口、显示格式、计时、主题、启动服务等 C# 源码。
- `assets/`：图标与 WPF 样式资源；品牌资源由 `tools/Generate-BrandAssets.ps1` 生成。
- `tools/`：构建辅助脚本和可选的启动任务集成验证脚本。
- `tests/`：Pester 基线测试；常规测试不得修改用户的正式启动项。
- `docs/`：设计资料与验证记录。
- `artifacts/`：本地构建和测试产物，已被 Git 忽略。

## 依赖方向

- `Program` 负责启动和组合；`MainWindow`、`SettingsWindow` 可以调用服务与格式化模块。
- 服务、计算和格式化模块不得反向依赖具体窗口或测试代码。
- `tests/` 与 `tools/` 可以验证应用，但应用运行时不得依赖测试或构建脚本。
- 保持现有 .NET Framework 4.8 兼容性，不擅自改为 SDK 风格项目或升级运行时。

## 编码规则

- 用户可见文字使用简体中文；类型、成员、文件名与代码注释使用英文。
- 遵守 `.editorconfig`：UTF-8、4 空格缩进、无行尾空白、文件以换行结尾；保留现有文件的内容和排版，避免全仓格式化。
- 优先修改与任务直接相关的文件，不顺手更改业务行为或公开数据格式。
- 新增验证必须可重复运行；常规 `test` 不创建或删除正式计划任务，也不依赖网络。
- 如果命令失败且修复范围超出当前任务，在 `docs/baseline.md` 记录现象，不做无关修复。

## 必跑命令

每次修改后，在 PowerShell 7 中从项目根目录运行 `./dev.ps1 check`。这个入口依次执行 `lint`、`typecheck`、`test`、`build`；也可单独运行 `./dev.ps1 lint`、`./dev.ps1 typecheck`、`./dev.ps1 test`、`./dev.ps1 build`。

`tools/Test-StartupTask.ps1` 会创建并删除一次性 Windows 计划任务，仅在需要实际注册验证时手动运行，不纳入常规 `check`。
