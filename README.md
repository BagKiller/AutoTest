# VegaAutomation

Vega 自动化回归工程。编排层和确定性验证放在同一个目录里，被测产品源码不在这里。

```
VegaAutomation/
  code_indicator/     L0  编排（vega-agent、vega-mcp-server）
  VegaAutoTest/       L1  确定性验证（harness、用例、Runner）
```

每日验证由验证机的计划任务 `VegaAutoTest-DailyValidation` 每晚 23:30 运行 `VegaAutoTest/daily/Start-DailyValidation.ps1`（控制器 `autopilot.py`，CLI `daily.py`）。读取当天全部提交，逐提交分析需求和生成用例，全部用例在最新 HEAD 上只构建一次后执行，工作区固定为 `C:\AutoTest\workspaces\daily-latest`。规则见 `VegaAutoTest/daily/AGENTS.md`，用法见 `daily/README.md`。单项手跑可设置被测工程：

```powershell
$env:VEGA_SW_ROOT = "C:\AutoTest\workspaces\Vega_SW"
```

`code_indicator` 调用 Runner 时按本目录相对定位，不需要再写 `VEGA_AUTOTEST_RUNNER`。入口说明见 `VegaAutoTest\README.md` 和 `VegaAutoTest\AI操作手册.md`。
