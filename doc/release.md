# 发布流程（标准）

## 完整发布步骤（Git tag + GitHub Release）

正式发布必须使用未被占用的版本号，并保证 zip、Git tag 与 GitHub Release 都对应同一个源码提交。以下以 `1.0.1` 为例。

### 1. 选择并写入新版本（先做这一步）

在修改 `FlashStickNote.csproj` 前，先检查本地和 GitHub 是否已经使用该 tag：

```powershell
git tag --list v1.0.1
git ls-remote --tags origin v1.0.1
```

两条命令都没有输出才可使用该版本。若任一命令有输出，增加版本号。然后将 `FlashStickNote.csproj` 的 `<Version>` 改为 `1.0.1`；**在完成此修改前，不要运行 `publish.ps1`，也不要用 `-Version` 参数绕过它。**

### 2. 验证发布版本

```powershell
dotnet restore
dotnet build FlashStickNote.csproj -c Release --no-restore
dotnet run --project tests\FlashStickNote.Tests.csproj -c Release --no-restore
dotnet build FlashStickNote.csproj -c Debug --no-restore
dotnet run --project tests\FlashStickNote.Tests.csproj -c Debug --no-restore
```

### 3. 构建发布软件包

```powershell
.\publish.ps1
```

确认输出为 `release\FlashStickNote-v1.0.1-win-x64.zip`。`release/` 已被忽略，zip 不进入 Git 提交。

### 4. 提交发布版本并推送 tag

```powershell
git add FlashStickNote.csproj doc README.md release-templates
git commit -m "release: v1.0.1"
git push origin avalonedit-editor
git tag -a v1.0.1 -m "FlashStickNote v1.0.1"
git show --no-patch v1.0.1
git push origin v1.0.1
```

`git show` 显示的提交必须是本次发布版本的提交。tag 已存在时绝不移动或复用它，应提高版本号后重新执行以上步骤。

### 5. 通过网页创建 GitHub Release

1. 打开 `https://github.com/djabboc/FlashStickNote`，点击 **Releases** → **Draft a new release**。
2. 在 **Choose a tag** 选择已推送的 `v1.0.1`，确认 **Target** 是步骤 4 的发布提交。
3. 在 **Release title** 填写 `FlashStickNote v1.0.1`，在说明框填写该版本的用户可见改动。
4. 在 **Attach binaries** 上传 `release\FlashStickNote-v1.0.1-win-x64.zip`，等待文件名和大小显示完成。
5. 正式版本取消 **Set as a pre-release**，保留 **Set as the latest release**，确认后点击 **Publish release**。
### 6. 发布后验证

1. 用未登录 GitHub 的浏览器窗口打开 Release 页面，确认 tag、标题和 zip 均可见。
2. 下载 zip 并解压，确认包含程序、`theme/`、干净默认配置和 README，不含个人笔记或本机配置。
3. Release 页面自动提供的 Source code zip/tar.gz 只是源码；用户应下载上传的 `FlashStickNote-v1.0.1-win-x64.zip`。

至此本次发布完成。下一次发布从步骤 1 重新开始，并使用新的版本号。



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
