"""Summarize independent Runner results without modifying any raw result.json."""
from datetime import datetime, timezone, timedelta
import hashlib
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

out = Path(__file__).resolve().parent
repo = Path(r'C:\AutoTest\workspaces\individual-20261022-20261023')
head = '10c33df5e69aa932bea8d843a29d8f6530d1fd73'
def git(*args):
    return subprocess.check_output(['git', '-c', f'safe.directory={repo.as_posix()}', '-C', str(repo), *args], encoding='utf-8').strip()
def link(label, path):
    return f'[{label}](/{Path(path).as_posix()})'
assert git('rev-parse','HEAD') == head
dirty = git('diff', '--name-only').splitlines()
assert all(p == 'packages.lock.json' for p in dirty), f'Unexpected product source edits: {dirty}'
proof = json.loads((out / 'build-reference.json').read_text(encoding='utf-8'))
for item in proof['dlls']:
    assert hashlib.sha256(Path(item['path']).read_bytes()).hexdigest() == item['sha256']
proof.update(verifiedAt=datetime.now(timezone(timedelta(hours=8))).isoformat(),
             sdkBuild='10.0.402', sdkTest='8.0.426', sourceFilesMatchHead=True,
             generatedDirtyFiles=dirty, fullVegaExeBuilt=False,
             configurationRestoration={'required':False,'reason':'No product GUI/configuration/session changed; fixture config is confined to the test-host output.'})
(out / 'build-reference.json').write_text(json.dumps(proof, indent=2, ensure_ascii=False), encoding='utf-8')
cases = [
    dict(sha='8c8583d50d3592ed718b8223bf415a54076bb1c5', case='wi3672-logic', name='#3672：CoW 拟合与 Notch offset 跳变',
         purpose='工作项明确要求：对同一组 Edge 数据反复分析时，CoW 不再在相差 20+ μm 的两点之间跳动。提交修改 Kåsa 初值、RANSAC 固定种子、内点带宽和 LM 径向 RMS 护栏。',
         coverage=[
             '已知圆心 (31,-47) μm、半径 150000 μm 的理想圆：代数拟合圆心/半径误差不超过 1e-6 μm。',
             '四段短弧输入：圆心误差不超过 1e-5 μm。',
             '真实 WaferAlignmentService.FindCOWCircleFitting：720 点合成圆拟合圆心误差小于等于 1 μm，随后重复调用 10 次，输出严格一致。',
             'RANSAC：120 个圆内点与 3 个明确离群点，连续 30 次返回相同的 120/3 分组，未走 fallback。',
             '0/1/2 点、共线输入、无足够共识分别检查退化返回和 fallback 标记；实际 service 两点输入返回 NaN。'],
         gaps='缺少工单截图对应的原始 Edge BIN，未做该现场数据的重复回放、最终 Notch theta 测量或真实 EdgeChuck 设备验收。测试覆盖实际算法模块及服务方法，但没有触发硬件或完整诊断页流程。'),
    dict(sha='8d0882910c5dbb1bfa5803f74f926a0a7eb33c2a', case='commit-8d088291-logic', name='PR4975：Notch 假候选与阈值包夹过滤',
         purpose='只接受有效的阈值包夹交点；无效边缘点不参与平均半径和候选；同步维护点、有效性掩码及窗口索引，防止 EdgeChuck 选中假 Notch。该提交没有工作项号，按提交 SHA 独立验证。',
         coverage=[
             '直接编译链接该提交自带测试源文件，调用本次新构建的真实 WaferAlignmentService DLL；通过结构化包装器逐条记录原断言，不改原测试预期。',
             'FindBracketedNotchEdgeInColumn：7 条，覆盖跳过未包夹点对继续内搜、合法插值、等强度、列角度偏移及整列无交点。',
             'FindNotchEdgesForIndexes：4 条，覆盖混合有效列、越界索引、掩码长度错误、无掩码旧路径。',
             'NotchCoarseSearchCore：6 条，覆盖候选最小半径、有效点均值分母、全无效、掩码长度错误、空数组与无效邻域拒绝。',
             'NotchFineSearchValid：6 条，覆盖抛物线顶点、有效点不足、掩码错误、顶点越界、边界支撑不足与全无效。',
             'ExtractNotchWindow：4 条，覆盖空/错长掩码、同步切片及原始索引映射、空窗口。'],
         gaps='未取得提交描述里的真实 BIN 与 StartTheta=3.090740/3.118 两种现场回放数据，因此未独立复核真实 Notch 角度。未做 VacuumChuck 实物数据回放和完整诊断页操作。'),
    dict(sha='10c33df5e69aa932bea8d843a29d8f6530d1fd73', case='wi3677-logic', name='#3677：离线后处理 pitch 缺失与 DC 不合并',
         purpose='工作项原始步骤是导入 dual recipe、创建 job、选择离线 postprocess 并添加 Normal/Oblique JSON。提交为新后处理 collection 设置 recipe throughput 推导的 PitchInMicron，并校验 JSON pitch 覆盖值。',
         coverage=[
             '真实 PopulateCollectionRunFromJson：使用独立合成配方和配置，将 Normal pitch=18 μm、Oblique pitch=36 μm 写入 CollectionRunInfo，并检查 300000 μm wafer size。校准服务为空结果、授权服务不授予特性，图像/采集通道在测试配方中关闭。',
             '真实 SetupForCollectAsync 的 ReproPostProc 路径：保留已有 25 μm pitch 和唯一 collection，避免覆盖持久化实测值。',
             'JSON 16 个边界组合：缺字段、null、两个字段名、数字字符串、零/负数、非法字符串、NaN/Infinity、10 倍边界内外、字段优先级。',
             'de-DE 当前区域设置下，字符串 19.5 按 invariant culture 正确解析。',
             '读取工单两个原始 Normal/Oblique JSON，确认顶层均无 pitch 字段，调用真实 ApplyJsonPitch 后保留 18 μm 兜底。',
             '已从工作项下载两份原始算法 JSON 和加密 dual.recipe，记录哈希。配方附件未在桌面中导入。'],
         gaps='尚未执行 su 登录后的完整创建 job/离线后处理界面流程；未将原始 dual.recipe 与两份 JSON 送入整个处理引擎，也未量化最终 DC 合并数量或 SPA 聚类输出。本次证明 pitch 初始化、校验和保留逻辑，不能替代完整 DC 合并验收。')
]
rows = []
for item in cases:
    paths = sorted((out / item['case']).glob('*/result.json'), key=lambda p:p.stat().st_mtime_ns)
    result_path = paths[-1]
    result = json.loads(result_path.read_text(encoding='utf-8-sig'))
    assert result['caseId'] == item['case']
    assert result['target']['gitCommit'] == head
    assert result['verdict'] in ('PASS','FAIL','BLOCKED','ERROR')
    assert result['target']['builtFromCurrentSource'] is False
    diff = git('show', '--format=fuller', '--first-parent', '--no-ext-diff', item['sha'])
    (out / (item['sha'][:8] + '.diff')).write_text(diff, encoding='utf-8')
    summary = result['summary']
    md = [f'# {item["name"]}：逐提交复验报告', '', f'提交 `{item["sha"]}`。', '', item['purpose'], '',
          f'本次 Runner 原始 verdict：**{result["verdict"]} / {result["verdictCode"]}**；结构化检查 {summary["passed"]}/{summary["total"]}；runId `{result["runId"]}`。', '',
          f'被测版本为昨天最终 HEAD `{head}`；本次按提交各自设计和执行独立测试，共用此确定版本的重新构建模块。未逐个 checkout 三个历史 SHA 做隔离二分。', '',
          '## 实际执行的验证', '', *['- '+line for line in item['coverage']], '',
          '## 覆盖边界与未验项', '', item['gaps'], '',
          '本项登记为 `partial-acceptance`，算法/逻辑 PASS 不等于整条工作项或整机验收。', '',
          '## 来源与证据', '',
          f'- {link("原始 result.json",result_path)}', f'- {link("Runner 原生报告",result_path.parent / "report.md")}',
          f'- {link("本次独立构建及 DLL SHA256",out / "build-reference.json")}',
          f'- {link("提交原始 diff",out / (item["sha"][:8]+".diff"))}', '',
          'Runner 没有调用其自身 --rebuild，故 `builtFromCurrentSource=false` 原样保留；其内置字段不能证明本次模块构建。附加构建证据与测试内 SHA256 比对证明所调用 DLL 来自本次独立目录；未使用旧批次 DLL，未构建/启动完整 Vega.exe。', '',
          '## 逐用例原始结果', '', '| 用例 | 结果 |', '| --- | --- |']
    md += [f'| {test["name"].replace("|","/")} | {test["outcome"]} |' for test in result['tests']]
    md += ['', '## 历次执行记录', '', '| runId | 原始 verdict | 检查数 |', '| --- | --- | --- |']
    for path in paths:
        old = json.loads(path.read_text(encoding='utf-8-sig'))
        md.append(f'| `{old["runId"]}` | {old["verdict"]}/{old["verdictCode"]} | {old["summary"]["passed"]}/{old["summary"]["total"]} |')
    md += ['', '早期锁文件冲突、测试适配代码编译/容器配置异常属于测试准备阶段，原始 ERROR 全部保留；正式结果取本文件列出的最终 runId，未编辑历史 Runner 结果。', '']
    report = out / (item['sha'][:8] + '-report.md')
    report.write_text('\n'.join(md), encoding='utf-8')
    rows.append(dict(commit=item['sha'], title=item['name'], caseId=item['case'],runId=result['runId'],
                     verdict=result['verdict'],verdictCode=result['verdictCode'],summary=summary,
                     report=str(report), result=str(result_path), acceptance='PARTIAL', gaps=item['gaps']))

attachments = [{'name':p.name,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'bytes':p.stat().st_size}
               for p in out.iterdir() if p.suffix == '.recipe' or ('Normal_' in p.name or 'Oblique_' in p.name) and p.suffix=='.json']
data = dict(date='2026-10-22',generatedAt=proof['verifiedAt'],targetHead=head,
            selection='Three commits with committer date 2026-10-22, verified against refreshed remote master history; no backlog included',
            results=rows,attachments=attachments,fullAcceptance=False)
(out / 'summary.json').write_text(json.dumps(data, indent=2, ensure_ascii=False),encoding='utf-8')
md = ['# 2026-10-22 三条提交逐项复验', '', f'复验时间：{proof["verifiedAt"]}。远端重新刷新后确认昨天只有以下 3 条新增提交，历史积压未纳入。', '',
      '本次逐条分析原始 diff、读取 #3672/#3677 的实际工作项要求，补充专属测试，并顺序独立调用 Runner。没有重用昨晚的四个批次回归结果。', '',
      '| 提交 | 验证对象 | 本次原始 verdict | 检查 | runId | 详细报告 |', '| --- | --- | --- | --- | --- | --- |']
for row in rows:
    md.append(f'| `{row["commit"][:8]}` | {row["title"]} | {row["verdict"]}/{row["verdictCode"]} | {row["summary"]["passed"]}/{row["summary"]["total"]} | `{row["runId"]}` | {link("逐条报告",row["report"])} |')
md += ['', '三组共 59 项结构化检查。PASS 代表上述实际执行范围；三条均保留为部分覆盖，完整桌面流程、现场数据与最终 DC 合并等未验项见各自报告。', '',
       f'独立源码目录：`{repo}`；目标为昨天最终 HEAD `{head}`。构建 SDK10，测试宿主 SDK8/.NET8；对 37 个项目 DLL 核对 SHA256。相关模块 WaferAlignment 构建 0 错误/231 警告，WaferRun 构建 0 错误/384 警告。', '',
       '完整 Vega.exe 未构建，未启动 GUI、Authenticator、Simulator 或真机；未修改产品模型配置，无产品配置恢复动作。测试容器配置仅写在独立测试宿主输出。用户 checkout、每日游标和已停用积压队列均未更改。', '',
       'Runner `builtFromCurrentSource=false` 未改动；另附独立构建指纹。独立 worktree 中只有 unlocked restore 生成的 packages.lock.json 变化，产品源文件与目标 HEAD 一致。', '',
       f'- {link("独立构建指纹",out/"build-reference.json")}', f'- {link("机器可读汇总及附件指纹",out/"summary.json")}', '',
       '测试库新增 wi3672-logic、commit-8d088291-logic、wi3677-logic 三个部分验收条目。初期测试适配异常保留在各条目的运行历史中；未改产品代码、未改 Runner verdict 语义或历史 result.json。', '']
(out / 'report.md').write_text('\n'.join(md),encoding='utf-8')
print(json.dumps({'report':str(out/'report.md'),'results':rows},ensure_ascii=False))
