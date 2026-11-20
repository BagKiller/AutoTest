# 工作项 #3774 验证报告

- 结论：验证工具异常（ERROR / HARNESS_EXCEPTION）
- 说明：HARNESS_EXCEPTION: 1 个用例异常（非断言失败）
- runId：`20261117T063708Z_wi3774_d791066`
- 用例：wi3774-special-v3-20261117
- 断言：1 过 1（失败 0，跳过 0）
- 被测：commit `ab2ced6a`，分支 HEAD，Runner 未自行重编（builtFromCurrentSource=false；结论针对本次记录的二进制，来源另核对独立构建及指纹）
- 二进制构建时间：2026-11-17T11:58:20+08:00
- 起止：2026-11-17T14:37:08+08:00 → 2026-11-17T14:38:24+08:00（76.1 秒）

## 失败用例

- `Vega.AutoTest.Authored.SpaKlarfReproTests.AddingSpaPreservesExistingKlarfSelection`（Failed）：Vega.AutoTest.Harness.UiElementNotFoundException : UI element not found: Collections header (automationId=-, name=Collections, type=Text). Nearby elements: [Button '采集设置' #RecipeExpander_HeaderSiteToggleButton; Text '采集设置'; Custom ''; Tab '' #CollectionsView_CollectionTabControl; TabItem 'Vega.UI.Inspection.CollectionViewModel'; Text 'Default'; Custom ''; Custom '晶圆扫描设置'; Group '晶圆扫描设置' #ScanWaferSettingView_ScanWaferSettingsExpander; Button '晶圆扫描设置' #HeaderSite; Text '晶圆扫描设置'; Text '产量'; ComboBox '' #ScanWaferSettingView_ThroughputComboBox; Group '晶圆尺寸'; Text '晶圆尺寸'; List '' #ScanWaferSettingView_WaferSizeListBox; ListItem '200 mm'; RadioButton '200 mm'; Text '200 mm'; ListItem '300 mm'; RadioButton '300 mm'; Text '300 mm'; Group '检测方向'; Text '检测方向'; RadioButton '正面' #ScanWaferSettingView_InspectionSideGroupRadioButton; Text '正面'; Custom '暗场设置'; Group '暗场设置' #DarkFieldSettingView_DarkFieldSettingsExpander; Button '暗场设置' #HeaderSite; Text '暗场设置'; Custom '选型'; Group '选型'; Text '选型'; Group '单位'; Text '单位'; Text 'LPD'; Text 'Haze'; RadioButton 'µmLSE' #DFSizingView_LPDGroupRadioButton; Text 'µmLSE'; Text 'nPPM']

## 断言

| 结果 | 编号 | 说明 | 期望 | 实际 |
| --- | --- | --- | --- | --- |
| PASS | precondition | KLARF 已有手工勾选项且非全选 | 0 < checked < total | 3 / 35 |

## 取证

`C:\AutoTest\AutoTest\VegaAutoTest\analysis\wi3774-20261117\v3\runner\20261117T063708Z_wi3774_d791066`

