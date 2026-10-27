from datetime import datetime, timezone, timedelta
import hashlib
import json
from pathlib import Path

root=Path(__file__).resolve().parent
build=json.loads((root/'build-after-expand/build-provenance.json').read_text(encoding='utf-8-sig'))
startup=json.loads((root/'startup-status.json').read_text(encoding='utf-8-sig'))
paths=sorted((root/'runner').glob('*/result.json'),key=lambda p:p.stat().st_mtime_ns)
path=paths[-1]
result=json.loads(path.read_text(encoding='utf-8-sig'))
assert result['caseId']=='wi3677-ui-evidence'
assert result['target']['gitCommit']==build['commit']==startup['targetHead']
repo=Path(build['repo'])
bin=repo/'Vega/bin/Debug/net8.0-windows10.0.19041.0/win-x64'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
assert sha(bin/'Vega.dll')==build['vegaDllSha256']
assert sha(bin/'RecipeBatchEditor/RecipeBatchEditor.dll')==build['batchEditorDllSha256']
restore=startup['restoration']
assert startup['stopCompleted'] and startup['restoreCompleted']
assert restore['state']=='RESTORED'
assert restore['componentActual']==restore['componentExpected']
assert restore['hardwareActual']==restore['hardwareExpected']
assert startup['machineRestoredHash']==startup['machineOriginalHash']
link=lambda title,p:f'[{title}](/{Path(p).as_posix()})'
data={'workItem':3677,'generatedAt':datetime.now(timezone(timedelta(hours=8))).isoformat(),
      'uiComplete':False,'verdict':result['verdict'],'verdictCode':result['verdictCode'],'runId':result['runId'],
      'result':str(path),'diskCapacityGB':119.90,'diskFreeAfterBuildGB':40.85,
      'targetHead':build['commit'],'vegaExeSha256':sha(bin/'Vega.exe'),'build':build,'startup':startup,
      'blocker':'Computer Use launch_app/list_windows timed out; reset and reinitialization did not restore the desktop-control connection.'}
(root/'ui-summary.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
md=['# #3677 扩容后 UI 复验记录','',f'时间：{data["generatedAt"]}。本次实际 UI 验收未完成。', '',
    f'Runner 原始 verdict：**{result["verdict"]} / {result["verdictCode"]}**；runId `{result["runId"]}`。', '',
    '## 已完成的操作','',
    '- 确认 C 盘已扩容至约 119.90 GB，开始检查时可用约 44.05 GB；构建后仍约 40.85 GB。原磁盘空间不足问题已解除。',
    '- 启动原有 MongoDB 数据目录，127.0.0.1:27017 检查通过；未新建空库。',
    f'- 在独立目录重新完整构建昨天目标 `{build["commit"]}`：主程序 0 错误/3821 警告，配套编辑器 0 错误/143 警告，DLL SHA256 已核对。使用 SDK10 构建，未使用旧批次输出。',
    f'- 准备 Vega300-HWv1 / Simulator / Gem=false / Gen2 隔离会话。真实启动 `{startup["process"]["image"]}`，PID `{startup["process"]["pid"]}`，启动时间 `{startup["process"]["startedAt"]}`。',
    f'- 20 秒启动检查中进程仍存在，窗口标题为 `{startup.get("mainWindowTitle","")}`，Responding={startup.get("responding")}; 未取得主窗口就绪证据。该短时进程检查不能判定产品启动故障或 UI 通过。',
    '- finally 停止本轮 Vega，恢复 ComponentConfig、HardwareProfiles 及 MachineConfiguration。下表 SHA256 与备份一致。', '',
    '## 当前阻塞及未执行步骤','',
    'Computer Use 的 Authenticator launch_app 超时；后续 list_windows 超时；重置 JavaScript 会话、重新导入 @oai/sky 并重试 list_windows 后仍超时。按工具技能恢复指引停止继续发送桌面输入。本次没有取得可用的界面操作或截图证据。', '',
    '| #3677 必需步骤 | 本次状态 |','| --- | --- |',
    '| 授权并完成 su 登录 | 未完成 |','| 导入原始 dual.recipe | 未执行 |','| 创建 Job 并选择配方 | 未执行 |',
    '| 离线 postprocess 添加原始 Normal/Oblique JSON | 未执行 |','| 执行并查看结果 | 未执行 |',
    '| 核对本轮 UI 产出的双 collection pitch、DC 合并结果 | 未执行，无本轮产出 |','',
    '桌面控制连接恢复前，不能完成上述实际 UI 验收。此前 22 项逻辑检查仍只代表此前逻辑覆盖，不用于替代本次 UI 结果。', '',
    '## 配置恢复证据','', '| 文件 | 原始 SHA256 | 恢复 SHA256 |','| --- | --- | --- |',
    f'| ComponentConfig | `{restore["componentExpected"]}` | `{restore["componentActual"]}` |',
    f'| HardwareProfiles | `{restore["hardwareExpected"]}` | `{restore["hardwareActual"]}` |',
    f'| MachineConfiguration | `{startup["machineOriginalHash"]}` | `{startup["machineRestoredHash"]}` |','',
    f'会话：`{startup["sessionPath"]}`；恢复状态 `{restore["state"]}`。未执行真机动作，未修改每日收集游标或历史 Runner 结果。', '',
    '## 原始证据','',f'- {link("Runner 原始 result.json",path)}',f'- {link("Runner 原生报告",path.parent/"report.md")}',
    f'- {link("启动与恢复审计",root/"startup-status.json")}',f'- {link("本次完整构建指纹",root/"build-after-expand/build-provenance.json")}',
    f'- {link("桌面工具故障及未完成步骤",root/"ui-evidence.json")}',f'- {link("机器可读汇总",root/"ui-summary.json")}', '',
    'Runner 的 builtFromCurrentSource=false 原样保留；独立构建指纹另附。UI 专项检查只审核实际操作证据，缺证据不会算 PASS。初次聚合 L2 工程编译遇到与 #3677 无关的 IncidenceFilterType/IncidenceOD3 引用错误，原始 ERROR 结果保留；随后仅隔离测试工程依赖，未改动六项验收要求或降低 minAssertions。', '',
    '## Runner 执行历史','', '| runId | 原始 verdict |','| --- | --- |']
for p in paths:
    r=json.loads(p.read_text(encoding='utf-8-sig'))
    md.append(f'| `{r["runId"]}` | {r["verdict"]}/{r["verdictCode"]} |')
(root/'report.md').write_text('\n'.join(md)+'\n',encoding='utf-8')
print(json.dumps({'report':str(root/'report.md'),'verdict':result['verdict'],'runId':result['runId']},ensure_ascii=True))
