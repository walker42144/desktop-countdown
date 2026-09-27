# 验证基线（2026-09-27）

## 环境与范围

- Windows 本机；C# WPF、.NET Framework 4.8；使用系统 `csc.exe` 构建，没有 NuGet 依赖。
- PowerShell 7 执行统一入口；测试框架为本机 Pester 3.4.0。
- 本机没有 .NET SDK，因此不能运行 `dotnet format` 或 SDK 内置的 C# 分析器；此次没有安装新工具或修改业务逻辑。
- 验证时工作区已存在未提交的应用与文档改动。以下结果对应当前工作区，并非某个已提交版本。

## 命令结果

从项目根目录执行，五条命令均以退出码 0 结束：

| 命令 | 结果 | 实际覆盖 |
| --- | --- | --- |
| `./dev.ps1 lint` | 通过；28 个文件 | 行尾空白、末尾换行、PowerShell 语法、XML 结构；遵循 `.editorconfig` 的最小格式基线。 |
| `./dev.ps1 typecheck` | 通过 | 使用 .NET Framework 编译器编译全部 C# 源码，警告视为错误；产物在 `artifacts/baseline-typecheck/`。 |
| `./dev.ps1 test` | 通过；当前工作区 Pester 3/3 | 应用内置冒烟检查、主题预览渲染；当前工作区还包含未提交的 `StartupService.cs`，因此额外验证了任务 XML 编码及任务计划程序内存解析。产物在 `artifacts/baseline-test/`。 |
| `./dev.ps1 build` | 通过 | 完整构建、内置冒烟检查与主题预览渲染；版本 `0.3.0.0`，产物在 `artifacts/baseline-build/`。 |
| `./dev.ps1 check` | 通过 | 依次重复执行 `lint`、`typecheck`、`test`、`build`，最终输出 `CHECK_OK`。 |

## 已知边界与后续建议

- `.editorconfig` 提供编辑器格式化规则；命令行目前只做空白和结构验证，不提供完整 C# 自动格式化。若以后引入 .NET SDK，可评估 `dotnet format`，但需先确认旧式 .NET Framework 项目可正常加载，避免全仓重排。
- `lint` 不包含完整 C# 语义分析器或 PowerShell PSScriptAnalyzer；C# 编译警告在 `typecheck` 中视为错误。需要更深的静态分析时，单独评估工具和新增告警，不在本次基线中强行处理。
- 常规 `test` 不依赖网络，也不注册计划任务。真实任务创建/查询/删除的集成验证保留在 `tools/Test-StartupTask.ps1`，仅在明确需要时手动运行。
- `StartupService.cs` 当前尚未提交；Pester 的任务 XML 用例仅在该源文件存在时启用。干净检出本次安全网提交时，预期运行前两项基础测试，不需要提交未完成的功能改动。
- 提交前从暂存区导出的独立快照也运行了 `./dev.ps1 check`：退出码 0，`lint` 检查 20 个文件，Pester 2/2，构建版本为 `0.2.0.0`。这与上表当前工作区的 3/3、`0.3.0.0` 不同，原因是功能改动仍未暂存。
- 网络校时的实时测试未纳入常规基线，以免网络状态导致不稳定失败。
- 额外兼容性检查：Windows PowerShell 5.1 解析现有 `build.ps1` 时因无 BOM 的中文字符串失败。统一入口现提前提示必须使用 PowerShell 7；本次不批量改写原脚本编码。
