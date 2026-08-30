# GitHub 分支发布与 Release 页面

本项目当前开发分支是 `avalonedit-editor`。发布遵循“分支、tag、Release 资产”三层：分支保存源码，tag 固定发布提交，GitHub Release 页面附带可下载 zip。

## Git tag 和 GitHub Release 的关系

Git tag 是 Git 仓库内的名字，用来永久指向一个确定的提交；它随 `git push` 上传到 GitHub 后，仍然只是一个“源码版本坐标”。GitHub Release 是 GitHub 网站上的发布记录，必须选择一个 tag 作为版本依据，并可附加标题、变更说明和 zip 等二进制资产。

| 对象 | 保存的位置 | 作用 | 是否自动产生另一个对象 |
| --- | --- | --- | --- |
| Git tag，例如 `v1.0.1` | Git 提交历史 | 固定可复现的源码提交 | 推送 tag 不会自动创建 GitHub Release |
| GitHub Release | GitHub 网站 | 提供用户可下载页面、说明与附件 | 创建 Release 不会生成或移动 Git tag |
| 发布 zip | Release 附件 | 用户安装包 | 不会进入 Git 提交历史 |

一次正式发版通常是一对一关系：一个版本 tag 对应一个 GitHub Release。先完成代码、测试和打包，再提交发布版本、创建 tag，最后让 Release 选择该 tag 并上传**由同一提交构建**的 zip。tag 一旦已公开使用，不应移动或复用；发现问题时提高版本号，创建新的提交、新 tag 和新 Release。

## 完成一次发布的五步

1. **准备发布提交**：在 `avalonedit-editor` 完成代码、文档、Release/Debug 构建和两轮测试；执行 `./publish.ps1` 生成 `release/FlashStickNote-v{版本}-win-x64.zip`。
2. **配置 GitHub remote**：首次发布时由仓库所有者创建空 GitHub 仓库，再添加 `origin`。
3. **推送分支**：将 `avalonedit-editor` 作为独立分支推送，保留其开发历史，不合并到 `master`。
4. **创建并推送 tag**：tag 必须指向已验证的发布提交，格式为 `v{版本}`。
5. **创建 GitHub Release 页面**：在 GitHub 网页选择该 tag，填写标题和变更说明，上传生成的 zip。

## 第一次连接 GitHub（专用 SSH 密钥）

本机为 GitHub 推送创建了专用密钥 `C:\Users\huang\.ssh\id_ed25519_flashsticknote`。Public 仓库允许任何人读取和下载，但推送仍必须使用拥有写权限的 GitHub 身份。

### 1. 验证 GitHub 身份

在 PowerShell 执行：

```powershell
ssh -i C:\Users\huang\.ssh\id_ed25519_flashsticknote -o IdentitiesOnly=yes -T git@github.com
```

成功时会显示 `Hi djabboc! You've successfully authenticated, but GitHub does not provide shell access.`。`git@github.com` 中不要写反斜杠；`git\@github.com` 是错误的 SSH 用户名。直接运行 `ssh -T git@github.com` 不会自动使用本项目的 Git 专用密钥，可能显示 `Permission denied (publickey)`。

### 2. 为仓库配置 remote 和专用密钥

```powershell
# FlashStickNote 当前仓库，只需执行一次
git remote add origin git@github.com:djabboc/FlashStickNote.git
git config core.sshCommand "ssh -i C:/Users/huang/.ssh/id_ed25519_flashsticknote -o IdentitiesOnly=yes"
git remote -v

git push -u origin avalonedit-editor
```

`core.sshCommand` 只写入当前仓库的 `.git/config`，因此之后正常执行 `git push` 会自动使用这把密钥，不影响其他仓库。

不要直接照抄 GitHub 空仓库页面的 `git branch -M main`：它会把当前分支改名为 `main`。本项目应保留 `avalonedit-editor` 并推送该分支。其他项目也遵循同一原则，例如 EnputMethod 应替换 remote 地址，并推送自己的当前分支：

```powershell
git remote add origin git@github.com:djabboc/EnputMethod.git
git config core.sshCommand "ssh -i C:/Users/huang/.ssh/id_ed25519_flashsticknote -o IdentitiesOnly=yes"
git push -u origin <当前分支名>
```

不要使用 `--force`；首次推送失败时先确认仓库地址、写入权限和当前分支名。

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

## 通过 GitHub 网页上传 Release

以下示例使用下一次正式版本 `v1.0.1`。开始前确认 `v1.0.1` 已推送，且 zip 是由该 tag 指向的提交构建；不要把当前 `FlashStickNote-v1.0.0-win-x64.zip` 上传到旧 tag `v1.0.0`。

1. 打开仓库首页 `https://github.com/djabboc/FlashStickNote`，点击右侧 **Releases**，再点击 **Draft a new release**。
2. 在 **Choose a tag** 中选择已有的 `v1.0.1`；不要输入新 tag，也不要让网页创建临时 tag。
3. 确认页面显示的 **Target** 是预期的 `avalonedit-editor` 发布提交；它必须和本地打包时的提交一致。
4. 在 **Release title** 填写 `FlashStickNote v1.0.1`。
5. 在说明框填写用户可见改动、兼容性说明和已知限制；内容可从 `doc/changelog.md` 对应版本整理。
6. 将 `release/FlashStickNote-v1.0.1-win-x64.zip` 拖入 **Attach binaries** 区域，或点击该区域选择文件；等待文件名和大小显示完成。
7. 正式版本不要勾选 **Set as a pre-release**；只有测试版本才勾选。通常保留 **Set as the latest release**，只有有意让旧版继续显示为最新版时才取消。
8. 再次核对 tag、标题和 zip 文件名，点击 **Publish release**。
9. 发布完成后，复制 Release 页面链接，用无登录浏览器窗口打开并下载 zip，确认下载项可见且压缩包能正常打开。

### 发布后核对

1. Release 页面显示的 tag 应为 `v1.0.1`，不能是相近但不同的版本号。
2. 附件应只有对应版本的 `FlashStickNote-v1.0.1-win-x64.zip`；GitHub 自动提供的 Source code zip/tar.gz 是源码，不是安装包。
3. 若上传了错误 zip，不要移动既有 tag；删除或编辑草稿/Release 附件，重新构建正确包后再上传。若已正式公开错误版本，提升版本号并创建新 tag 与新 Release。

本机没有 GitHub CLI（`gh`）。因此使用网页创建 Release 页面最直接，也不需要授予自动化工具 GitHub 账号权限。

## 公开前的隐私检查

当前工作树已经将 `conf.json`、`shortcut.json`、`notes/` 和 `log.txt` 设为本机忽略，发布 zip 只使用 `release-templates/` 的干净默认配置。

早期 Git 提交曾跟踪根目录 `conf.json`，其中包含本机用户名与笔记目录路径。仓库所有者已接受公开这些历史路径；未发现凭据或笔记正文。未来若要清理历史，必须单独授权重写 Git 历史并人工复核结果。

## 当前状态（2026-08-30）

- GitHub remote：`git@github.com:djabboc/FlashStickNote.git`
- 已推送分支：`avalonedit-editor`（以 GitHub 仓库页面显示的最新提交为准）
- 已推送 tag：`v1.0.0`，指向历史提交 `4132193`
- GitHub Release 页面：尚未创建
- 已验证 zip：`release/FlashStickNote-v1.0.0-win-x64.zip`，不能附加到旧的 `v1.0.0` tag；下一次正式发布应先将项目版本提高到 `1.0.1`，在当前代码提交上重新打包，再创建 `v1.0.1` tag 和 Release。
- 已验证命令：restore、Release/Debug build、Release/Debug WPF harness、`publish.ps1`