# 本次逐提交界面验收

这是 2026-10-27 提交专项复验的固定版本，按 `TargetUiAcceptance.Required` 列出的真实业务步骤检查本次会话证据。AI 必须通过 computer-use 实际操作目标软件，将截图/输出和预期对比写入当前提交的 `ui/<slot>/evidence.json`，再调用 daily.py 的 run-slot。

本工程负责审核证据，不代替软件操作。没有当前证据时只记录 SKIP；登记的 `minAssertions=1` 保证正式 Runner 返回 BLOCKED，不能把单独 dotnet test 的方法返回视为界面通过。测试核对目标 SHA、产品 DLL、进程、观察时间、必需步骤和证据文件校验值。

界面无法操作时，记录真实阻塞和启动/恢复尝试，不编造 evidence.json，不拿旧截图填充。共享源码在整批冻结前一次准备好；冻结后不得追加其他日期/SHA或修改断言，未来需要扩展时创建新版本工程。
