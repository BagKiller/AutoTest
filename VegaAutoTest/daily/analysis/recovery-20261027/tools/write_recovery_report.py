"""Write a supplemental engineering audit from preserved live-run artifacts."""
import argparse
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
import daily as d
import coverage_preparation as coverage

p=argparse.ArgumentParser()
p.add_argument('original',type=Path)
p.add_argument('campaign',type=Path)
a=p.parse_args()
original,campaign=a.original.resolve(),a.campaign.resolve()
s=d.load(campaign/'summary.json')
lines=['# 昨晚每日验证：故障修复与授权专项模拟', '',
       f'生成时间：{d.now_cn().isoformat()}。专项批次：`{campaign.name}`。', '',
       f"流程：{s['workflowState']}；验收：{s['acceptanceState']}；逐提交状态：{s['statusCounts']}；待收尾：{len(s['pendingCommitValidations'])}。", '',
       '## 根因与修复', '',
       '| 根因 | 修复及验证 |', '| --- | --- |',
       '| 原代理在第二项后台命令尚未退出时结束，后续三项未处理 | 保留执行会话并等待末行 JSON；新增 --check-complete 非空队列退出码 2；本次五项逐个 finish |',
       '| 将 30 秒等待结束误判为进程死亡，重复启动同一 SHA | 全局内核锁及 HEADLESS_DONE 重发门禁；进程死亡自动释放；原始六个 run 保留 |',
       '| 共享测试 bin/obj 导致不同构建 DLL 污染 | 每次每层独立 --artifacts-path，短路径 C:/AutoTest/test-artifacts，哈希映射归档；禁用共享编译服务 |',
       '| 普通 Xunit 逻辑断言被误判为基础设施 ERROR | 优先按错误首行识别 Assert./Xunit.Sdk.；初始化包装异常仍 ERROR |',
       '| 无 UI 的混合通过/失败被误判 CONTRACT_VIOLATION | 删除错误的 UI 矛盾推断；FAIL 与 ERROR 分类回归 |',
       '| 中断/并发 outcomes 写入遗漏原始结果 | 原子 JSON 写入；汇总只在 case/phase/目标 SHA 唯一时恢复原始结果索引，不改 result.json |',
       '| 预取消测试仍要求旧行为一组数据 | 根据现有产品循环首部取消语义改为零组且缓存未消费；没有修改产品源码或放松新契约 |',
       '| 只登记缺用例，未实际补测试 | 新增需求拆解、真实源链接/目标 DLL 测试及已准备 UI receipt 审核；冻结后不改断言 |',
       '| SDK8 被按错误目录判缺失 | 实际 SDK8 C:/AutoTest/runtime/dotnet；SDK10 dotnet10；最终 Runner 回归使用 SDK8/runtime8 |', '',
       '27 项隔离 Python 编排回归通过，日志：[individual-contracts-final.log](../../analysis/recovery-20261027/individual-contracts-final.log)。',
       'Runner 分类回归修复前 3 失败/1 通过；修复后最终验证 10/10 通过（含自定义消息、错配堆栈、消息前缀碰撞和非 Xunit 异常反例），证据：[contracts-final-verified](../../analysis/recovery-20261027/contracts-final-verified/contracts.trx)。',
       '补充运行曾错误使用 SDK10 运行 net8 testhost，因缺少 runtime8 中止；该日志保留，随后显式使用实际 SDK8/runtime8 重试。专项第一项另有长路径 MSB3030 错误；改用短独立 artifacts 后有限恢复，错误原始结果保留。', '',
       '## 逐提交结果与独立构建', '',
       '| SHA | 状态 | 目标与构建指纹 | 完成说明 |', '| --- | --- | --- | --- |']
for c in s['commits']:
    child=campaign/'commits'/c['sha']
    target=d.load(child/'execution-target.json')
    provenance=d.load(Path(target['evidence'])/'build'/'build-provenance.json')
    if provenance.get('commit')!=c['sha']:raise ValueError('Build SHA mismatch')
    if d.git('rev-parse','HEAD',repo=Path(target['repo']))!=c['sha']:raise ValueError('Current worktree HEAD mismatch')
    actual_dll=coverage.digest(Path(target['repo'])/'Vega/bin/Debug/net8.0-windows10.0.19041.0/win-x64/Vega.dll').upper()
    if actual_dll!=provenance['vegaDllSha256'].upper():raise ValueError('Built DLL fingerprint mismatch')
    lines.append(f"| `{c['sha'][:8]}` | {c['status']} | HEAD `{provenance['commit']}`；Vega.dll `{provenance['vegaDllSha256']}` | {d.md(c['completion']['reason'])} |")
lines+=['', '## Runner 原始四态与 runId', '',
        '下表含本专项所有原始 run（包括有限恢复前的 ERROR），不以最新通过覆盖旧证据。builtFromCurrentSource 字段原样保留；上表独立构建证明另附。', '',
        '| SHA | case | runId | verdict / code | PASS/FAIL/SKIP | 原始结果 |', '| --- | --- | --- | --- | --- | --- |']
for path in sorted((campaign/'commits').glob('*/runner/*/result.json')):
    r=d.load(path)
    counts={k:sum(x.get('result')==k for x in r.get('assertions',[])) for k in ('PASS','FAIL','SKIP')}
    link=path.relative_to(original).as_posix()
    lines.append(f"| `{r['target']['gitCommit'][:8]}` | {r['caseId']} | `{r['runId']}` | {r['verdict']} / {r['verdictCode']} | {counts['PASS']}/{counts['FAIL']}/{counts['SKIP']} | [result]({link}) |")
lines+=['', '## 必需 UI、未验范围和恢复', '',
        '- WI3690：真实 Vega100-PIC 官方授权登录；真实同 SHA 编辑器 Profile 切换、Filter、保存并重开。严格原始内容比较发现 18 处 Condition token 顺序变化，UI Runner FAIL；尚未证明语义变化或由此提交引入，不武断归为产品回归。dead-token/Feature 的真实显示仍未完整验证。',
        '- WI3693：真实 Vega300-HWv1 官方授权登录，原始 Normal/Oblique 输入实际离线后处理，新 waferRun 93355853799751680；UI/日志/KLARF 归档（GC/DC359、filtered82、events1274）。没有独立可信的多 collection SPA 输出基准，故不以非零输出替代验收，UI Runner BLOCKED。',
        '- WI3652：真实目标 Vega300-HWv1 授权登录/Idle、检测工作区及配方菜单已观察；导航后 UIA 连续为空，截图坐标操作有限恢复。Binning/Counters 删除、criteria 确认与即时 Summary 尚未完成；删除动作还受 computer-use 技能的操作时确认限制，无人值守下未执行。45 原始逻辑场景/46 记录不能替代 UI，Runner BLOCKED。界面中历史 wafer 结果明确不当作本 SHA 输出。',
        '- 两次锁文件提交：只改变 packages.lock.json，首项目标框架从 net8.0-windows10.0.19041 变为 windows7.0，后项恢复。第四项冻结测试误将合法 Project 节点视作必须有 resolved 的包，原始 ERROR/HARNESS_EXCEPTION 保留，不能归责产品；未回改冻结测试。第五项独立 v2 项目发现检查通过后冻结，但正式运行又暴露 RID 继承未处理（runtime.native.System.IO.Ports 存在于基框架、未重复出现在 win-x64 目标）以及自定义 Xunit 消息误归 ERROR；原始结果同样保留，不把它认定为产品缺陷。后续分类器已按匹配的 Xunit 类型/消息/测试堆栈修正；锁图 v3 使用独立新项目处理 Project/RID，4 个正反例 fixture 回归通过。v3 没有重跑或改判本次冻结提交。构建均明确 lockedRestore=false，未证明锁定还原可复现。没有 UI 改动，审核明确无需 UI。', '',
        '三个准备过的会话均 finally Stop/Restore；恢复输出和实际文件 SHA256 另见审计 JSON。没有会话仅因 Prepare 后认证失败而漏恢复。', '']
audit=[]
for child in (campaign/'commits').iterdir():
    receipt=d.load(child/'ui'/'ui-Vega100-PIC'/'evidence.json') or d.load(child/'ui'/'ui-Vega300-HWv1'/'evidence.json')
    session_audit={}
    if receipt:
        session_path=Path(receipt['sessionPath'])
        session=d.load(session_path/'session.json')
        if session['state']!='RESTORED':raise ValueError('Session not restored')
        session_audit=dict(sessionPath=str(session_path),process=session['process'],state=session['state'],hashes={})
        for name,expected in session['originalHashes'].items():
            current=coverage.digest(Path(session['bin'])/'Resources'/(name+'.json')).upper()
            if current!=expected.upper():raise ValueError('Runtime configuration not restored: '+name)
            session_audit['hashes'][name]=dict(expected=expected,actual=current,matched=True)
        machine_before=session_path/'MachineConfiguration.before.json'
        if machine_before.exists():
            expected=coverage.digest(machine_before)
            current=coverage.digest(Path(session['machineConfiguration']))
            if current!=expected:raise ValueError('MachineConfiguration not restored')
            session_audit['hashes']['MachineConfiguration']=dict(expected=expected,actual=current,matched=True)
    for path in child.glob('slots/*/restore.json'):
        value=d.load(path)
        if value.get('exitCode')!=0:raise ValueError('Restoration failed')
        audit.append(dict(path=str(path),sha256=coverage.digest(path),result=value,session=session_audit))
        lines.append(f"- [{child.name[:8]} 恢复证据]({path.relative_to(original).as_posix()})：{value['output'].strip()}")
d.save(original/'restoration-audit.json',audit)
lines+=['', '## 历史与安全边界', '',
        '这是用户明确授权、独立目录的新专项模拟，不是恢复历史冻结批次执行。原 manifest/diff/plan/需求审核/Runner 原始 result 保持不变，revalidation-link.json 固定哈希审计；原报告/summary 另存 before-revalidation 备份。原根报告只新增明确标注的专项关联。',
        '未重置用户 checkout，未改产品源码，未复用其他批次 DLL，未改工作项/PR/远端，未触发真机。原始 builtFromCurrentSource=false 不改写；独立构建 SHA 和 DLL 指纹另行证明。凭据只在官方工具或内存/子进程环境中使用，未写入本报告。',
        '每日游标仍为 collectedHead=38b7b39789212751b3f725f5c321dbc01614a5c0、collectedAt=2026-10-26T23:32:06.284502+08:00，pendingCommits=[]；未补收或自动重放历史队列。',
        '结论：编排故障已修复并完成本次五 SHA 闭环，但产品验收未全部通过。后续完整 UI/基准专项须重新准备独立计划，不能回改本次冻结断言。']
lines+=['', '## 最终环境状态', '',
        '本轮 Vega、ComponentConfigEditor、LoggingServer 均已退出。可选 NTFS 压缩仅部分完成，两个压缩会话已主动停止，没有删除原始结果或构建二进制。2026-10-27 18:11 时 C: 剩余约 5.36 GiB；独立构建及隔离测试占用较大，后续批次仍需磁盘预检，空间不足必须真实报告，不得将其当作无提交。未擅自清理历史批次。']
source_audit=[]
for child in (campaign/'commits').iterdir():
    plan=d.load(child/'plan.json')
    drift=[]
    for path,expected in plan.get('frozenCases',{}).get('files',{}).items():
        current=coverage.digest(d.ROOT/path)
        if current!=expected:drift.append(dict(path=path,frozenSha256=expected,currentSha256=current))
    source_audit.append(dict(sha=child.name,currentSourceDrift=drift))
d.save(original/'frozen-source-audit.json',source_audit)
if any(row['currentSourceDrift'] for row in source_audit):
    lines+=['', '## 冻结源文件审计发现的限制', '',
            '准备第三项时在共享 DailyRecovery 项目中增加/扩展了第三项的测试代码；这使先前已完成的第一、二项共享源文件与其冻结文件指纹不再一致。它们的已运行目标方法断言未被用于改判，原计划哈希和原始 Runner 结果仍保留，但不能声称当前工作区与全部早期冻结源码完全相同。此为本次执行的流程偏差，不隐瞒或升级为完整验收；早期结果仅保留已执行证据。第四项发现测试错误后，修正改用独立 v2 项目，未再改原共享测试源或重跑冻结项。',
            '逐文件差异见 [frozen-source-audit.json](frozen-source-audit.json)。今后后续提交补测必须新增独立项目/独立源码，不能编辑被任何已冻结计划引用的文件。']
(original/'recovery-report.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print(original/'recovery-report.md')
