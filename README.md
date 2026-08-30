# FlashStickNote

Windows 便携闪念笔记软件。使用 WPF/.NET 9 与 AvalonEdit，笔记、主题和运行时配置都保存在应用目录，适合放入 U 盘或同步目录使用。

## 功能

- JSON、txt、md 笔记；自动保存、回收站和外部文件变更同步
- 搜索、正则/全字/大小写匹配、独立撤销/重做历史
- 窗口内快捷键：Ctrl+F 搜索、Ctrl+B 在列表和编辑位置间切换、F2 循环置顶、Ctrl+P 切换置顶
- 可配置字体回退、主题、行号、折行、光标、窗口位置和托盘行为

## 开发

要求：Windows 10/11、.NET 9 SDK。

```powershell
# 还原、构建与测试
dotnet restore FlashStickNote.csproj
dotnet build FlashStickNote.csproj -c Release --no-restore
dotnet run --project tests\FlashStickNote.Tests.csproj -c Release --no-restore

# 生成框架依赖的 win-x64 发布包
.\publish.ps1
```

详细设计、调试与发布步骤见 [doc/README.md](doc/README.md)。

## 本机数据

根目录的 `conf.json`、`shortcut.json`、`notes/` 和 `log.txt` 是本机运行数据，不纳入 Git。可分发的干净默认配置只维护在 [release-templates/](release-templates/) 中。

## 发布前

执行 [doc/release.md](doc/release.md) 的 GitHub 提交前检查，确认 Release/Debug 测试和发布包检查均通过，再配置 remote 并推送分支。