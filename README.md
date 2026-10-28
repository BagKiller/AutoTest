# VegaAutomation

Vega 自动化回归工程。编排层和确定性验证放在同一个目录里，被测产品源码不在这里。

```
VegaAutomation/
  code_indicator/     L0  编排（vega-agent、vega-mcp-server）
  VegaAutoTest/       L1  确定性验证（harness、用例、Runner）
```

本机每日验证入口是 `VegaAutoTest/daily/daily.py`，完整调用顺序见 `VegaAutoTest/daily/AGENTS.md` 和 `daily/README.md`。使用独立 SHA worktree，不把共享输出目录当作目标构建。单项手跑可设置被测工程：

```powershell
$env:VEGA_SW_ROOT = "C:\AutoTest\workspaces\Vega_SW"
```

`code_indicator` 调用 Runner 时按本目录相对定位，不需要再写 `VEGA_AUTOTEST_RUNNER`。入口说明见 `VegaAutoTest\README.md` 和 `VegaAutoTest\AI操作手册.md`。
