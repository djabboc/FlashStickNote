# 功能清单

## 窗口与托盘

- 关闭窗口 → 隐藏到托盘（真正退出用托盘菜单"退出"）
- 托盘双击 / 托盘"显示/隐藏"菜单 → 唤出窗口
- 托盘菜单：新建笔记、打开笔记目录、打开配置目录、退出
- 静默启动：`silentStart=true` 时只创建窗口句柄并直接进托盘，不会调用 `Show()`，避免窗口和任务栏闪现
- 单实例：`allowMultiInstance=false`（默认）时二次启动唤醒已有窗口后退出
- 开机启动：`startWithWindows=true` 时同步注册表 Run 键
- 窗口图标可配置（`icon`），默认内置闪电图标
- 窗口默认大小与绝对位置可由 `windowWidth`、`windowHeight`、`windowLeft`、`windowTop` 配置；Left/Top 留空时居中启动
- 默认记住窗口矩形（`rememberWindowBounds=true`）：停止移动/缩放约 600ms 后保存，托盘“退出”立即保存；设为 false 可固定使用配置矩形

## 全局快捷键（shortcut.json）

> 作用域原则：**窗口相关**功能用全局快捷键（窗口隐藏时也能唤出）；
> **编辑相关**功能只在 FlashStickNote 获得焦点时生效，绝不拦截其他程序。

| 键 | 默认 | 行为 | 作用域 |
|---|---|---|---|
| toggleWindow | Ctrl+Shift+N | 显示/隐藏窗口（来回切换） | 全局 |
| newNote | Ctrl+N | 新建笔记 | 窗口内 |
| hideWindow | Ctrl+W | 仅隐藏到托盘 | 窗口内 |
| fontZoom | Ctrl+Wheel | 编辑区字体缩放（上滚放大、下滚缩小，范围 8-72，600ms 防抖写回 conf.json） | 窗口内 |
| toggleWordWrap | Alt+Z | 切换编辑区自动换行（结果写回 conf.json 的 wordWrap） | 窗口内 |
| focusSearch | Ctrl+F | 聚焦搜索框并选中现有搜索词 | 窗口内 |
| focusNoteList | Ctrl+B | 聚焦左侧列表；当前笔记被筛选隐藏时恢复其可见性并滚入视野 | 窗口内 |
| cyclePinnedNotes | F2 | 在置顶笔记间循环，目标滚入视野后恢复原焦点 | 窗口内 |

## 编辑区（AvalonEdit）

- 标题框（上）+ 内容框（下），中间分隔条可拖调比例（左列最小 140、右列最小 300）
- 行号：内置精确行号，折行按逻辑行起点标注（`showLineNumbers`）
- 折行开关（`wordWrap`），关闭时横向滚动；`Alt+Z` 快速切换
- **Shift+滚轮**：横向滚动内容编辑区（折行关闭时有横向范围时生效）
- 可选统计栏：`showDocumentStatistics=true` 时在编辑区底部实时显示当前笔记正文的行数和字数
- 垂直滚动条可分别隐藏（`hideListScrollbar` / `hideEditorScrollbar`，隐藏后滚轮仍可滚动）
- 标题栏可隐藏（`hideTitleBar`）：无边框窗口，边缘可调整大小；无论是否隐藏标题栏，菜单栏右侧空白、列表底部空白和其他非交互背景均可拖动窗口
- 内置撤销/重做（Ctrl+Z / Ctrl+Y）
- 超链接识别：仅 http/https，蓝字+下划线（`linkColor` 可配），**Ctrl+单击**在默认浏览器打开
- 字体/颜色：标题、内容、行号、链接均可在 conf.json 配置（留空 = 跟随全局/主题）
- 字体回退：`fontFamily` 配合有序 `fontFallbackFamilies`，默认 Lucida Fax → Microsoft JhengHei → Arial → Microsoft YaHei
- 插入光标：`caretStyle`（line/block/underline）、`caretWidth`（1-12）、`caretColor`、`caretOpacity`（0-1，默认 0.8）和 `caretBlinkInterval`（毫秒，0 为常亮）可配置

## 左侧列表

- 文件菜单：仅点击“文件”按钮时打开，提供新建笔记、隐藏到托盘和退出；菜单栏、搜索区和列表的空白点击不会触发它
- 仅笔记项右键菜单可删除对应笔记；列表空白区右键不会弹出菜单。空白草稿切换到其他笔记或右键选中其他笔记时仍保留在列表中，删除仍遵从确认和回收站规则
- 搜索框动态过滤：标题或内容匹配即命中，输入即过滤、清空恢复；过滤隐藏选中项时编辑区保持原笔记；Ctrl+B 为定位当前笔记会自动清除会隐藏它的筛选
- 右键笔记项可“置顶/取消置顶”；置顶项始终排在普通项之前，金色边线、浅色底纹和 `PIN` 标签区分，状态写入 `pinnedNotes`
- F2 在置顶笔记间循环并使目标项滚入视野；保留原键盘焦点（在正文中按 F2 后仍可继续输入）
- 搜索规则：正则、全字匹配、区分大小写三个复选项均默认关闭；无效正则会在搜索栏下提示且不导致崩溃
- 内容预览：`listContentLines`（0-N 行，0 为关闭）
- 标题/预览字体字号独立配置
- 选中项高亮跟随主题

## 笔记管理

- 三种存储格式：`json`（含元数据）/ `txt`（文件名=标题，内容纯正文）/ `md`（同 txt）
- `notesFormat` 只决定**新建**笔记的格式，旧文件保持原格式，绝不强制转换
- `notesDir` 支持相对路径、绝对路径、`~/`、`%USERPROFILE%`；切换目录时自动搬入旧笔记（一次性，`.flashsticknote` 标记）
- 空笔记不落盘：无标题无内容不创建文件；内容被清空则删除文件；加载时忽略空文件
- 无标题时显示名 = 内容第一行（`EffectiveTitle`）
- txt/md 正文按文件原文加载；不猜测并剥离同名首行，避免正文恰好等于标题时发生数据丢失
- 删除：Del（列表焦点）/ 删除按钮，弹窗确认可关（`confirmDelete`），删除后自动选中下一条
- 回收站：删除的笔记移入 `notes/recycle/`，移回 notes 即恢复（同名自动加序号）
- 自动保存：输入停顿 600ms 防抖落盘，切换笔记/退出前强制保存

## 文件系统监控

- 监控笔记目录（FileSystemWatcher）：
  - 外部**新增**文件 → 自动载入列表（400ms 防抖，文件被占用时重试 5×120ms，仍失败延迟 1.5s 再试 2 次）
  - 外部**修改** → 同步标题/内容（正在编辑中的笔记不被覆盖）
  - 外部**删除** → 从列表移除（选中则自动选下一条）
  - 外部**重命名** → 笔记跟随（txt/md 标题随文件名变化）
- 防自写循环：程序自身写入 2 秒窗口内忽略监控事件；recycle 子目录忽略
- 全局兜底：`DispatcherUnhandledException` 记录日志阻止闪退

## 主题

- 内置 4 套：浅色 light / 护眼绿 green / 纸张黄 paper / 深色 dark
- `conf.json` 的 `theme` 指定；主题文件可改可新增；首次运行自动生成 theme 文件夹
- 覆盖范围：窗口/列表/头部/编辑区/行号/分隔条/按钮/选中态

## 发布

- 标准脚本 `publish.ps1`（框架依赖 Release 构建 + 默认模板 + 主题 → zip），详见 [release.md](release.md)
