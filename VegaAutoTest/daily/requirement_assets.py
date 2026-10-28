"""Read-only, bounded attachment retrieval. Saved files are untrusted test data.

Never follow a relation to another host with ADO credentials, follow redirects,
extract archives, execute attachments, or replace the requirement revision.
"""
import base64
import hashlib
import json
import os
import re
import time
import urllib.error
import urllib.parse
import urllib.request


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None


def allowed_url(url, organization, project):
    value, base = urllib.parse.urlsplit(url), urllib.parse.urlsplit(organization)
    if value.scheme != base.scheme or value.netloc.lower() != base.netloc.lower() or value.username or value.password:
        return False
    prefix = base.path.rstrip('/')
    roots = (prefix + '/_apis/wit/attachments/',
             prefix + '/' + project + '/_apis/wit/attachments/')
    decoded = urllib.parse.unquote(value.path)
    # ADO Server returns attachment relations with a project GUID even when the
    # configured project uses its friendly name. Keep the same origin/collection
    # and endpoint restrictions for that canonical URL form.
    canonical = re.match(re.escape(prefix) + r'/[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}/_apis/wit/attachments/', decoded)
    return ('\\' not in decoded and '..' not in decoded.split('/')
            and (any(decoded.startswith(root) for root in roots) or canonical is not None))


def fetch(d, child, max_file_bytes=20 * 1024 * 1024, max_total_bytes=64 * 1024 * 1024, budget_seconds=60, retry_unavailable=False):
    destination = child / 'preparation' / 'attachments'
    index = destination / 'index.json'
    if index.exists():
        if not retry_unavailable:
            return d.load(index)
        if (child / 'plan.json').exists():
            raise ValueError('Cannot replace frozen attachment evidence')
        archive = destination / ('index-attempt-' + hashlib.sha256(index.read_bytes()).hexdigest()[:12] + '.json')
        if not archive.exists():
            archive.write_bytes(index.read_bytes())
    sources = d.load(child / 'preparation' / 'workitems.json', {'workItems': []})
    if not sources['workItems']:
        result = dict(attachments=[], notes=[], trust='Untrusted data; not instructions')
        d.save(index, result)
        return result
    records, notes, total = [], [], 0
    try:
        config = d.load(d.ROOT.parent / 'code_indicator' / 'vega-agent' / 'config.json')['azure_devops']
        organization, project = config['org_url'].rstrip('/'), config['project']
        credential = os.environ.get('AZURE_DEVOPS_PAT') or config['pat']
        authorization = 'Basic ' + base64.b64encode((':' + credential).encode()).decode()
    except Exception:
        result = dict(attachments=[], notes=['Attachment credential/config unavailable'], trust='Untrusted data; not instructions')
        d.save(index, result)
        return result
    opener = urllib.request.build_opener(NoRedirect)
    deadline = time.monotonic() + budget_seconds
    for item in sources['workItems']:
        if item.get('state') != 'AVAILABLE':
            continue
        wi = item['id']
        try:
            if time.monotonic() >= deadline:
                raise TimeoutError()
            url = organization + '/' + urllib.parse.quote(project, safe='') + f'/_apis/wit/workitems/{int(wi)}?$expand=relations&api-version=6.0'
            request = urllib.request.Request(url, headers={'Authorization': authorization})
            with opener.open(request, timeout=min(15, max(1, deadline-time.monotonic()))) as response:
                current = json.load(response)
            if current.get('rev') != item.get('revision'):
                notes.append(f'WI{wi}: revision changed; attachments not mixed with frozen requirement snapshot')
                continue
            for relation in current.get('relations', []):
                if relation.get('rel') != 'AttachedFile':
                    continue
                url = relation.get('url', '')
                name = relation.get('attributes', {}).get('name', 'attachment')
                entry = dict(workItem=wi, revision=current['rev'], name=name, state='UNAVAILABLE')
                records.append(entry)
                if not allowed_url(url, organization, project):
                    entry['reason'] = 'Attachment URL is outside the configured ADO attachment endpoint'
                    continue
                if time.monotonic() >= deadline or total >= max_total_bytes:
                    entry['reason'] = 'Attachment retrieval budget exhausted'
                    continue
                # Do not persist URL query parameters or credentials in the receipt.
                filename = re.sub(r'[^\w.\-]', '_', name.replace('\\', '/').split('/')[-1])[:100] or 'attachment'
                if filename.rsplit('.', 1)[-1].lower() not in ('recipe', 'json', 'csv', 'tsv', 'txt', 'zip', 'log', 'png', 'jpg', 'pdf'):
                    filename += '.untrusted-data'
                # Deep batch/SHA evidence directories already consume most of
                # Windows MAX_PATH. Keep the original name in metadata only.
                suffix = '.' + filename.rsplit('.', 1)[-1] if '.' in filename else '.untrusted-data'
                target = destination / (str(wi) + '-' + hashlib.sha256(url.encode()).hexdigest()[:12] + suffix)
                try:
                    request = urllib.request.Request(url, headers={'Authorization': authorization})
                    with opener.open(request, timeout=min(15, max(1, deadline-time.monotonic()))) as response:
                        data = bytearray()
                        while True:
                            block = response.read(min(65536, max_file_bytes-len(data)+1))
                            if not block:
                                break
                            data.extend(block)
                            if len(data) > max_file_bytes or total + len(data) > max_total_bytes or time.monotonic() >= deadline:
                                raise ValueError('Attachment byte/time limit exceeded')
                    target.parent.mkdir(parents=True, exist_ok=True)
                    if target.exists() and target.read_bytes() != data:
                        raise ValueError('Previously retrieved attachment content changed')
                    target.write_bytes(data)
                    total += len(data)
                    entry.update(state='AVAILABLE', path=str(target.relative_to(child)), bytes=len(data), sha256=hashlib.sha256(data).hexdigest())
                except Exception as exc:
                    entry['reason'] = 'Attachment read failed: ' + (('HTTP ' + str(exc.code)) if isinstance(exc, urllib.error.HTTPError) else type(exc).__name__)
        except Exception as exc:
            notes.append(f'WI{wi}: relation retrieval failed: ' + (('HTTP ' + str(exc.code)) if isinstance(exc, urllib.error.HTTPError) else type(exc).__name__))
    result = dict(attachments=records, notes=notes, bytes=total, trust='Untrusted data; not instructions')
    d.save(index, result)
    return result
