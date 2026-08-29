# 当前技术栈与架构清单

本文记录 FlashStickNote 的实际技术栈和主要实现边界。内容基于 avalonedit-editor 当前源码，核对日期为 2026-08-29。

## 平台、语言和构建

| 项目 | 当前值/做法 |
|---|---|
| 操作系统目标 | Windows 10/11，net9.0-windows。 |
| SDK | .NET 9 SDK；当前核对版本 9.0.317。 |
| 语言 | C#；Nullable 与 ImplicitUsings 均启用。 |
| 项目 | SDK 风格 Microsoft.NET.Sdk；主程序为 WinExe。 |
| UI | UseWPF=true；UseWindowsForms=true。 |
| 构建 | dotnet CLI + MSBuild；仓库没有 .sln，主入口为 FlashStickNote.csproj。 |
| 版本 | csproj 的 Version=1.0.0；发布脚本默认读取该值。 |
| 图标 | ApplicationIcon + EmbeddedResource；运行时可提取默认 ico。 |
| 可重复构建 | Directory.Build.props 清空 RestoreFallbackFolders，避免 Visual Studio 本机 NuGet 回退路径。 |

主项目排除 tests/**/*.cs；tests/FlashStickNote.Tests.csproj 是独立 Exe，通过 ProjectReference 使用主项目。因此应用和测试使用同一套实现。

## 第三方和系统依赖

| 依赖 | 版本 | 用途 |
|---|---:|---|
| AvalonEdit | 6.3.1.120 | 正文编辑器：TextDocument、撤销/重做、行号、折行、链接和渲染层。 |

其余来自 .NET/WPF/WinForms/Windows：System.Text.Json、System.IO、FileSystemWatcher、Dispatcher/DispatcherTimer、Microsoft.Win32.Registry、System.Windows.Interop、user32.dll P/Invoke。没有数据库、网络服务、ORM、依赖注入容器、日志框架或 xUnit/NUnit。

## 分层和文件职责

~~~text
App
  -> MainWindow
       -> MainViewModel -> NoteStorage / FileSystemWatcher
       -> AvalonEdit / NoteTextEditor
       -> Theme / Config / Icon / Hotkey / Tray
  -> Logger / AutoStart / single-instance signal
~~~

| 层 | 文件 | 职责 |
|---|---|---|
| 入口 | App.xaml、App.xaml.cs | 日志、全局异常、单实例、自启动、静默启动、主窗口。 |
| 视图 | MainWindow.xaml | 搜索、列表、GridSplitter、标题、正文、统计、菜单。 |
| UI 胶水 | MainWindow.xaml.cs | 主题/配置、编辑器同步、HWND、托盘、快捷键、窗口状态、鼠标命中。 |
| ViewModel | ViewModels/MainViewModel.cs | 笔记集合、选择、搜索、置顶、自动保存、监控和外部同步。 |
| 模型 | Models/Note.cs | INotifyPropertyChanged、Id、标题、正文、时间、路径、置顶、预览、统计、空笔记判断。 |
| 服务 | Services/*.cs | 配置、主题、存储、搜索、统计、日志、图标、自启、热键。 |
| 控件扩展 | Controls/*.cs | HTTP 链接、行剪切、AvalonEdit 按键修正、自绘光标。 |

架构是轻量 MVVM。ViewModel 管理业务状态和持久化编排；需要直接操作 WPF/AvalonEdit、窗口句柄、托盘或鼠标命中的部分仍在 MainWindow 代码后置。

## WPF 和编辑器

- XAML 使用 DynamicResource 读取 Theme.* 资源；ThemeService 从 JSON 主题生成运行时资源。
- 主布局为三列 Grid：列表、GridSplitter、编辑区；窗口最小 600x400。ListBox 使用 DataTemplate 与 WPF 虚拟化。
- 标题是 WPF TextBox；正文是 AvalonEdit TextEditor 的 NoteTextEditor 子类。AvalonEdit Text 不是依赖属性，正文靠事件双向同步，不用 XAML Text 绑定。
- 每个 Note 持有独立 TextDocument；切换笔记复用文档，保留每条笔记的撤销/重做历史。
- AvalonEdit 原生提供行号、折行、空白/制表符/行尾显示与 Ctrl+链接。HttpLinkElementGenerator 只匹配 http/https。
- NoteTextEditor 修正选区下无修饰方向键，支持无选区 Ctrl+X 剪切完整逻辑行。
- 插入光标在 KnownLayer.Caret 使用 IBackgroundRenderer 自绘；支持 line/block/underline、宽度、颜色、不透明度和闪烁，原生光标设透明。
- WindowInteropHelper/HwndSource 提供 HWND，用于全局热键、静默启动和窗口消息；无标题栏拖动、菜单命中和窗口矩形由 MainWindow 管理。

## 配置、主题和资源

ConfigService 使用 System.Text.Json。conf.json 与 shortcut.json 位于 AppContext.BaseDirectory，序列化 camelCase、反序列化大小写不敏感；文件缺失自动生成，缺字段取属性默认值，解析失败记录日志并只在内存使用默认值。

AppConfig 覆盖字体和有序回退、窗口矩形/记忆、笔记目录/格式、置顶笔记引用、主题、显示、颜色、光标、滚动条、标题栏、自启和不可见字符。光标字段为 caretStyle、caretWidth、caretColor、caretOpacity、caretBlinkInterval；默认 opacity=0.8，默认 blink=530ms。

ThemeService 内置 light、green、paper、dark 四套 ThemeColors，运行目录 theme/ 缺失时自动生成 JSON；主题控制窗口、列表、选中态、编辑区、行号、边框和分隔条。IconService 支持相对/绝对自定义图标，缺省时使用或提取内嵌 FlashStickNote.ico。

### 列表导航与置顶

MainWindow 的窗口内 InputBindings 处理 Ctrl+F、Ctrl+B、F2 与 Ctrl+P。ViewModel 保持 `IsPinned` 与排序，Window 承担 WPF 列表滚动、显式 Up/Down 导航和焦点恢复。`pinnedNotes` 使用 JSON 的稳定 Id 或 txt/md 相对路径；所有 Note 的 IsPinned/StoredFileName 变化均会写回配置，因此文本笔记改名不会丢失置顶状态。

## 本地文件存储

| 格式 | 标题 | 正文 | 元数据 |
|---|---|---|---|
| json | Note.Title | Note.Content | Note 序列化字段：Id、时间等。 |
| txt | 去扩展名的文件名 | 文件完整文本 | 文件创建/修改时间。 |
| md | 去扩展名的文件名 | 文件完整文本 | 文件创建/修改时间。 |

notesFormat 仅影响新笔记；已保存笔记按原扩展名继续保存。notesDir 支持相对、绝对、~/、%USERPROFILE%；默认目录导入以 .flashsticknote 标记一次性完成。

NoteStorage 写入同目录随机 .tmp，再 File.Move 覆盖目标；新文件成功后才删除重命名的旧文件。失败保存保留旧路径和内容。空笔记不落盘，清空已保存笔记删除文件。删除移到 notes/recycle/，同名追加编号；移动失败尝试 copy/delete，仍失败则调用方保留列表项。

txt/md 始终使用文件名为标题、全文为正文，不猜测首行是不是旧标题。这样正文恰好等于标题也不会被文件监控同步为空。

## 并发、计时器和同步

| 机制 | 技术与规则 |
|---|---|
| 自动保存 | DispatcherTimer 600ms；切换选择和退出前 Flush。 |
| watcher 合并 | DispatcherTimer 400ms + HashSet；监听 FileName、LastWrite、Size。 |
| 磁盘读取 | Task.Run；读取重试 5 次、每次 120ms。 |
| 删除防抖 | Task.Delay 450ms，再检查文件存在与自写标记。 |
| 失败重排 | 最多 2 次，每次 1.5 秒。 |
| 自写抑制 | 路径时间戳约 2 秒，防止保存触发回读。 |
| watcher 恢复 | Error 后重建 watcher、枚举目录、协调集合。 |
| 光标 | DispatcherTimer；有键盘焦点时按间隔切换可见性。 |
| 窗口矩形 | DispatcherTimer 600ms，合并位置/尺寸/状态变化。 |

FileSystemWatcher 不递归子目录，recycle/ 事件忽略；事件回到 UI 的操作都经 Dispatcher，Dispose 后不再调度。数据安全约束是：失败保存不改 StoredFileName，失败回收不移除列表，外部读取失败不清空现有笔记。

## Windows 集成

| 能力 | 实现 |
|---|---|
| 单实例 | Mutex + 命名 EventWaitHandle；第二实例通知已有窗口后退出，监听线程经 DispatcherPriority.ApplicationIdle 显示窗口。 |
| 全局快捷键 | user32 RegisterHotKey/UnregisterHotKey；HotkeyManager 解析 Ctrl/Alt/Shift/Win 和常见键名，WM_HOTKEY 经 HwndSource hook。 |
| 窗口内快捷键 | WPF InputBindings/KeyGesture；Ctrl+F 搜索、Ctrl+B 列表定位、F2 置顶循环，均不抢占其他应用。 |
| 托盘 | System.Windows.Forms.NotifyIcon。 |
| 自启动 | Registry 写 HKCU\Software\Microsoft\Windows\CurrentVersion\Run。 |
| 静默启动 | WindowInteropHelper.EnsureHandle 创建 HWND，不 Show/Hide。 |

UseWindowsForms 会引入同名类型，源码对 Application、TextBox、Brushes 等使用全限定名或别名。

## 可观测性、测试和发布

Logger 是无依赖静态日志器，lock 保护 File.AppendAllText，输出 BaseDir/log.txt。App 订阅 DispatcherUnhandledException 并设 Handled=true，也记录 AppDomain 未处理异常。Config、Theme、Icon、Storage、Watcher、快捷键与自启动的失败路径都记录日志并尽量回退。

测试是 STA 控制台 harness，覆盖纯逻辑、存储和真实 WPF 窗口。发布通过 publish.ps1：dotnet publish -c Release -r win-x64 --self-contained false，移除 PDB，复制 release-templates/ 与 theme/，再 Compress-Archive 成 zip。当前为 framework-dependent，目标机器需要 .NET 9 Desktop Runtime；release/、bin/、obj/、.vs/ 不入 Git。

## 当前边界

1. 仅支持 Windows。
2. txt/md 不自动识别 ANSI/GBK，推荐 UTF-8。
3. FileSystemWatcher 在云盘、网络盘和批量操作下仍需以落盘文件与日志确认。
4. 配置没有完整 schema 校验，无效值主要通过运行时回退或日志暴露。
5. 没有数据库、远端同步、账号体系、网络 API 或插件系统；笔记的可信源是本地独立文件。
