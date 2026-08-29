# FlashStickNote 开发文档

Windows 便携闪念笔记软件，WPF (.NET 9) 开发。所有数据（配置、主题、笔记）保存在程序目录，可整体放入 U 盘使用。

## 文档目录

| 文档 | 内容 |
|---|---|
| [features.md](features.md) | 全部功能清单 |
| [config.md](config.md) | conf.json / shortcut.json / 主题文件完整参考 |
| [development.md](development.md) | 架构、关键实现、踩坑记录 |
| [release.md](release.md) | 标准发布流程（publish.ps1） |
| [changelog.md](changelog.md) | 开发历史（按时间线） |
| [debugging.md](debugging.md) | 当前调试、测试、日志、构建与发布验证方案 |
| [tech-stack.md](tech-stack.md) | 当前技术栈、架构、存储、并发与 Windows 集成 |
| [task-book-2026-08-29-navigation-pins.md](task-book-2026-08-29-navigation-pins.md) | 导航与置顶任务书、验收与验证记录 |

## 快速开始（开发）

```powershell
# 运行（Debug）
dotnet run

# 构建
dotnet build

# 发布标准包（版本自动读取 csproj 的 <Version>）
.\publish.ps1
```

## 运行要求

- Windows 10 / 11 (x64)
- .NET 9 桌面运行时（框架依赖发布方式时需要；开发环境用 SDK）

## Git 分支结构

- `master`：TextBox 编辑区旧版（初始备份，保留回退点）
- `avalonedit-editor`：当前开发主线（AvalonEdit 编辑区 + 全部后续功能），tag `v1.0.0`
- 发布策略：分支独立发版，不合并 master

## 目录结构

```
FlashStickNote/
├── App.xaml(.cs)            入口：单实例、全局异常兜底、静默启动、开机自启
├── MainWindow.xaml(.cs)     主窗口：布局、快捷键、图标、主题应用、编辑器同步
├── Models/
│   └── Note.cs              笔记模型（INPC、EffectiveTitle、ContentPreview）
├── ViewModels/
│   ├── MainViewModel.cs     笔记集合、选择、搜索过滤、文件监控、自动保存
│   └── RelayCommand.cs      ICommand 实现
├── Services/
│   ├── ConfigService.cs     conf.json/shortcut.json 读写（camelCase）
│   ├── NoteStorage.cs       笔记存储（json/txt/md、回收站、目录导入、格式保留）
│   ├── ThemeService.cs      主题加载 + 内置 4 套主题
│   ├── IconService.cs       图标解析（默认图标自动提取）
│   ├── AutoStartService.cs  注册表 Run 键开机启动
│   ├── HotkeyManager.cs     全局快捷键（RegisterHotKey）
│   └── Logger.cs            log.txt 日志
├── Controls/
│   └── HttpLinkElementGenerator.cs  AvalonEdit 超链接生成器（仅 http/https）
├── theme/                   内置主题（light/green/paper/dark）
├── release-templates/       发布模板（默认 conf/shortcut/README）
├── publish.ps1              标准发布脚本
├── conf.json / shortcut.json 开发用配置（发布包用 release-templates）
└── FlashStickNote.ico       闪电图标（7 尺寸，内嵌 exe + 内嵌资源）
```
