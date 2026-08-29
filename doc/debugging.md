# 当前调试与验证方案

本文是 FlashStickNote 当前调试、诊断、测试、构建与发布验证手册。内容以 avalonedit-editor 分支当前源码为准，核对日期为 2026-08-29，适用于 Windows 10/11 与 .NET 9 SDK。

## 调试原则

1. 数据优先。运行目录中的 conf.json、shortcut.json、theme/、notes/ 与 log.txt 均可能是用户数据，先确认路径，不要用删除目录排查问题。
2. 按层缩小范围：配置/启动 -> 存储 -> 文件监控 -> ViewModel -> 编辑器/UI。日志和回归 harness 用于定位责任层。
3. 每个稳定复现的问题必须加入 tests/Program.cs 的可执行断言；手工验证不能替代回归。
4. 先验证 Release，再验证 Debug。Release 是发布产物，Debug 用于断点和交互检查。
5. 提交前区分用户已有改动与本次改动，特别是个人 conf.json：同时检查 git diff、git diff --cached 和 git status。

## 运行目录与数据边界

ConfigService.BaseDir 为 AppContext.BaseDirectory，即运行 exe/dll 的目录，而非仓库根目录。不同启动方式使用各自目录下的配置和笔记：

| 方式 | 目录 | 用途 |
|---|---|---|
| dotnet run | bin/Debug/net9.0-windows/ | 日常断点调试 |
| dotnet run -c Release | bin/Release/net9.0-windows/ | 本机发布配置验证 |
| 直接运行发布包 | 解压目录 | 真机验证 |

构建不会从仓库根目录复制 conf.json，避免覆盖用户配置。正式发布包的干净默认配置来自 release-templates/；本地运行后可在 BaseDir 看到 conf.json、shortcut.json、theme/、notes/、FlashStickNote.ico 与 log.txt。

## 标准验证命令

在仓库根目录执行。首次、更新 SDK 或 NuGet 缓存异常时先执行 dotnet restore；常规构建使用 --no-restore，以分离恢复问题和编译问题。

~~~powershell
dotnet restore
dotnet build FlashStickNote.csproj -c Release --no-restore

$testData = Join-Path $env:TEMP 'FlashStickNote-WpfTests'
if (Test-Path -LiteralPath $testData) {
    Remove-Item -LiteralPath $testData -Recurse -Force
}
dotnet run --project tests\FlashStickNote.Tests.csproj -c Release --no-restore

dotnet build FlashStickNote.csproj -c Debug --no-restore
if (Test-Path -LiteralPath $testData) {
    Remove-Item -LiteralPath $testData -Recurse -Force
}
dotnet run --project tests\FlashStickNote.Tests.csproj -c Debug --no-restore

git diff --check
git status --short
~~~

唯一允许自动删除的目录是明确命名的 %TEMP%\FlashStickNote-WpfTests。它是 WPF harness 的固定临时 notesDir；残留笔记会改变列表高度与虚拟化状态，造成空白区或右键测试假失败。

成功标准：

- build 为 0 个警告、0 个错误。
- test 输出 All FlashStickNote tests passed.
- git diff --check 无输出。

## 构建输出被锁定

出现 MSB3021/MSB3027 且提示 FlashStickNote.exe 占用 apphost.exe 或 FlashStickNote.exe 时，是同一配置的应用仍在运行。先确认 PID，再关闭该进程：

~~~powershell
Get-Process FlashStickNote
Stop-Process -Id <PID>
dotnet build FlashStickNote.csproj -c Debug --no-restore
~~~

强制结束前应等待 600ms 自动保存完成；优先使用托盘菜单“退出”。只能结束已确认的 FlashStickNote 进程，不要通过删除 bin/ 或 obj/ 绕过锁。

## 自动化回归 harness

项目不使用 xUnit/NUnit。tests/FlashStickNote.Tests.csproj 是 STA/WPF 控制台程序，通过 ProjectReference 引用主项目；主项目排除 tests/**/*.cs，因此测试不进入应用程序集。

| 范围 | 当前覆盖 |
|---|---|
| 纯逻辑 | 搜索：大小写、全字、正则、非法正则；行剪切范围；旧行尾配置迁移；字体回退；文档统计。 |
| 存储 | txt 新建/重命名、失败保存保留旧文件、回收站、正文与标题同名的 XXX 以及 XXX+换行+YYY 读回。 |
| 窗口和交互 | 普通/无标题栏的菜单命中、文件菜单、列表空白右键、笔记右键、空草稿保留、跨笔记 Ctrl+Z。 |
| 窗口矩形 | 绝对坐标启动、移动/缩放 600ms 写回、退出最终写回。 |
| 编辑器光标 | caretBlinkInterval 边界与真实闪烁、光标移动后的立即显示、0 常亮；caretOpacity 默认、边界与真实画刷透明度。 |

UI 测试创建真实 Application、MainWindow、AvalonEdit、键盘焦点和 DispatcherTimer，不是无头单元测试。列表项断言会 ScrollIntoView 并泵送 Dispatcher，避免布局队列或虚拟化产生假失败。

## 手工验证清单

### 配置、启动和日志

1. 启动目标 exe，检查 BaseDir/log.txt 有“程序启动”、配置、主题、快捷键、监控相关记录。
2. 修改 conf.json 或 shortcut.json 后重启；当前绝大多数配置不热加载。
3. 配置无法解析时，程序记录错误并在内存使用默认值，不会覆盖原文件。
4. 单实例：第二实例应通知已有窗口显示；多实例：临时设置 allowMultiInstance=true。
5. silentStart：不应闪出窗口或任务栏；startWithWindows：检查 HKCU\Software\Microsoft\Windows\CurrentVersion\Run 与日志。

### 笔记、持久化和恢复

建议设临时 notesDir，避免污染真实笔记：

1. 创建 txt/md/json，输入正文，等待超过 600ms，检查文件。
2. 改标题：新文件完整写入后才删除旧文件；目标不可写时旧文件和 StoredFileName 必须保留。
3. 清空已保存笔记：文件删除；删除笔记：文件进入 notes/recycle/，同名自动编号。
4. 同名正文：标题 XXX，正文输入 XXX、换行、YYY，重启或外部同步后两行均保留。
5. txt/md 的文件名始终为标题、文件全文始终为正文；禁止根据首行猜测“旧标题”，因为会损失合法正文。

### 文件监控

在 notesDir 用资源管理器或另一编辑器新增、修改、删除、重命名 json/txt/md：

- Created/Changed/Renamed 先经 400ms DispatcherTimer 合并。
- 文件读取在 Task.Run，读取被占用时重试 5 次、每次 120ms；失败最多再排队 2 次、每次 1.5 秒。
- Deleted 先等待 450ms，再检查文件仍不存在且非程序刚写入；过滤云盘或原子替换的短暂 delete/create。
- 程序保存路径会进入约 2 秒的自写抑制窗口，避免 watcher 回读覆盖自己。
- FileSystemWatcher.Error 会重建 watcher 并全量枚举目录协调。

正在保存的当前笔记不应被 watcher 回读覆盖。外部同步异常时，首先对照日志、notesDir 与 recycle/ 路径，并确认当前运行目录。

### 列表导航与置顶

- Ctrl+F 后搜索框应获得键盘焦点并全选已有内容。
- Ctrl+B 后当前笔记的 `ListBoxItem` 必须存在、在视野中且获得键盘焦点；Up/Down 只应改变列表选中项，不能跳到搜索或菜单。再次按 Ctrl+B 必须回到记录的标题/正文编辑位置。若搜索过滤使它消失，SearchText 会被清空，这是定位语义而非数据修改。
- 右键笔记项检查“置顶/取消置顶”文案与金色 `PIN` 标识；查看 conf.json 的 `pinnedNotes` 是否立即更新。
- F2 在置顶笔记中回环。正文聚焦时，切换后 TextArea 仍应获得键盘焦点；Ctrl+P 与右键均应立即更新 `pinnedNotes`。txt/md 重命名后重启应用检查置顶是否保留。
### 编辑器、撤销和光标

每个 Note 有独立 AvalonEdit TextDocument；切换笔记必须复用文档，不能每次设置 TextEditor.Text，否则会清空撤销栈。验证：编辑 A，切到 B，再回 A，Ctrl+Z 应撤销 A。

自绘插入光标检查：

- caretStyle：line/block/underline。
- caretWidth：运行时限制 1-12。
- caretColor：空值回退为正文色或主题色。
- caretOpacity：0-1，默认 0.8；只作用于光标画刷副本。
- caretBlinkInterval：0 常亮；其他值限制 100-2000ms；光标移动或重获焦点立即显示后重置计时。

### 窗口、托盘和快捷键

- rememberWindowBounds=true：位置、尺寸、状态变化经 600ms 防抖写回；托盘“退出”立即保存；最大化保存 RestoreBounds。
- toggleWindow 是 user32 RegisterHotKey 全局快捷键；newNote、hideWindow、fontZoom、toggleWordWrap、focusSearch、focusNoteList、cyclePinnedNotes 是窗口内 WPF 输入绑定，不能抢占其他应用。
- 右键列表空白区不得改变选择或删除空草稿；笔记项右键才弹删除/置顶菜单。Ctrl+B 应使当前笔记可见；F2 切换置顶项后应保留原输入焦点。

## 日志和断点地图

Logger 在 App.OnStartup 最早初始化，使用 lock + File.AppendAllText 追加 BaseDir/log.txt。日志写入失败会被吞掉，因此没有日志不等于没有错误。

| 问题 | 首查位置 | 关键代码 |
|---|---|---|
| 启动、单实例、静默启动、全局异常 | log.txt；App.xaml.cs | OnStartup、ListenForShowSignal、OnExit |
| 配置/主题/图标 | BaseDir 文件和 log.txt | ConfigService、ThemeService、IconService、MainWindow.ApplyConfig |
| 保存、改名、回收站 | notesDir、recycle/、log.txt | NoteStorage |
| 外部文件不同步 | log.txt、文件时间戳、路径 | MainViewModel 的 OnWatcher*、ProcessReloadQueue、ReloadFromDisk |
| 搜索、选择、自动保存 | ViewModel 属性 | RefreshFilter、SelectedNote、Flush |
| 撤销、链接、光标 | ContentBox/TextArea/TextDocument | MainWindow 编辑器同步、NoteTextEditor、HttpLinkElementGenerator |
| 托盘、标题栏、窗口矩形 | MainWindow.xaml.cs | HWND hook、窗口事件、ExitApp |

报告问题时应同时提供：运行目录、notesDir、相关 conf 字段、精确操作步骤、文件名/时间与对应时间段的 log.txt。开发、Release 和发布包可能使用不同配置，因此“同步失败”本身不足以定位。

## 时序和可靠性约束

| 机制 | 时间/规则 | 调试含义 |
|---|---|---|
| 自动保存 | 600ms DispatcherTimer | 输入后立刻结束进程可能绕过防抖；切换笔记和退出前会 Flush。 |
| 监控合并 | 400ms DispatcherTimer | 连续事件不会逐一处理。 |
| 读取重试 | 5 x 120ms | 文件复制尚未完成时正常延迟。 |
| 失败重排 | 最多 2 x 1.5s | 连续失败要查文件锁和访问权限。 |
| 删除确认 | 450ms + 文件二次检查 | 瞬态删除不应从列表移除笔记。 |
| 自写抑制 | 约 2s | 保存后的 watcher 事件通常会被跳过。 |
| 窗口矩形 | 600ms | 拖动中不应频繁写配置。 |
| 光标闪烁 | 默认 530ms | 只有编辑器有键盘焦点时运行。 |

## 发布验证和已知边界

publish.ps1 读取 csproj Version，执行 framework-dependent 的 win-x64 Release 发布，复制 release-templates/ 和 theme/，压缩为 release/ 下 zip：

~~~powershell
.\publish.ps1
~~~

发布后检查 zip 的干净配置、主题、README；解压到新目录测试启动、托盘、笔记保存、全局唤窗和默认配置。目标机需要 .NET 9 Desktop Runtime。

已知边界：txt/md 使用 File.ReadAllText，未自动识别 ANSI/GBK，中文文本应保存 UTF-8；FileSystemWatcher 在云盘/网络盘/批量操作下可能合并或丢失事件，需结合文件与日志确认；harness 不替代不同 DPI、多屏、注册表权限、云盘和真实用户目录的发布包手工测试。
