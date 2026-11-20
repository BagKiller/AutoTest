# Azure DevOps 离线适配层

这个目录不修改 `Vega.AutoTest.Runner`，也不向 Azure DevOps 写入。

判定仍然只读 Runner 的 `result.json`。发布门禁是另一份 `release-gate.json`。样例映射里的 Plan、Suite、Test Case、Point 都是空的，空着就不能发布。

夜间检查仍由 `VegaAutoTest-NightlyAgent` 执行。这里的命令不会代替它。

```text
python -m vega_ado discover
python -m vega_ado validate-map --map config\test-case-map.sample.json --registry C:\AutoTest\AutoTest\VegaAutoTest\cases\registry.json
python -m vega_ado publish --result <result.json> --out out\publish
```

`--execute` 会拒绝。不要把 PAT 写进这个目录。
