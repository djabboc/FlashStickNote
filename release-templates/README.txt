FlashStickNote v%VERSION%
========================

绿色便携闪念笔记软件：所有数据（配置、主题、笔记）都保存在程序目录中，
可整体放到 U 盘等任意位置使用。

运行要求
--------
- Windows 10 / 11 (x64)
- .NET 9 桌面运行时（Windows Desktop Runtime 9.0）：
  https://dotnet.microsoft.com/zh-cn/download/dotnet/9.0
  未安装时启动会弹窗提示，按提示下载安装即可。

快速上手
--------
1. 解压后双击 FlashStickNote.exe
2. Ctrl+Shift+N  全局快捷键：显示/隐藏窗口（唯一全局热键，不会误伤其他程序）
3. Ctrl+N        窗口内：快速新建笔记
4. Ctrl+W        窗口内：隐藏到托盘（只隐藏，不切换）
5. Del           焦点在左侧列表时：删除选中笔记（移入回收站，弹窗可关）
6. Ctrl+滚轮     编辑区字体缩放（自动保存到 conf.json）
7. Alt+Z         窗口内：切换编辑区自动换行（自动保存到 conf.json）
8. Ctrl+单击链接 用浏览器打开（仅识别 http/https）

目录与文件
----------
- conf.json      全部配置：字体、主题、笔记目录/格式、开机启动、静默启动等
- shortcut.json  快捷键配置
- theme/         主题文件夹（内置 light/green/paper/dark 四套，可修改或新增）
- notes/         笔记存放目录（路径由 conf.json 的 notesDir 决定）
- notes/recycle/ 回收站：删除的笔记移到这里，移回 notes 即恢复
- log.txt        运行日志（排查问题用）

版本：v%VERSION%
