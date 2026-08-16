# 配置文件参考

配置文件位于 **exe 同目录**。首次运行自动生成缺失的配置（默认值）；构建不会覆盖用户修改（配置不在 csproj 复制项中）。

## conf.json（全部配置项及默认值）

```json
{
  "fontFamily": "Microsoft YaHei UI",   // 全局默认字体
  "fontSize": 14.0,                      // 内容框字号
  "titleFontSize": 18.0,                 // 标题框字号
  "noteListFontSize": 13.0,              // 列表日期行字号

  "notesDir": "notes",                   // 笔记目录：相对 exe / 绝对路径 / ~/ / %USERPROFILE%
  "notesFormat": "json",                 // 新建笔记格式：json | txt | md（旧文件保持原格式）

  "allowMultiInstance": false,           // false=单实例（重复启动唤醒已有窗口）
  "newNoteFocus": "title",               // 新建后聚焦：title | content
  "listContentLines": 0,                 // 列表内容预览行数，0=关闭
  "showLineNumbers": false,              // 编辑区行号
  "wordWrap": true,                      // 自动换行（false=横向滚动）

  "theme": "light",                      // 主题名，对应 theme/{name}.json
  "confirmDelete": true,                 // 删除笔记前弹窗确认
  "icon": "",                            // 图标文件（相对 exe 或绝对路径），空=默认闪电图标

  "startWithWindows": false,             // 开机启动（注册表 HKCU Run 键）
  "silentStart": false,                  // 启动后直接进托盘

  // 以下留空 "" 表示跟随全局/主题
  "listTitleFontFamily": "",             // 列表标题字体
  "listTitleFontSize": 13.0,
  "listPreviewFontFamily": "",           // 列表预览字体
  "listPreviewFontSize": 11.0,
  "titleFontFamily": "",                 // 标题框字体
  "titleColor": "",                      // 标题框文字颜色（如 "#FF0000"）
  "contentFontFamily": "",               // 内容框字体
  "contentColor": "",                    // 内容框文字颜色
  "linkColor": ""                        // 超链接颜色（默认蓝色）
}
```

## shortcut.json（全部快捷键及默认值）

```json
{
  "toggleWindow": "Ctrl+Shift+N",   // 全局：显示/隐藏（来回切换，窗口相关→全局）
  "newNote": "Ctrl+N",              // 窗口内：新建笔记（编辑相关→仅本程序获得焦点时生效）
  "hideWindow": "Ctrl+W",           // 窗口内：仅隐藏到托盘
  "fontZoom": "Ctrl+Wheel"          // 窗口内：字体缩放（支持 Ctrl/Alt/Shift + Wheel）
}
```

- 作用域原则：**窗口相关**（toggleWindow）注册为全局热键；**编辑相关**（newNote/hideWindow/fontZoom）只在 FlashStickNote 窗口内生效，不会在 VS Code 等其他程序里误触发
- 全局快捷键支持 Ctrl/Alt/Shift/Win + 任意键（如 `Ctrl+N`、`F1`、`Space`、`Delete`）
- **注意**：全局快捷键会拦截系统级按键（如全局注册 Ctrl+W 会让浏览器关标签失效），破坏性组合（Del、Ctrl+W 等）请用窗口内快捷键
- fontZoom 必须包含至少一个修饰键 + `Wheel`，否则禁用（避免劫持普通滚轮）

## theme/{name}.json（主题文件）

```json
{
  "name": "浅色",                       // 显示名（仅日志用）
  "windowBackground": "#F5F5F5",
  "listBackground": "#F5F5F5",
  "listHeaderBackground": "#ECECEC",
  "listHeaderForeground": "#333333",
  "listForeground": "#1E1E1E",
  "listSecondaryForeground": "#999999", // 列表预览文字
  "listTertiaryForeground": "#BBBBBB",  // 列表日期文字
  "listSelectedBackground": "#D6E4F0",
  "listSelectedForeground": "#1E1E1E",
  "editorBackground": "#FFFFFF",
  "editorForeground": "#1E1E1E",
  "lineNumberBackground": "#FAFAFA",    // 预留（AvalonEdit 行号底与编辑区一致）
  "lineNumberForeground": "#999999",
  "borderColor": "#DDDDDD",
  "splitterColor": "#E0E0E0"
}
```

内置 4 套：`light`（浅色）、`green`（护眼绿）、`paper`（纸张黄）、`dark`（深色）。
自定义主题：在 theme 文件夹新建 `{名字}.json`，`conf.json` 的 `"theme": "{名字}"` 引用。
