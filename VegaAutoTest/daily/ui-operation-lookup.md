# 界面操作查表

写界面步骤或修复「找不到控件」之前先查本表。命中一行就按「正确操作」做，不要再走「失败做法」。

## 追加

- 只追加已经确认的打法。确认来源是用户当场看到的点击位置，或正式 Runner `result.json` 为 PASS 的 runId。
- 失败尝试、猜测、只读源码不能单独成行。
- 新行加在表末尾。不改已有行的 runId，不回写历史批次的计划和 `result.json`。
- 夜间生成不会自己打开本文件（`project_doc_max_bytes=0`）。`model_author.infer` 组请求时会附上全文。

## 查表

| 编号 | 要做的事 | 失败做法 | 正确操作 | 确认 |
| --- | --- | --- | --- | --- |
| ui-001 | 从主窗口进入配方 | 按英文名 `Inspection` 查找 | 点击 `InspectionButton`，再等到 `RecipeExplorerView`。旁边菜单是中文「配方」`#RecipeMenuItem`。 | PASS `20261117T063253Z_wi0_61abb46` |
| ui-002 | 打开配方编辑 | 停在检查入口 | 在配方树双击目标节点（本次为 Default Recipe），等到 `RecipeView_ListView`。 | 同上 |
| ui-003 | 打开结果输出的 KLARF 页 | 按 `TabItem.Name=KLARF` 查找 | `TabItem.Name` 是视图模型类型名。选内部 Text 含「KLARF」的类型页。树是 `CheckBoxTreeView_checkboxTreeTreeView`。 | 同上 |
| ui-004 | 在「采集设置」上打开「插入元素」，插入空间分析 | 右击折叠体、点标题文字、或用 `RecipeExpander_HeaderSiteToggleButton` 的 BoundingRectangle。该矩形可以在屏幕外（一次为 left=331 top=-1576）。点到标题行下方没有任何菜单。界面没有名为 SPA 的项；按测试进程语言取 Insert Element / Spatial Analysis 对不上中文菜单。 | 把 `RecipeView_ListView` 滚到顶部。在 `RecipeView_CollectionsExpander` 里取名为「采集设置」的 Text。右击必须落在这行文字的右侧空白：y 为文字垂直中线，x 为文字右缘 + 140，且不超过列表右缘 - 30。本次文字 (354,444)、48×16，点击 (542,452)。然后点 `RecipeExpander_GenericMenuItem`（插入元素，仅编辑态可用），再点 `RecipeExpander_InsertElementCommandMenuItem_4`（空间分析）。新元素是 `RecipeView_SpatialAnalysisExpander`。 | 用户 2026-11-17 观察；PASS `20261117T063253Z_wi0_61abb46` |

## ui-004 的界面事实

- 上下文菜单只挂在标题按钮 `HeaderSite`（`RecipeExpander_HeaderSiteToggleButton`）上，不挂在内容区。
- 标题文字左对齐。同一行向右的空白仍是标题按钮；再往下一点是内容，右击无反应。
- 中文界面文案是「采集设置」「插入元素」「空间分析」。不要用 `ProductResources` 的当前文化去对菜单名。

## 定位失败

找不到命中点时抛 `UiElementNotFoundException`。`UiPrerequisiteException` 只接受 `MAIN_WINDOW_TIMEOUT`、`LOGIN_NOT_READY`、`DEVICE_NEVER_IDLE`、`UI_NOT_READY`、`VEGA_PROCESS_EXITED`。自造代码会在消息形成前变成 `ArgumentException`。
