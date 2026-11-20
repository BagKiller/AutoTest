# 工作项 #3796 验证报告

- 结论：已验证通过（PASS / OK）
- 说明：OK: 4/4 断言通过
- runId：`20261119T031451Z_wi3796_b7a9ef8`
- 用例：wi3796-histogram-ui
- 断言：4 过 4（失败 0，跳过 0）
- 被测：commit `ab2ced6a`，分支 HEAD，Runner 未自行重编（builtFromCurrentSource=false；结论针对本次记录的二进制，来源另核对独立构建及指纹）
- 二进制构建时间：2026-11-19T11:09:42+08:00
- 起止：2026-11-19T11:14:51+08:00 → 2026-11-19T11:16:02+08:00（71.1 秒）

## 断言

| 结果 | 编号 | 说明 | 期望 | 实际 |
| --- | --- | --- | --- | --- |
| PASS | attachment | 工单附件原字节 | 4d004f82732e818aea176a7bc42c789c9da85311dd91e70203ca7bf3e2d49b29 | 4D004F82732E818AEA176A7BC42C789C9DA85311DD91E70203CA7BF3E2D49B29 |
| PASS | imported-wafer | Wafer_1_202611094113 | Wafer_1_202611094113 | Wafer_1_202611094113 |
| PASS | gallery-wafer | Wafer_1_202611094113 | Wafer_1_202611094113 | Wafer_1_202611094113 / WorkSpaceOfflineJob_20261109134113242_Cycle1 |
| PASS | dna-oblique-lpd-bar | DNA Oblique LPD 直方图显示该缺陷 | visible bar, width ratio about 1/8 | baseline=132px plot=91px bar=9px ratio=0.099; screenshot=wi3796-histogram-screen.png |

## 取证

`C:\AutoTest\AutoTest\VegaAutoTest\analysis\manual-20261116\runner\20261119T031451Z_wi3796_b7a9ef8`

