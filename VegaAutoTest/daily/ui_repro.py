"""Work-item repro steps drive UI verification (2026-11-13 user rule).

If an ADO work item has repro steps or acceptance criteria, verification is ONE
UI test that executes those steps in order and checks the stated expectation,
with a screenshot per step. The steps come from the work item, never from the
model; the model only translates them into UI automation. Logic/component tests
do not substitute, and the commit's result is the UI result.
"""
import html
import re

REPRO_FIELD = 'Microsoft.VSTS.TCM.ReproSteps'
ACCEPTANCE_FIELD = 'Microsoft.VSTS.Common.AcceptanceCriteria'

_SECTION = re.compile(r'【\s*([^】]{1,12})\s*】')
_NUMBERED = re.compile(r'(?<![0-9A-Za-z.])(\d{1,2})\s*[、.．)）:：](?!\d)\s*')
_CAPTURE = re.compile(r'StepEvidence\s*\.\s*Capture\s*\([^;]*?"step-(\d+)', re.S)
_EXPECTED_KEYS = ('预期结果', '预期', '期望结果', '期望', 'expected', 'expected result')
_IGNORED_KEYS = ('实际结果', '实际', 'actual', 'actual result', '版本', 'version', '其他', '其它', 'other', '截图')
_PRECONDITION_KEYS = ('前置条件', '前提条件', 'precondition', 'preconditions')


def plain(value):
    """ADO rich text -> plain text; block tags become line breaks."""
    text = re.sub(r'(?i)<\s*(br|/p|/div|/li|/tr|/h\d)\s*/?\s*>', '\n', value or '')
    text = re.sub(r'<[^>]+>', ' ', text)
    text = html.unescape(text).replace('\xa0', ' ')
    lines = [re.sub(r'[ \t]+', ' ', line).strip() for line in text.splitlines()]
    return '\n'.join(line for line in lines if line)


def _sections(text):
    """Split '【key】value' sections; text before the first marker is section ''."""
    parts, last, key = {}, 0, ''
    for match in _SECTION.finditer(text):
        parts[key] = (parts.get(key, '') + ' ' + text[last:match.start()]).strip()
        key, last = match.group(1).strip(), match.end()
    parts[key] = (parts.get(key, '') + ' ' + text[last:]).strip()
    # '【预期结果】：xxx' -> 'xxx'
    return {k: v.lstrip('：: ').strip() for k, v in parts.items() if v.lstrip('：: ').strip()}


def _numbered(text):
    """'1、A 2、B' or one step per line -> ['A', 'B']."""
    marks = list(_NUMBERED.finditer(text))
    if len(marks) >= 2 or (marks and marks[0].start() == 0):
        steps = [text[m.end():(marks[i + 1].start() if i + 1 < len(marks) else len(text))].strip(' ；;。')
                 for i, m in enumerate(marks)]
    else:
        steps = [line.strip(' ；;。') for line in text.splitlines()]
    return [s for s in (re.sub(r'\s+', ' ', step).strip() for step in steps) if s]


def _matches(key, names):
    lowered = key.lower()
    return any(lowered.startswith(name) for name in names)


def spec_for(record):
    """Repro spec of one AVAILABLE work-item record, or None when it has no repro/acceptance text."""
    if record.get('state') != 'AVAILABLE':
        return None
    fields = record.get('fields') or {}
    repro = plain(fields.get(REPRO_FIELD, ''))
    acceptance = plain(fields.get(ACCEPTANCE_FIELD, ''))
    if not repro and not acceptance:
        return None
    steps, expected, preconditions = [], [], []
    for key, value in _sections(repro).items():
        if _matches(key, _EXPECTED_KEYS):
            expected.append(value)
        elif _matches(key, _PRECONDITION_KEYS):
            preconditions.append(value)
        elif _matches(key, _IGNORED_KEYS):
            continue
        else:  # 复现流程 / 复现步骤 / untitled body
            for step in _numbered(value):
                # Inline form without 【】: '期望：...' / '实际：...' lines.
                head = re.match(r'^\s*([^:：]{1,8})\s*[:：]\s*(.+)$', step)
                if head and _matches(head.group(1), _EXPECTED_KEYS):
                    expected.append(head.group(2).strip())
                elif head and _matches(head.group(1), _IGNORED_KEYS):
                    continue
                else:
                    steps.append(step)
    if acceptance:
        expected.append(acceptance)
        if not steps:
            # Acceptance-only items: the stated acceptance flow is the step list.
            steps = _numbered(acceptance)
    return dict(workItem=record['id'], title=plain(fields.get('System.Title', '')),
                preconditions=preconditions, steps=steps,
                expected=' / '.join(expected), source=REPRO_FIELD if repro else ACCEPTANCE_FIELD)


def specs(workitems, work_item_ids=None):
    """Repro specs for the given work items (all records when ids is None)."""
    wanted = None if work_item_ids is None else {int(w) for w in work_item_ids}
    result = []
    for record in (workitems or {}).get('workItems', []):
        if wanted is not None and int(record['id']) not in wanted:
            continue
        spec = spec_for(record)
        if spec and spec['steps']:
            result.append(spec)
    return result


def normalized(text):
    return re.sub(r'\s+', ' ', str(text or '')).strip()


def captured_steps(source):
    """Step numbers N used in StepEvidence.Capture(..., "step-N...") calls."""
    return {int(n) for n in _CAPTURE.findall(source or '')}
