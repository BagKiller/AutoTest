# #3644 核心逻辑探针

本项目读取 `VEGA_SW_ROOT` 下 Vega 的现有二进制。测试机型只配置在独立测试宿主中，不修改 Vega.exe 的机型、授权或用户登录。

Runner 用例：`wi3644-core`。`logic100`、`logic500`、`logic-none` 每层启动独立进程，分别验证 Vega100、Vega500、无匹配机型的跨机工具上下文。每层 10 个测试、33 条结构化断言。

覆盖机型默认尺寸、XY 初始配置、历史尺寸回退、批量编辑器转换器、BFP 查询签名与实测宽度计算、BeamStability 空队列/未选中/尺寸不匹配时的拒绝路径。

批量编辑器转换器从当前检出源码链接编译；其他断言调用现有产品 DLL。BFP 与 BeamStability 检查使用隔离依赖或不执行构造函数的实例，仅调用计算/前置校验，不执行校准队列，不访问设备。

不覆盖完整校准页面操作、XY ViewModel 恢复流程、完整 CalculateWithBFP 调用链、硬件校准和真实算法服务器。PASS 仅适用于登记范围。

```powershell
$env:DOTNET_ROOT = 'C:\AutoTest\runtime\dotnet'
$env:PATH = "$env:DOTNET_ROOT;C:\Program Files\Git\cmd;$env:PATH"
$env:VEGA_SW_ROOT = 'C:\Vega\Vega_SW'
$env:NuGetAudit = 'false'
dotnet .\runner\Vega.AutoTest.Runner\bin\Debug\net8.0-windows\Vega.AutoTest.Runner.dll verify --work-item 3644 --repo-root $env:VEGA_SW_ROOT
```

在 `VegaAutoTest` 目录运行。结果以 Runner 生成的 `result.json` 为准；未使用 Runner 重编时，`builtFromCurrentSource=false`，不能把 PASS 当作当前源码已重新构建并完整验收。
