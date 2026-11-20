"""Read-only server probe. No credentials are read from config files."""

from __future__ import annotations

import json
import urllib.error
import urllib.request
from pathlib import Path


def probe_collection(collection_base_url: str) -> dict:
    url = collection_base_url.rstrip("/") + "/_apis/projects?api-version=5.0"
    request = urllib.request.Request(url, method="GET")
    try:
        with urllib.request.urlopen(request, timeout=15) as response:
            body = response.read(500).decode("utf-8", "replace")
            return {"url": url, "status": response.status, "anonymous": True, "bodyPreview": body[:180]}
    except urllib.error.HTTPError as error:
        return {"url": url, "status": error.code, "anonymous": True, "authRequired": error.code in (401, 403)}
    except Exception as error:
        return {"url": url, "status": None, "error": type(error).__name__}


def write_discovery(out_dir: Path, compatibility: dict, report: str) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / "compatibility.json").write_text(
        json.dumps(compatibility, ensure_ascii=False, indent=2), encoding="utf-8")
    (out_dir / "discovery-report.md").write_text(report, encoding="utf-8")
