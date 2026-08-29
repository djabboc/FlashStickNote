# 配置文件参考

配置文件位于 **exe 同目录**。首次运行自动生成缺失的配置（默认值）；构建不会覆盖用户修改（配置不在 csproj 复制项中）。

## 默认值与校验

- `AppConfig` 和 `ShortcutConfig` 的全部字段都在代码中定义了默认值。配置文件缺失时会自动生成；已有配置缺少新字段时，缺少字段使用默认值。
- 配置文件无法解析时，程序记录日志并在本次运行使用默认值，不会自动覆盖原文件，便于修复错误配置。
- 部分值有运行时回退：未知主题回退为浅色主题，未知笔记格式回退为 JSON，非法颜色忽略并沿用主题色。
- 快捷键或字体大小等输入未做完整的预校验。无效快捷键会注册失败并写入日志；建议保持字号为正数、路径可访问且快捷键组合有效。

## conf.json（全部配置项及默认值）

```json
{
  "fontFamily": "Lucida Fax",            // 全局首选字体
  "fontFallbackFamilies": [                // 全局字体回退，按顺序使用
    "Microsoft JhengHei",
    "Arial",
    "Microsoft YaHei"
  ],
  "fontSize": 14.0,                      // 内容框字号
  "titleFontSize": 18.0,                 // 标题框字号
  "noteListFontSize": 13.0,              // 列表日期行字号
  "windowWidth": 920.0,                  // 默认窗口宽度（绝对值，最小 600）
  "windowHeight": 620.0,                 // 默认窗口高度（绝对值，最小 400）
  "windowLeft": null,                    // 默认左边坐标；与 windowTop 同时填写时生效
  "windowTop": null,                     // 默认上边坐标；null=居中启动
  "rememberWindowBounds": true,          // 默认记住最后的位置和大小

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
  "silentStart": false,                  // 启动后创建窗口句柄并直接进托盘，不显示窗口
  "hideListScrollbar": false,            // 隐藏左侧列表的垂直滚动条（隐藏后仍可用滚轮滚动）
  "hideEditorScrollbar": false,          // 隐藏右侧编辑区的垂直滚动条（隐藏后仍可用滚轮滚动）
  "hideTitleBar": false,                 // 隐藏系统标题栏（无边框窗口，边缘可调整大小；无论开关状态，菜单栏右侧、列表底部等空白区域均可拖动）
  "showSpaces": false,                   // 编辑区显示空格标记
  "showTabs": false,                     // 编辑区显示制表符标记
  "showEndOfLine": false,                // 编辑区显示行尾标记
  "showDocumentStatistics": false,       // 编辑区底部显示当前笔记的行数和字数

  // 以下留空 "" 表示跟随全局/主题
  "listTitleFontFamily": "",             // 列表标题字体
  "listTitleFontSize": 13.0,
  "listPreviewFontFamily": "",           // 列表预览字体
  "listPreviewFontSize": 11.0,
  "titleFontFamily": "",                 // 标题框字体
  "titleColor": "",                      // 标题框文字颜色（如 "#FF0000"）
  "contentFontFamily": "",               // 内容框字体
  "contentColor": "",                    // 内容框文字颜色
  "linkColor": "",                       // 超链接颜色（默认蓝色）
  "caretStyle": "line",                  // 正文光标：line | block | underline
  "caretWidth": 2.0,                       // line/underline 的粗细（1-12）
  "caretColor": "",                      // 正文光标颜色；留空跟随正文/主题
  "caretBlinkInterval": 530              // 闪烁间隔（毫秒）；0 表示常亮
}
```
`fontFamily` 是首选字体；`fontFallbackFamilies` 是有序回退列表。WPF 会优先使用首选字体，并在字体未安装或当前字符没有字形时依次回退。默认顺序为 Lucida Fax、Microsoft JhengHei、Arial、Microsoft YaHei。标题、正文和列表的单独字体配置仍可使用，且会共享该回退列表；`fontFamily` 和这些单独字体字段也兼容逗号分隔的字体名。

`windowLeft` 和 `windowTop` 必须同时为数值才按绝对坐标启动；任一为 `null` 时居中启动。`rememberWindowBounds=true`（默认）时，窗口停止移动、缩放或切换状态约 600ms 后会写回最后一次普通窗口矩形；托盘菜单的“退出”会立即写回。最大化状态保存其还原后的矩形。设为 `false` 可固定使用配置中的默认矩形；无效尺寸会回退至最小窗口大小。

AvalonEdit 将 Windows 的 `\r\n` 视为一个行结束单元并以单一行尾标记显示，因此使用 `showEndOfLine` 统一控制。旧配置中的 `showLineFeed` 或 `showCarriageReturn` 仍可读取并自动迁移为开启状态；下次应用保存配置时会写成新字段。

`showDocumentStatistics=true` 时，编辑区底部显示当前笔记正文的统计信息。行数按 CRLF、CR 或 LF 作为一次逻辑换行计算；字数统计非空白字符，空格、制表符和换行不计入。

`caretStyle` 默认 `line`，可选 `block` 或 `underline`；未知值回退为 `line`。`caretWidth` 限制为 1-12；`caretColor` 留空时跟随 `contentColor`，仍为空则跟随主题正文颜色。`caretBlinkInterval` 默认 530ms，`0` 表示常亮，其他值限制在 100-2000ms；光标移动或重新获得焦点时立即显示并重新开始计时。

## shortcut.json（全部快捷键及默认值）

```json
{
  "toggleWindow": "Ctrl+Shift+N",   // 全局：显示/隐藏（来回切换，窗口相关→全局）
  "newNote": "Ctrl+N",              // 窗口内：新建笔记（编辑相关→仅本程序获得焦点时生效）
  "hideWindow": "Ctrl+W",           // 窗口内：仅隐藏到托盘
  "fontZoom": "Ctrl+Wheel",         // 窗口内：字体缩放（支持 Ctrl/Alt/Shift + Wheel）
  "toggleWordWrap": "Alt+Z"         // 窗口内：切换自动换行（结果写回 conf.json 的 wordWrap）
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
