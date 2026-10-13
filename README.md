# VegaAutomation

Vega 自动化回归工程。编排层和确定性验证放在同一个目录里，被测产品源码不在这里。

```
VegaAutomation/
  code_indicator/     L0  编排（vega-agent、vega-mcp-server）
  VegaAutoTest/       L1  确定性验证（harness、用例、Runner）
```

被测程序仍在 `D:\Code\Vega\Vega_SW.ChenLR\Vega_SW.chen.ChenLR`。跑验证前设置：

```powershell
$env:VEGA_SW_ROOT = "D:\Code\Vega\Vega_SW.ChenLR\Vega_SW.chen.ChenLR"
```

`code_indicator` 调用 Runner 时按本目录相对定位，不需要再写 `VEGA_AUTOTEST_RUNNER`。入口说明见 `VegaAutoTest\README.md` 和 `VegaAutoTest\AI操作手册.md`。
