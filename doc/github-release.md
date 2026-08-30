# GitHub 分支发布与 Release 页面

本项目当前开发分支是 `avalonedit-editor`。发布遵循“分支、tag、Release 资产”三层：分支保存源码，tag 固定发布提交，GitHub Release 页面附带可下载 zip。

## 完成一次发布的五步

1. **准备发布提交**：在 `avalonedit-editor` 完成代码、文档、Release/Debug 构建和两轮测试；执行 `./publish.ps1` 生成 `release/FlashStickNote-v{版本}-win-x64.zip`。
2. **配置 GitHub remote**：首次发布时由仓库所有者创建空 GitHub 仓库，再添加 `origin`。
3. **推送分支**：将 `avalonedit-editor` 作为独立分支推送，保留其开发历史，不合并到 `master`。
4. **创建并推送 tag**：tag 必须指向已验证的发布提交，格式为 `v{版本}`。
5. **创建 GitHub Release 页面**：在 GitHub 网页选择该 tag，填写标题和变更说明，上传生成的 zip。

## 第一次连接 GitHub

将下列占位地址替换为实际仓库地址。远程仓库应为空，或至少不包含与本地无关的初始提交。

```powershell
# 在 GitHub 创建仓库后执行一次
git remote add origin https://github.com/<owner>/<repository>.git
git remote -v

# 首次推送当前开发分支并建立上游关系
git push -u origin avalonedit-editor
```

若远程名称不是 `origin`，将命令中的 `origin` 换成实际名称。不要使用 `--force`；首次推送失败时先确认仓库地址、权限和远程初始提交。

## 发布一个新版本

以下示例使用 `1.0.1`。必须先将 `FlashStickNote.csproj` 的 `<Version>` 更新为相同版本，提交后再生成包和 tag。

```powershell
# 1. 验证与打包
dotnet build FlashStickNote.csproj -c Release --no-restore
dotnet run --project tests\FlashStickNote.Tests.csproj -c Release --no-restore
dotnet build FlashStickNote.csproj -c Debug --no-restore
dotnet run --project tests\FlashStickNote.Tests.csproj -c Debug --no-restore
.\publish.ps1

# 2. 提交发布版本并推送分支
git add FlashStickNote.csproj doc README.md release-templates
git commit -m "release: v1.0.1"
git push origin avalonedit-editor

# 3. 用已推送的提交创建带说明的 tag，再推送 tag
git tag -a v1.0.1 -m "FlashStickNote v1.0.1"
git push origin v1.0.1
```

`release/` 已忽略，zip 是 Release 页面资产，不能作为源码提交。

## GitHub Release 页面

GitHub 网页操作：进入仓库首页 → **Releases** → **Draft a new release**。

- **Choose a tag**：选择已推送的 `v1.0.1`，不要在网页临时创建与本地不同的 tag。
- **Target**：确认是 `avalonedit-editor` 的发布提交。
- **Release title**：`FlashStickNote v1.0.1`。
- **Describe this release**：从 `doc/changelog.md` 复制该版本的用户可见变更、兼容性说明和已知限制。
- **Attach binaries**：上传 `release/FlashStickNote-v1.0.1-win-x64.zip`。
- 发布前取消勾选 **Set as a pre-release**；只有测试版本才勾选。

本机没有 GitHub CLI（`gh`）。因此使用网页创建 Release 页面最直接，也不需要授予自动化工具 GitHub 账号权限。

## 公开前的隐私检查

当前工作树已经将 `conf.json`、`shortcut.json`、`notes/` 和 `log.txt` 设为本机忽略，发布 zip 只使用 `release-templates/` 的干净默认配置。

但早期 Git 提交曾跟踪根目录 `conf.json`，其中可能包含本机路径。首次公开推送前必须决定：

- 接受保留历史：确认历史中的本机路径可以公开，然后按上述步骤推送。
- 清理历史：先单独授权重写 Git 历史并人工复核结果，再添加 remote 和推送。历史重写会改变所有提交 ID，不能与已有协作者的分支混用。

未完成这项决定前，不应向公共 GitHub 仓库推送。

## 当前状态（2026-08-30）

- 分支：`avalonedit-editor`
- 本地 tag：`v1.0.0`
- 已验证发布包：`release/FlashStickNote-v1.0.0-win-x64.zip`
- GitHub remote：尚未配置
- 已验证命令：restore、Release/Debug build、Release/Debug WPF harness、`publish.ps1`