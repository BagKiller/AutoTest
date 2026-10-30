# 昨日提交每日任务模拟执行结果

新批次：`revalidation-20261028T164432_577c69`。按昨晚原始清单验证 2026-10-27 的 10 个提交；不采集今天提交，不推进每日游标。

另从服务器拉取并按 10 月 27 日完整日期窗口复核，当天恰好 10 个提交，没有遗漏昨晚收集之后的额外提交。

流程：FINISHED；待收尾：0；验收：INCOMPLETE。流程完成不等于全部产品通过。

成功独立构建：9/10；正式无界面 Runner 运行：30 次；PASS：30 次；结构化通过检查：3490。

| 提交 | 独立构建 | 无界面结果 | 界面结果/未验 | 本次提交状态 |
| --- | --- | --- | --- | --- |
| `00b3004a` | 成功 | PASS: 4 | ui-Vega300-HWv1: BLOCKED/ZERO_ASSERTIONS_MATCHED, ui-Vega500: BLOCKED/ZERO_ASSERTIONS_MATCHED | BLOCKED |
| `6b4cf4af` | 成功 | PASS: 3 | ui-Vega500: BLOCKED/ZERO_ASSERTIONS_MATCHED | BLOCKED |
| `227ace85` | 成功 | PASS: 3 | ui-Vega100-PIC: BLOCKED/ZERO_ASSERTIONS_MATCHED | BLOCKED |
| `bd9ccb55` | 失败 | 未执行（构建失败） | 未完成；见构建/阻塞证据 | ERROR |
| `19c18be5` | 成功 | PASS: 3 | ui-Vega100-PIC: BLOCKED/ZERO_ASSERTIONS_MATCHED | BLOCKED |
| `9fa008a9` | 成功 | PASS: 3 | ui-Vega300-HWv1: BLOCKED/ZERO_ASSERTIONS_MATCHED | BLOCKED |
| `d117a21d` | 成功 | PASS: 4 | ui-Vega300-HWv1: BLOCKED/ZERO_ASSERTIONS_MATCHED | BLOCKED |
| `eba9945a` | 成功 | PASS: 4 | ui-Vega300-HWv1: BLOCKED/ZERO_ASSERTIONS_MATCHED | BLOCKED |
| `b428717a` | 成功 | PASS: 3 | ui-Vega300-HWv1: BLOCKED/ZERO_ASSERTIONS_MATCHED | BLOCKED |
| `c30defb8` | 成功 | PASS: 3 | 不涉及界面变更 | NEEDS_CASE |

## 具体问题与边界

`bd9ccb55` 在原 SHA 上再次发生 CS0103：CaliCommonDataDefinition.cs 第 68、73 行引用不存在的 FileName。没有借用后续提交 DLL 或修改产品源码隐藏失败。

computer-use 初始化两次失败（含重置后重试）：`failed to write kernel assets: 系统找不到指定的路径。 (os error 3)`。本次记录了可构建提交的实际 Simulator 准备、启动和恢复尝试；没有完成官方认证后的真实目标操作和结果观察。缺少当前观察证据的 UI 用例返回 BLOCKED，不写假截图或假 PASS。

锁文件提交另实跑 locked restore：退出 1；原锁文件字节保持不变：True。图结构测试通过不能替代此项结果，具体诊断见提交目录 preparation/locked-restore。

锁定还原产生 NU1004，多个子项目的包/项目引用与根锁文件不匹配。另对 Vega.PD.ILaserDevice 使用产品默认 targets、去掉 Simulator 排除规则单独复核，仍退出 1 且锁文件不变；因此这不是仅由仿真构建排除原生卡项目造成的误报。产品 Directory.Build.props 将各子项目 NuGetLockFilePath 指向同一个根 packages.lock.json，是需要在产品仓库另行处理的依赖配置问题；本次没有修改历史产品提交。

10 个冻结计划和 5295 个原批次证据文件最终校验通过；已准备的会话配置和 MachineConfiguration 均核对恢复。

本次模拟还修正了：发现检查包含额外基线时的误报、发现阶段编译副本的隔离/回收，以及已返回 BLOCKED/ERROR 的界面仍须显示为未验。68 项 Python 流程回归通过。

[本批次逐提交报告](C:/AutoTest/AutoTest/VegaAutoTest/daily/runs/20261027T233318+0800_9ad645/revalidation-20261028T164432_577c69/report.md)；[本次机器汇总](C:/AutoTest/AutoTest/VegaAutoTest/daily/analysis/simulation-20261028/verification-summary.json)。
