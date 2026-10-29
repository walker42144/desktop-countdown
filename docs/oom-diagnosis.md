# v0.3.1 内存异常诊断

## 2026-10-29 诊断增强与降载（未发布）

- 诊断日志的 `Memory` 行新增 `gdi=`、`user=`（`GetGuiResources`）、`commit=`/`limit=`（`GlobalMemoryStatusEx` 的页面文件口径，即系统提交量与上限）、`availablePhysical=`，以及 `top=`（按工作集排序的前三个进程名与 MB 数）。进程名按 `[A-Za-z0-9._-]` 过滤并截断到 32 字符；日志仍只写本地文件，不含路径与异常消息。
- 本机临时探针验证：`GlobalMemoryStatusEx` 可用；`GetGuiResources` 只从 `user32.dll` 导出，从 `gdi32.dll` 导入会抛 `EntryPointNotFoundException`，因此 GDI 与 USER 两个计数都通过 `user32.dll` 的不同 flags 获取。
- 内存耗尽（`System.OutOfMemoryException`）改为 `Environment.Exit(1)`。不再调用 `Environment.FailFast`，避免系统提交接近上限时 Windows 错误报告收集转储造成的长时间挂起；退出前仍先写入诊断行，同样不会留下过期倒计时。
- 防烧屏动画只在窗口的整数设备像素位置变化时应用 `Left`/`Top`，同一像素位置不重复 `SetWindowPos`，减少透明分层窗口的合成刷新；运动轨迹不变。
- 后续复测时先看新 `Memory` 行的 `commit`/`limit` 比例与 `top` 进程，判断 20GB 级别的系统提交由哪个进程占用；本进程若有增长，再对比 `gdi`/`user` 是否随时间累积。

## 已确认

- 2026-09-28 的本地诊断日志记录 14 条 `Program.UnhandledException System.OutOfMemoryException 0x8007000E`，分成三个时间段（本地时间 UTC+8）：15:05:51（1 条）、15:22:50–15:22:59（4 条）、16:43:12–16:43:29（9 条）。这是三个异常时段，不代表进程崩溃或重启了 14 次。
- Windows 应用程序事件日志在这些时段没有倒计时的进程崩溃记录。08:43 左右的 `LiteMonitor.exe` 与 `Corsair iCUE QmlRenderer.exe` 崩溃发生在上午，比最后一个倒计时异常时段早约八小时，目前没有证据表明它们相关。
- v0.3.1 的计时回调先停止计时器，刷新成功后才安排下次刷新；异常会使窗口仍显示但倒计时停止。全局处理器此前将所有 UI 异常标记为已处理。
- 旧日志不含调用堆栈或进程内存数据，因此无法把首次内存异常归因到某个功能。当前的自动壁纸取色、微位移与视觉呼吸都可能在无人操作时运行，但没有证据单独指向其中之一。

## v0.3.2 诊断能力

- 启动时和每隔五分钟，在 `%LOCALAPPDATA%\DesktopCountdown\diagnostics.log` 记录进程私有内存、工作集、托管堆与句柄数。单位为字节；日志沿用 256 KiB 上限和单份轮转备份。
- 异常新增最多四层方法名，不写异常消息、用户文件路径或设置内容。计时、外观、位移和视觉呼吸事件各自有独立上下文。
- 内存耗尽不再继续保持一个可能冻结的倒计时；诊断日志写入后，程序按不可恢复异常终止。Windows 可能生成包含用户数据的崩溃报告或转储，不要公开分享原始转储。

## 用户实机复测

1. 从托盘完全退出 v0.3.1，再运行 `C:\software\DesktopCountdown-v0.3.2.exe`；不要同时运行两个版本。
2. 保持平时的主题、壁纸与防烧屏设置，观察至少覆盖原先的报错时间跨度。无需刻意频繁操作。
3. 若再次报错或程序退出，先备份 `diagnostics.log` 与存在时的 `diagnostics.log.1`，记录发生时间和当时是否刚切换壁纸、显示器或从休眠恢复。
4. 可先分享上述文本日志；不要直接上传 Windows 崩溃转储。若长时间不再报错，也不能仅凭一次运行断定根因已消失。
