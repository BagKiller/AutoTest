"""Rebuild the read-only archive inventory and standalone review artifact."""
from pathlib import Path
from collections import Counter
import hashlib
import json
import re

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[2]
BASE = ROOT / 'daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00'

DAYS = [
    ['20261019', '10-19', 17, 'INCOMPLETE', '遗留进程占用构建输出；测试宿主反射/依赖/单例问题。补测核心320、补充18通过，完整UI/数据/硬件范围仍有缺口。'],
    ['20261020', '10-20', 10, 'INCOMPLETE', '两批缺用例9/11、工具异常各1；同一预取消测试错误期望1，被分类及契约检查进一步包装为ERROR。'],
    ['20261021', '10-21', 5, 'INCOMPLETE', '缺用例12、工具异常1；重放历史待验队列，重复相同预取消断言。'],
    ['20261022', '10-22', 5, 'INCOMPLETE', '缺用例15、工具异常1；新Pitch/DC、Notch需求未转化为验收用例，旧探针继续失败。'],
    ['20261023', '10-23', 11, 'INCOMPLETE', '10项缺用例、10项未收尾；提交报告显示没有成功目标构建、用例未执行。'],
    ['20261024', '10-24', 1, 'NO_COMMITS', '无新提交，待收尾0；不属于失败，也不代表历史问题已修复。'],
    ['20261025', '10-25', 1, 'NO_COMMITS', '无新提交，待收尾0；不属于失败。'],
    ['20261026', '10-26', 18, '复验 HAS_FAIL', '原宿主污染/重复推进/旧探针错误；复验1 FAIL（Condition排序）、2 BLOCKED（基准/UIA）、2 ERROR（锁图测试）。'],
    ['20261027', '10-27', 35, '原批次未完成；复验 INCOMPLETE', '原首项后9项PENDING；第一轮7个路径字符串误报；第二轮8 UI阻塞、1产品构建错误、1锁定还原缺口。'],
    ['20261028', '10-28', 14, '原批次未完成；复验 INCOMPLETE', '原批次6项未收尾；专项1调用异常、5缺覆盖，涉及生成失败/480秒超时，原始详细日志不在归档。'],
    ['20261029', '10-29', 22, 'HAS_FAIL；流程收尾', '2缺覆盖；Uniformity组件65/70门槛错配，五配置UI中4个Busy超时、1个NotNull；未进入业务比较。'],
]

def read_rows():
    rows = []
    for p in sorted(BASE.rglob('report.md')):
        raw = p.read_bytes()
        t = raw.decode('utf-8-sig')
        rel = p.relative_to(BASE)
        runner = '- runId：' in t
        kind = 'Runner' if runner else '原批次汇总' if len(rel.parts) == 2 else '提交报告' if 'commits' in rel.parts else '复验汇总'
        lines = t.splitlines()
        if runner:
            m = re.search(r'\b(PASS|FAIL|ERROR|BLOCKED) / ([A-Z_]+)', t)
            status = m.group() if m else '未提取'
            summary = next((x.removeprefix('- 说明：') for x in lines if x.startswith('- 说明：')), '')
            case = next((x.removeprefix('- 用例：') for x in lines if x.startswith('- 用例：')), '')
        else:
            states = [x for x in lines if any(s in x for s in ['总体验收：', '验收完整性：', '当前流程已收尾', '当前流程：'])]
            status = '；'.join(states) or '见报告正文'
            commit_lines = [x for x in lines if re.match(r'\| `[a-f0-9]{8}` \|', x)]
            summary = commit_lines[0].split('|')[-2].strip() if commit_lines else next((x for x in lines if '结论：' in x), status)
            case = ''
        observations = list(dict.fromkeys(x for x in lines if x.startswith('- `') or x.startswith('| FAIL')))
        rows.append(dict(path=rel.as_posix(), absolutePath=p.as_posix(), date=rel.parts[0][:8],
                         kind=kind, status=status, summary=summary, case=case, observations=observations,
                         bytes=len(raw), sha256=hashlib.sha256(raw).hexdigest(), text=t))
    return rows

rows = read_rows()
assert len(rows) == 139
counts = Counter(r['kind'] for r in rows)
assert counts == {'Runner': 57, '原批次汇总': 13, '提交报告': 65, '复验汇总': 4}
assert Counter(r['status'].split(' / ')[0] for r in rows if r['kind'] == 'Runner') == {'PASS': 33, 'ERROR': 18, 'FAIL': 5, 'BLOCKED': 1}
for day in DAYS:
    assert sum(r['date'] == day[0] for r in rows) == day[2]
(OUT / 'report-inventory.json').write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding='utf-8')

index = ['# 139份原报告索引', '', '按原批次日期归属，含次日专项复验。父子报告不能重复计为独立测试。摘要为原文提取，分析解释见 analysis.md。', '']
for i, r in enumerate(rows, 1):
    index += [f'**{i:03d}. [{r["path"]}]({r["absolutePath"]})**', '',
              f'- 类型：{r["kind"]}', f'- 原报告状态：{r["status"]}',
              f'- 摘要：{r["summary"]}', f'- SHA256：`{r["sha256"]}`']
    if r['case']:
        index.append(f'- 用例：{r["case"]}')
    if r['observations']:
        # Keep all unique failure lines for auditing without emitting a Markdown table.
        for line in r['observations']:
            index.append('- 失败证据：' + line.strip('| ').removeprefix('- '))
    index.append('')
(OUT / 'report-index.md').write_text('\n'.join(index), encoding='utf-8')

findings = [
    ['重复执行的机制', '10-20至10-22把前批未验收提交重新入队；同一坏探针与未补齐的覆盖反复出现。后续关闭历史自动重放，但欠账仍保留。'],
    ['共享基线放大故障', '预取消错误拖累整个daily-supplement；PMT路径分隔符错误使7个不同SHA都FAIL。失败数量不等于独立根因数量。'],
    ['公共UI前置被记作业务FAIL', 'WaitReady用Assert.Fail表示启动超时，主窗口用Assert.NotNull；Runner优先识别Xunit断言，导致五配置显示FAIL。'],
    ['生成、发现、执行之间缺校验', 'minAssertions由模型给出；Uniformity正常路径仅65条但登记70条。BF多个调用没有runId。需冻结前完整调用试运行。'],
    ['证据审核器没有操作端', 'DailyUi20261028只读取当前receipt；缺文件会SKIP。需要实际操作生产证据，重复审核不会补出真实UI流程。'],
    ['已确认的报告计数问题', 'Uniformity finally与基类Dispose两次Flush，记录器每次写完整列表；一次workflow失败被记录两遍。'],
    ['保留产品问题与未知项', 'bd9ccb55报告两次真实构建失败；Condition排序差异需核对语义要求；locked restore与模型失败的深层原因缺原始日志。'],
]

template = '''import { Stack, Row, H1, H2, Text, Table, TextInput, Select, useState, useHostTheme } from "cursor/canvas";
const days = __DAYS__;
const findings = __FINDINGS__;
const reports = __REPORTS__;
export default function VegaDailyFailureReview() {
  const theme = useHostTheme();
  const [date, setDate] = useState("all");
  const [kind, setKind] = useState("all");
  const [query, setQuery] = useState("");
  const selected = reports.filter(r => (date === "all" || r.date === date) && (kind === "all" || r.kind === kind) && (!query || (r.path + r.text).toLowerCase().includes(query.toLowerCase())));
  return <Stack gap={22} style={{padding:24, maxWidth:1200, margin:"0 auto", color:theme.text.primary, background:theme.bg.editor}}>
    <Stack gap={8}>
      <H1>Vega 每日验证：为什么一直没有完整通过</H1>
      <Text>2026-10-19—10-29 批次 · 分析于 2026-10-30 · 北京时间</Text>
      <Text weight="bold">主要阻力集中在用例准备、编排、测试宿主与UI前置。原始FAIL仍保留，但启动超时和探针错误不能直接证明业务回归。</Text>
      <Text tone="secondary">全部139份report.md：13原批次汇总 + 4复验汇总 + 65提交报告 + 57独立Runner报告。归档仅含Markdown，原始TRX/截图/构建日志未随包提供。</Text>
      <a href="__ANALYSIS__" style={{color:theme.text.link}}>打开完整分析、源码证据与修复优先级</a>
    </Stack>
    <Stack gap={8}>
      <H2>归档中独立 Runner 文档状态（份）</H2>
      <Table headers={["PASS","ERROR","FAIL","BLOCKED","合计"]} rows={[[33,18,5,1,57]]}/>
      <Text size="small" tone="secondary">仅统计57份独立Runner报告，含失败后重试；汇总引用但未打包的运行不计入。不是整个工程通过率。ERROR中17次契约违例、1次宿主失败；5次FAIL来自Uniformity五配置。</Text>
    </Stack>
    <Stack gap={8}>
      <H2>逐日原因与后续复验</H2>
      <Table headers={["批次日期","报告数（份）","结果口径","原因"]} rows={days.map(d => [d[1],d[2],d[3],d[4]])} />
      <Text size="small" tone="secondary">来源：指定归档目录全部report.md。数量按父批次归属，包含次日复验；父子报告不视为独立测试。10-24与10-25为无提交。</Text>
    </Stack>
    <Stack gap={12}>
      <H2>反复发生的原因</H2>
      {findings.map(([title,body]) => <div key={title} style={{display:"grid",gridTemplateColumns:"minmax(140px, 1fr) 3fr",gap:16,borderBottom:`1px solid ${theme.stroke.tertiary}`,paddingBottom:12}}><Text weight="bold">{title}</Text><Text>{body}</Text></div>)}
    </Stack>
    <Stack gap={10}>
      <H2>全部报告检索与原文</H2>
      <Row gap={10} wrap>
        <Select value={date} onChange={setDate} options={[{value:"all",label:"全部批次日期"}, ...days.map(d => ({value:String(d[0]),label:String(d[1])}))]} />
        <Select value={kind} onChange={setKind} options={["all","原批次汇总","复验汇总","提交报告","Runner"].map(k=>({value:k,label:k==="all"?"全部报告类型":k}))}/>
        <TextInput value={query} onChange={setQuery} placeholder="搜索 SHA、工作项、错误或用例" style={{minWidth:280}}/>
        <Text size="small" tone="secondary">匹配 {selected.length} / 139 份</Text>
      </Row>
      {selected.map(r => <details key={r.path} style={{borderBottom:`1px solid ${theme.stroke.tertiary}`,padding:"10px 0"}}>
        <summary style={{cursor:"pointer",overflowWrap:"anywhere"}}>{r.kind} · {r.path}</summary>
        <Stack gap={8} style={{padding:"12px 0"}}>
          <Text weight="bold">{r.status}</Text><Text>{r.summary}</Text>
          <a href={r.absolutePath} style={{color:theme.text.link}}>打开本地原报告</a>
          <Text size="small" tone="secondary">SHA256: {r.sha256}</Text>
          <pre style={{whiteSpace:"pre-wrap",overflowWrap:"anywhere",fontSize:12,lineHeight:1.6,background:theme.fill.quaternary,padding:12,maxHeight:600,overflow:"auto"}}>{r.text}</pre>
        </Stack>
      </details>)}
    </Stack>
  </Stack>;
}
'''
canvas = template.replace('__DAYS__', json.dumps(DAYS, ensure_ascii=False)).replace('__FINDINGS__', json.dumps(findings, ensure_ascii=False)).replace('__REPORTS__', json.dumps(rows, ensure_ascii=False)).replace('__ANALYSIS__', (OUT / 'analysis.md').as_posix())
(OUT / 'canvas-source.txt').write_text(canvas, encoding='utf-8')
print(json.dumps({'reports':len(rows),'types':dict(counts),'sourceHashesVerified':True,'canvasBytes':len(canvas.encode('utf-8'))}, ensure_ascii=False))
