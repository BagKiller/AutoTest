# 工作项 #3774 验证报告

- 结论：已验证通过（PASS / OK）
- 说明：OK: 5/5 断言通过
- runId：`20261117T064258Z_wi3774_f44e6cb`
- 用例：wi3774-special-v6-20261117
- 断言：5 过 5（失败 0，跳过 0）
- 被测：commit `ab2ced6a`，分支 HEAD，Runner 未自行重编（builtFromCurrentSource=false；结论针对本次记录的二进制，来源另核对独立构建及指纹）
- 二进制构建时间：2026-11-17T11:58:20+08:00
- 起止：2026-11-17T14:42:58+08:00 → 2026-11-17T14:44:41+08:00（103.4 秒）

## 断言

| 结果 | 编号 | 说明 | 期望 | 实际 |
| --- | --- | --- | --- | --- |
| PASS | precondition | KLARF 已有手工勾选项且非全选 | 0 < checked < total | 3 / 35 |
| PASS | spa-enabled | 新增 SPA 已启用 | True | True |
| PASS | spa-channel | SPA 使用原 KLARF 缺陷通道 | DW2 Default | DW2 Default |
| PASS | new-items-visible | 新增 SPA 缺陷及属性显示在选择数据中 | At least one new Cluster/Scratch/Slipline attribute | DW2 Default / Defects / Cluster / Area; DW2 Default / Defects / Cluster / X; DW2 Default / Defects / Cluster / Y; DW2 Default / Defects / Scratch / Area; DW2 Default / Defects / Scratch / Length; DW2 Default / Defects / Scratch / X; DW2 Default / Defects / Scratch / Y; DW2 Default / Defects / Slipline / Length; DW2 Default / Defects / Slipline / X; DW2 Default / Defects / Slipline / Y; DC Default / Defects / Cluster / Area; DC Default / Defects / Cluster / X; DC Default / Defects / Cluster / Y; DC Default / Defects / Scratch / Area; DC Default / Defects / Scratch / Length; DC Default / Defects / Scratch / X; DC Default / Defects / Scratch / Y; DC Default / Defects / Slipline / Length; DC Default / Defects / Slipline / X; DC Default / Defects / Slipline / Y |
| PASS | result | 新增的缺陷/属性只出现在选择数据列表中，默认不勾选；原有勾选状态保持不变。 | 新增的缺陷/属性只出现在选择数据列表中，默认不勾选；原有勾选状态保持不变。 | 新增项: DW2 Default / Defects / Cluster=False; DW2 Default / Defects / Cluster / Area=False; DW2 Default / Defects / Cluster / X=False; DW2 Default / Defects / Cluster / Y=False; DW2 Default / Defects / Scratch=False; DW2 Default / Defects / Scratch / Area=False; DW2 Default / Defects / Scratch / Length=False; DW2 Default / Defects / Scratch / X=False; DW2 Default / Defects / Scratch / Y=False; DW2 Default / Defects / Slipline=False; DW2 Default / Defects / Slipline / Length=False; DW2 Default / Defects / Slipline / X=False; DW2 Default / Defects / Slipline / Y=False; DC Default / Defects / Cluster=False; DC Default / Defects / Cluster / Area=False; DC Default / Defects / Cluster / X=False; DC Default / Defects / Cluster / Y=False; DC Default / Defects / Scratch=False; DC Default / Defects / Scratch / Area=False; DC Default / Defects / Scratch / Length=False; DC Default / Defects / Scratch / X=False; DC Default / Defects / Scratch / Y=False; DC Default / Defects / Slipline=False; DC Default / Defects / Slipline / Length=False; DC Default / Defects / Slipline / X=False; DC Default / Defects / Slipline / Y=False; 原有属性: DW2 Default / Defects / LPD / Size=True; DW2 Default / Defects / LPD / SN_Ratio=False; DW2 Default / Defects / LPD / X=True; DW2 Default / Defects / LPD / Y=True; DW2 Default / Defects / Extended / Area=False; DW2 Default / Defects / Extended / Size=False; DW2 Default / Defects / Extended / SN_Ratio=False; DW2 Default / Defects / Extended / X=False; DW2 Default / Defects / Extended / Y=False; DW2 Default / SURFImage / Original Std Classic=False; DW2 Default / SURFImage / Original Std Maximum=False; DW2 Default / SURFImage / Original Std Minimum=False; DW2 Default / SURFImage / Original Std Average=False; DN Default / Defects / LPD / Size=False; DN Default / Defects / LPD / SN_Ratio=False; DN Default / Defects / LPD / X=False; DN Default / Defects / LPD / Y=False; DN Default / Defects / Extended / Area=False; DN Default / Defects / Extended / Size=False; DN Default / Defects / Extended / SN_Ratio=False; DN Default / Defects / Extended / X=False; DN Default / Defects / Extended / Y=False; DN Default / SURFImage / Original Std Classic=False; DN Default / SURFImage / Original Std Maximum=False; DN Default / SURFImage / Original Std Minimum=False; DN Default / SURFImage / Original Std Average=False; DC Default / Defects / LPD / Size=False; DC Default / Defects / LPD / SN_Ratio=False; DC Default / Defects / LPD / X=False; DC Default / Defects / LPD / Y=False; DC Default / Defects / Extended / Area=False; DC Default / Defects / Extended / Size=False; DC Default / Defects / Extended / SN_Ratio=False; DC Default / Defects / Extended / X=False; DC Default / Defects / Extended / Y=False |

## 取证

`C:\AutoTest\AutoTest\VegaAutoTest\analysis\wi3774-20261117\v6\runner\20261117T064258Z_wi3774_f44e6cb`

