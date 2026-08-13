# 发布流程（标准）

## 一键发布

```powershell
.\publish.ps1 -Version 1.0.0          # 默认 win-x64；可加 -Runtime win-arm64
```

脚本自动完成（幂等，可重复执行）：

1. `dotnet publish -c Release -r {RID} --self-contained false -p:Version={版本} -p:DebugType=None -p:DebugSymbols=false`
   - **Release 构建**（发布必须用 Release；Debug 未优化且带调试信息）
   - **框架依赖**：产物约 0.3MB，目标机需装 .NET 9 桌面运行时（zip 内 README 已写明）
   - 无 PDB
2. 组装 `release\FlashStickNote-v{版本}-{RID}\`：发布产物 + `release-templates\`（默认 conf.json / shortcut.json / README.txt）+ `theme\` 文件夹
3. 打包 `release\FlashStickNote-v{版本}-{RID}.zip`

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
- 版本号来源：csproj `<Version>`（默认值）+ 脚本参数覆盖
- 图标已内嵌 exe（ApplicationIcon + 内嵌资源自提取），zip 无需附带 ico

## 发布类型对比

| 类型 | 产物大小 | 目标机要求 | 适用 |
|---|---|---|---|
| 框架依赖（当前） | ~0.3MB | 需装 .NET 9 桌面运行时 | 个人分发、仓库存档 |
| 自包含 | ~170MB | 无要求 | 陌生机器免安装场景 |
| 单文件 | 略大 | 同框架依赖/自包含 | 追求单 exe 便携（启动稍慢） |

切换方式：`publish.ps1` 的 `dotnet publish` 行去掉 `--self-contained false` 即为自包含。
