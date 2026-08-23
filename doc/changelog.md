# 变更记录（Changelog）

按开发对话时间线整理。当前主线分支 `avalonedit-editor`（tag v1.0.0）。

## v1.0.0（AvalonEdit 主线）

### 阶段 1：初始开发
- WPF 笔记软件骨架：左侧列表 + 右侧标题/内容编辑区
- `shortcut.json` 全局快捷键（默认 Ctrl+Shift+N 显示/隐藏窗口）
- `conf.json` 字体配置；关闭窗口驻留托盘

### 阶段 2：快捷键与笔记目录
- 全局 Ctrl+N 快速新建笔记
- `notesDir`（相对/绝对/~/%USERPROFILE%）、`notesFormat`（json/txt/md）
- 切换目录时自动搬入旧笔记（`.flashsticknote` 一次性标记）

### 阶段 3：修复配置不生效（重要 Bug）
- 根因：System.Text.Json 反序列化未传 Options，camelCase 键静默被忽略
- 新增 log.txt 运行日志

### 阶段 4：行为调整
- 取消强制格式转换：`notesFormat` 只影响新建笔记，旧文件保持原格式
- `allowMultiInstance` 单实例开关（二次启动唤醒已有窗口）
- `newNoteFocus` 新建焦点（标题/内容）

### 阶段 5：空笔记与默认标题
- 空笔记不产生文件（清空即删文件、加载忽略空文件、空草稿自动清理）
- 无标题时显示名 = 内容第一行

### 阶段 6：文件系统监控
- FileSystemWatcher 实时同步外部增/删/改/重命名
- 删除后自动选中下一条；防自写循环；recycle 目录忽略

### 阶段 7：列表内容预览
- `listContentLines`（0-N 行）

### 阶段 8：快捷键扩展 + 回收站
- `hideWindow`（Ctrl+W，窗口内）、Del 删除笔记
- 回收站（notes/recycle/，移回即恢复）

### 阶段 9：界面样式
- GridSplitter 左右分栏可调
- 主题系统（theme 文件夹 + light/green/paper/dark 四套）
- `confirmDelete` 删除确认开关

### 阶段 10：搜索
- 搜索框替换"笔记列表"标签，ICollectionView 动态过滤
- 标题/内容包含匹配；过滤时编辑区保持选中笔记

### 阶段 11：修复监控闪退（重要 Bug）
- 复制文件时文件被占用导致未捕获异常闪退
- 读取重试 + 防抖 + 延迟重排 + DispatcherUnhandledException 全局兜底

### 阶段 12：文件名=标题模型
- txt/md 笔记文件名即标题、内容纯正文；旧格式首行自动剥离
- 空文件（有文件名）正常显示

### 阶段 13：行号修复（TextBox 时代）
- `\r\n` 归一化、Padding 偏移、TextBlock.Measure 校准（0 误差验证）

### 阶段 14：字体配置扩展
- 列表标题/预览字体字号、标题/内容框字体颜色独立配置
- 应用字体改为 Lucida Fax

### 阶段 15：字体缩放
- `fontZoom`（Ctrl+Wheel），600ms 防抖写回 conf.json

### 阶段 16：AvalonEdit 替换（建立 git 仓库）
- master 备份 + avalonedit-editor 分支
- 编辑区换 AvalonEdit：内置行号（删除自绘行号器）、内置撤销/重做
- 超链接：仅 http/https、Ctrl+单击浏览器打开、`linkColor` 配置
- Text 非依赖属性 → 事件双向同步

### 阶段 17：品牌与图标
- 标题/托盘/弹窗统一为 FlashStickNote
- 程序生成闪电图标（7 尺寸），`icon` 配置 + 内嵌资源自提取

### 阶段 18：开机启动 / 静默启动
- `startWithWindows`（注册表 Run 键）、`silentStart`（防闪窗进托盘）

### 阶段 19：标准发布流程
- `publish.ps1` + `release-templates`（框架依赖 Release + 默认模板 + 主题 → zip）
- tag v1.0.0

### 阶段 20：自动启动后的键盘焦点
- 从托盘或全局热键显示窗口时，立即恢复编辑区键盘焦点，并在输入队列中再次确认
- 静默启动后首次唤出窗口即可使用窗口内 `Ctrl+N` 新建笔记

### 阶段 21：编辑区不可见字符显示
- `conf.json` 新增 `showSpaces`、`showLineFeed`、`showCarriageReturn`，默认均为关闭
- 使用 AvalonEdit 原生空格和行尾标记；Windows 的 CRLF 以一个行尾标记显示

### 阶段 22：末行剪切
- 无选区按 `Ctrl+X` 时，先选中当前完整逻辑行再执行 AvalonEdit 的标准剪切命令
- 最后一行及文档末尾的空行均可剪切

### 阶段 23：文件菜单与右键删除
- 左上角改为“文件”菜单，提供新建笔记、隐藏到托盘和退出
- 笔记项右键菜单删除当前项，删除确认和回收站行为保持不变

### 阶段 24：搜索匹配规则
- 搜索栏新增正则、全字匹配、区分大小写开关，默认关闭
- 无效正则表达式显示提示且不会让程序崩溃；匹配器由无依赖的控制台测试覆盖

## 历史分支

- `master`：阶段 1-15 的 TextBox 编辑区版本（自绘行号），保留作为回退点
