# 发布流程（标准）

## 一键发布

```powershell
.\publish.ps1                         # 版本自动读取 FlashStickNote.csproj；默认 win-x64
.\publish.ps1 -Runtime win-arm64      # 发布 ARM64 包
.\publish.ps1 -Version 1.0.1          # 仅在临时覆盖项目版本时使用
```

发布前只需修改 `FlashStickNote.csproj` 的 `<Version>`，再执行 `publish.ps1`。脚本会把该版本写入程序集和 zip 文件名，避免手工参数与项目版本不一致。

脚本自动完成（幂等，可重复执行）：

1. `dotnet publish -c Release -r {RID} --self-contained false -p:Version={版本} -p:DebugType=None -p:DebugSymbols=false`
   - **Release 构建**（发布必须用 Release；Debug 未优化且带调试信息）
   - **框架依赖**：产物约 0.3MB，目标机需装 .NET 9 桌面运行时（zip 内 README 已写明）
   - 无 PDB
2. 组装 `release\FlashStickNote-v{版本}-{RID}\`：发布产物 + `release-templates\`（默认 conf.json / shortcut.json / README.txt）+ `theme\` 文件夹
3. 打包 `release\FlashStickNote-v{版本}-{RID}.zip`

## GitHub 提交前检查

1. `git status --short` 只能包含本次准备提交的源码、文档和发布模板；`conf.json`、`shortcut.json`、`notes/`、`log.txt` 必须保持本机忽略。
2. 运行 `dotnet build FlashStickNote.csproj -c Release --no-restore`、`dotnet build FlashStickNote.csproj -c Debug --no-restore`，再分别执行 Release/Debug 的测试 harness。
3. 运行 `./publish.ps1`，检查生成 zip 内只包含发布模板、theme 和发布产物，不包含个人笔记、日志或绝对路径配置。
4. 运行 `git diff --check`、`git fsck --no-reflogs --unreachable`；确认目标 remote 后再执行 `git push <remote> avalonedit-editor`。若尚未配置 remote，发布前需由仓库所有者添加。
## 发版清单（每次发版）

```powershell
# 1. 确认改动已提交
git status

# 2. 发布
.\publish.ps1 -Version 1.0.1

# 3. 提交 + 打 tag（在 avalonedit-editor 分支独立发版，不合并 master）
git commit -m "release v1.0.1"
git tag v1.0.1
```

## 产物约定

- `release/` 已 gitignore，不入库
- zip 内配置为**干净默认模板**（Microsoft YaHei UI、notes 目录、light 主题），不含开发机个人设置
- 版本号默认来源：csproj `<Version>`；`-Version` 仅用于临时覆盖
- 图标已内嵌 exe（ApplicationIcon + 内嵌资源自提取），zip 无需附带 ico

## 构建输出与发布包

- `bin\Release\net9.0-windows\` 是 `dotnet build -c Release` 的构建输出，用于本机验证，不作为正式分发包。它可能没有干净的配置、主题和 README，也可能混入本机运行生成的数据。
- `bin\Release\net9.0-windows\{RID}\publish\` 是 `dotnet publish` 的中间产物。
- `release\FlashStickNote-v{版本}-{RID}.zip` 是唯一推荐分发的发布包：脚本已经复制默认配置、快捷键、主题和 README。

## 发布类型对比

| 类型 | 产物大小 | 目标机要求 | 适用 |
|---|---|---|---|
| 框架依赖（当前） | ~0.3MB | 需装 .NET 9 桌面运行时 | 个人分发、仓库存档 |
| 自包含 | ~170MB | 无要求 | 陌生机器免安装场景 |
| 单文件 | 略大 | 同框架依赖/自包含 | 追求单 exe 便携（启动稍慢） |

切换方式：`publish.ps1` 的 `dotnet publish` 行去掉 `--self-contained false` 即为自包含。
