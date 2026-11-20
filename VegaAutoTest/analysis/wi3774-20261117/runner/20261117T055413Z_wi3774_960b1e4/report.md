# 工作项 #3774 验证报告

- 结论：验证工具异常（ERROR / HARNESS_EXCEPTION）
- 说明：HARNESS_EXCEPTION: 1 个用例异常（非断言失败）
- runId：`20261117T055413Z_wi3774_960b1e4`
- 用例：wi3774-special-20261117
- 断言：0 过 0（失败 0，跳过 0）
- 被测：commit `ab2ced6a`，分支 HEAD，Runner 未自行重编（builtFromCurrentSource=false；结论针对本次记录的二进制，来源另核对独立构建及指纹）
- 二进制构建时间：2026-11-17T11:58:20+08:00
- 起止：2026-11-17T13:54:12+08:00 → 2026-11-17T13:55:11+08:00（58.5 秒）

## 失败用例

- `Vega.AutoTest.Authored.SpaKlarfReproTests.AddingSpaPreservesExistingKlarfSelection`（Failed）：Vega.AutoTest.Harness.UiElementNotFoundException : UI element not found: KLARF settings tab (automationId=-, name=KLARF, type=TabItem). Nearby elements: [TabItem 'Vega.UI.Inspection.OptionSettingsViewModel'; Text 'Display'; Custom ''; Tab '' #OptionSettingsView_OptionSettingTabControl; TabItem 'Vega.UI.Inspection.OptionSettingViewModel'; Text 'Default'; Custom ''; Custom ''; Group '布局选择'; Text '布局选择'; Text '默认布局'; ComboBox '' #SelectLayoutView_DefaultLayoutComboBox; Text 'DSA布局'; ComboBox '' #SelectLayoutView_DSALayoutComboBox; TabItem 'Vega.UI.Inspection.OptionSettingViewModel'; Text '+'; DataGrid '' #OptionSettingsView_OptionSettingViewModelsDataGrid; Header '' #PART_ColumnHeadersPresenter; HeaderItem '设置'; Text '设置'; Thumb '' #PART_RightHeaderGripper; HeaderItem '保存选项'; Text '保存选项'; Thumb '' #PART_LeftHeaderGripper; Thumb '' #PART_RightHeaderGripper; HeaderItem 'Rejected'; Text 'Rejected'; Thumb '' #PART_LeftHeaderGripper; Thumb '' #PART_RightHeaderGripper; HeaderItem 'OverLoad'; Text 'OverLoad'; Thumb '' #PART_LeftHeaderGripper; Thumb '' #PART_RightHeaderGripper; DataItem 'Vega.UI.Inspection.OptionSettingViewModel'; HeaderItem ''; Thumb '' #PART_BottomHeaderGripper; Custom 'Default'; Text 'Default'; Custom '项: Vega.UI.Inspection.OptionSettingViewModel，列显示索引: 1'; ComboBox '']

## 断言

无结构化断言。

## 取证

`C:\AutoTest\AutoTest\VegaAutoTest\analysis\wi3774-20261117\runner\20261117T055413Z_wi3774_960b1e4`

