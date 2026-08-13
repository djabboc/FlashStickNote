# 开发指南：架构与踩坑记录

## 技术栈

- .NET 9 (net9.0-windows)，WPF + WinForms（托盘），MVVM（轻量，代码后置承担 UI 胶水）
- AvalonEdit 6.3.1.120（编辑区）
- 存储：笔记为独立文件（json/txt/md），配置为 json（System.Text.Json）

## 架构要点

### 主窗口分层

```
App.OnStartup
 ├─ Logger.Init + 全局异常兜底（DispatcherUnhandledException → 记日志阻止闪退）
 ├─ 单实例（Mutex + EventWaitHandle 信号 → 已有实例 ShowWindow）
 ├─ AutoStartService.Apply(startWithWindows)
 └─ 创建 MainWindow（silentStart 时 Opacity=0 → Show → Hide 防闪窗）

MainWindow 构造
 ├─ ApplyThemeResources（先于 InitializeComponent，设置 DynamicResource 主题画刷）
 ├─ NoteStorage 初始化（目录解析/旧目录导入/格式识别）
 ├─ MainViewModel（数据 + 搜索 + 监控 + 自动保存）
 ├─ ApplyAppIcon / ApplyConfig / InitEditor / InitEditorBinding / InitFontZoom / 托盘
 └─ OnSourceInitialized：全局快捷键注册（RegisterHotKey + HwndSource hook）
```

### 数据流

- 笔记 ↔ 文件：`NoteStorage`。txt/md **文件名=标题、内容=纯正文**；json 存元数据。
  保存防抖 600ms（VM DispatcherTimer）；清空内容删除文件；删除移入 recycle。
- 编辑器 ↔ 笔记：AvalonEdit 的 `Text` 不是依赖属性，**XAML 绑定不可用**，
  用事件双向同步：`TextChanged → note.Content`（触发防抖保存）、
  `SelectedNote 变化 / note.Content 外部变化 → editor.Text`（`_editorSyncing` 防回环）。
- 外部文件 ↔ 列表：`FileSystemWatcher` 事件 → Dispatcher → 防抖 400ms 处理。
- 搜索：`ICollectionView.Filter`（标题/内容包含匹配，OrdinalIgnoreCase）；
  列表 SelectedItem 改 OneWay 绑定 + SelectionChanged 事件，过滤时编辑区保持原笔记。

### 快捷键分层

- **全局**（RegisterHotKey）：toggleWindow、newNote —— 即使窗口隐藏也生效，但会拦截系统级按键
- **窗口内**（InputBinding/事件）：hideWindow（Ctrl+W）、fontZoom（Ctrl+Wheel）、Del（列表 KeyDown）
- 破坏性按键（Del/Ctrl+W 等）**不要**注册为全局，否则劫持所有应用

## 关键实现

### 行号（已由 AvalonEdit 内置取代）

旧 TextBox 自绘行号的最终方案（`LineNumberRenderer`，已删除，仅存档于 master 分支）：

1. 每个逻辑行用 `TextBlock.Measure(width=ScrollViewer.ViewportWidth, Wrap)` 测折后高度，
   行号 Canvas 定位于逻辑行起点（折行续行不编号）
2. 三个关键坑：
   - **`\r\n` 必须归一化**：按 `\n` 拆分后行尾残留 `\r`，WPF 把单独 `\r` 也当换行符 → 每行多测一行
   - **加 `TextBox.Padding.Top` 偏移**：padding 在滚动内容之外，行号从 0 开始会恒定错位
   - **不要用 `FormattedText` 测宽**（PixelsPerDip 参与时 Width 不可靠），用真实布局控件 Measure
   校准结论：同字体同宽度下 `TextBlock.Measure` 与 TextBox 布局逐行 0 误差
3. 缓存按 (文本, ViewportWidth) 失效；滚动只重排可见行号；文本/尺寸变化延迟到 Loaded 优先级重算

### 超链接（AvalonEdit）

`HttpLinkElementGenerator : LinkElementGenerator`，构造时传入自定义正则
`\bhttps?://[\w\d\._/\-~%@()+:?&=#!]*[\w\d/]`（只匹配 http/https）。
AvalonEdit 的 `VisualLineLinkText` 在 Ctrl+单击（`RequireControlModifierForHyperlinkClick=true`）
时若无处理者会自动 `Process.Start(UseShellExecute)` 打开浏览器，无需额外代码。
替换默认生成器：`TextArea.TextView.ElementGenerators` 移除内置 `LinkElementGenerator` 后加入自定义。

### 单实例

- `Mutex("FlashStickNote_SingleInstance")` + `EventWaitHandle` 信号
- **坑**：App.xaml 的 `StartupUri` 在 `OnStartup` 内 `Shutdown()` 后仍会创建窗口
  → 移除 StartupUri，改为 `OnStartup` 手动 `new MainWindow() + Show()`

### 配置热管理

- 配置/主题/图标一律在 **exe 目录**，程序首次运行自动生成默认值（`ConfigService.Load` 缺文件即写）
- csproj 不复制 json 到输出（避免构建覆盖用户修改；且**移除 CopyToOutputDirectory 项时，
  增量构建会删除输出目录中已复制的文件**——曾误删用户配置）

### 图标

`FlashStickNote.ico`：PNG 帧容器（16/24/32/48/64/128/256，系统绘制闪电多边形）。
csproj `ApplicationIcon`（exe 内嵌）+ `EmbeddedResource LogicalName`（运行时缺失自动提取）。

## 踩坑清单（历史教训）

| 现象 | 根因 | 修复 |
|---|---|---|
| 改 conf.json 永远不生效 | `JsonSerializer.Deserialize` 未传 Options，camelCase 键未映射到 PascalCase 属性，静默用默认值 | 传 `PropertyNameCaseInsensitive=true`；写用 `PropertyNamingPolicy=CamelCase` |
| 复制文件进笔记目录必闪退 | Created 事件触发时文件仍被占用，`File.ReadAllText` 未捕获异常 | 读取重试 5×120ms + 防抖 + 失败延迟重排 + 全局异常兜底 |
| 行号折行错位 | 自绘行号按逻辑行均匀排布 | 测量法（见上文）；最终整体换 AvalonEdit 内置行号 |
| 行号恒定偏移 10px | 漏加 TextBox.Padding.Top | 定位时加 padding |
| 行号随行数累积漂移 | 行尾残留 `\r` 被当作换行 | 测量前归一化 `\r\n`/`\r` → `\n` |
| WPF/WinForms 类型歧义（Application/TextBox/Size/Color/Brushes…） | UseWindowsForms 注入全局 using | 全限定或用别名 |
| WPF SDK 隐式 using 不含 System.IO | — | 显式 `using System.IO` |
| 单实例 Shutdown 后窗口仍出现 | StartupUri 处理时机 | 移除 StartupUri 手动建窗 |
| 静默启动闪窗 | Show 后再 Hide 有闪烁 | Opacity=0 → Show → Hide → 恢复 |
| 构建删除用户 bin 配置 | 移除 csproj copy 项触发增量清理 | 配置改为运行时自生成 |
| 单实例失败 | Mutex 未持有引用被 GC | static 字段持有 |
| 通知已有实例显示失败 | 信号到达时 MainWindow 未创建 | DispatcherPriority.ApplicationIdle 延迟执行 |
| 打开复制的中文 txt 乱码 | 文件为 ANSI/GBK 编码 | 未修复（按 UTF-8 读取）；README 建议用 UTF-8 |

## 测试约定

- 本项目无单元测试框架；采用**临时控制台 harness**（`fsntestN`，ProjectReference 引用主工程）做行为验证：
  - 存储层（格式迁移、回收站、目录导入、空笔记规则）用纯控制台断言
  - UI 层（行号测量、链接识别、视觉控件）用 STA + 真实 WPF 控件 + ground truth 对比
- 真机验证：临时改 bin conf → 启动 exe → 检查 log.txt / MainWindowTitle / 注册表 → 还原配置
- 行号测量曾用第二个真实 TextBox 逐行测量前缀文本高度作为 ground truth（300 行 0 误差）

## 日志

- `log.txt`（exe 目录）：启动流程、配置读取、监控事件、异常兜底、快捷键/图标/自启状态
- 排查用户问题优先看 log.txt；Logger 线程安全（lock + AppendAllText）
