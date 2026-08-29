# 导航与置顶任务书（2026-08-29）

## 目标

为高频键盘操作补齐搜索、列表浏览与置顶笔记切换能力。实现必须保持现有编辑器撤销栈、搜索过滤和文件监控行为，不覆盖用户本地 `conf.json` 的既有个性化设置。

## N1 快捷导航

- `Ctrl+F`：将键盘焦点移至搜索框，并选中现有搜索词。
- `Ctrl+B`：将键盘焦点移至左侧笔记列表；若当前笔记被筛选隐藏，先清除筛选，再滚动到该项并聚焦列表。
- 两项均为窗口内 WPF 输入绑定，不注册全局热键。

**验收**：焦点位置正确；当前笔记在列表中可见；切换不会改写笔记内容。

## N2 置顶与持久化

- 笔记右键菜单提供“置顶/取消置顶”。
- 置顶项排在普通项之前，按最近更新时间排序；左侧用金色边线、浅色底纹和 `PIN` 标签区分。
- `conf.json` 的 `pinnedNotes` 保存置顶引用：JSON 笔记使用稳定的 `id:` 键；txt/md 使用相对笔记目录的 `file:` 键。旧版绝对路径值仍可识别。
- txt/md 重命名时同步更新引用；删除笔记时移除引用。

**验收**：重启后置顶仍生效；重命名后的 txt/md 仍置顶；取消置顶或删除后配置不残留对应引用。

## N3 F2 循环

- `F2` 在当前列表顺序的置顶笔记之间循环，末项回到首项。
- 目标项自动滚入可见范围。
- 切换前记录焦点，切换后恢复原焦点；在正文编辑器中按 F2 不会把输入焦点留在列表。

**验收**：三条置顶笔记连续按 F2 可循环；正文、标题、列表三种焦点均保持其原控件类别；无置顶项时无副作用。

## 验证与提交

1. `dotnet build FlashStickNote.csproj -c Release --no-restore`
2. `dotnet build FlashStickNote.csproj -c Debug --no-restore`
3. `dotnet run --project tests\FlashStickNote.Tests.csproj -c Release --no-restore`
4. `dotnet run --project tests\FlashStickNote.Tests.csproj -c Debug --no-restore`
5. 更新功能、配置、开发、调试、技术栈、变更日志及发布说明后提交本次代码；不提交用户已有的 `conf.json` 字体大小修改。