# 工作项 #3802 验证报告

- 结论：无法验证（BLOCKED / UI_NOT_READY）
- 说明：UI_NOT_READY: Vega.AutoTest.Harness.UiPrerequisiteException : [VEGA_UI_PRECONDITION:UI_NOT_READY] 诊断树没有 BPS；树=系统诊断:Vega.Data.Navigation.FixedNavigationNode / ComponentManager:Vega.Data.Navigation.FixedNavigationNode / 配置管理:Vega.Data.Navigation.FixedNavigationNode / 分析子系统:Vega.Data.Navigation.FixedNavigationNode / 设备诊断:Vega.Data.Navigation.FixedNavigationNode / EFEM控制:Vega.Data.Navigation.FixedNavigationNode / DataRecorder:Vega.Data.Navigation.FixedNavigationNode
- runId：`20261119T081222Z_wi3802_654c3fc`
- 用例：wi3802-bps-film-ui
- 断言：0 过 0（失败 0，跳过 0）
- 被测：commit `58606ec9`，分支 HEAD，Runner 未自行重编（builtFromCurrentSource=false；结论针对本次记录的二进制，来源另核对独立构建及指纹）
- 二进制构建时间：2026-11-19T16:00:47+08:00
- 起止：2026-11-19T16:12:22+08:00 → 2026-11-19T16:13:04+08:00（42.2 秒）

## 失败用例

- `Vega.AutoTest.L2.WorkItem3802L2.OpenBpsDiagnosticsFilmUi`（Failed）：Vega.AutoTest.Harness.UiPrerequisiteException : [VEGA_UI_PRECONDITION:UI_NOT_READY] 诊断树没有 BPS；树=系统诊断:Vega.Data.Navigation.FixedNavigationNode / ComponentManager:Vega.Data.Navigation.FixedNavigationNode / 配置管理:Vega.Data.Navigation.FixedNavigationNode / 分析子系统:Vega.Data.Navigation.FixedNavigationNode / 设备诊断:Vega.Data.Navigation.FixedNavigationNode / EFEM控制:Vega.Data.Navigation.FixedNavigationNode / DataRecorder:Vega.Data.Navigation.FixedNavigationNode

## 断言

无结构化断言。

## 取证

`C:\AutoTest\AutoTest\VegaAutoTest\analysis\cli-drill-20261111\runner\20261119T081222Z_wi3802_654c3fc`

