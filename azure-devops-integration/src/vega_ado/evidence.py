"""Package evidence that already sits inside the allowed runs root."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    digest.update(path.read_bytes())
    return digest.hexdigest()


def index_evidence(evidence_dir: Path, runs_root: Path) -> dict:
    root = runs_root.resolve()
    folder = evidence_dir.resolve()
    folder.relative_to(root)
    files = []
    for path in sorted(p for p in folder.rglob("*") if p.is_file()):
        relative = path.relative_to(folder).as_posix()
        if relative.startswith("../") or Path(relative).is_absolute():
            raise ValueError("refusing a path outside the evidence directory")
        files.append({
            "path": relative,
            "bytes": path.stat().st_size,
            "sha256": _sha256(path),
        })
    return {"evidenceDir": str(folder), "files": files}


def write_index(index: dict, destination: Path) -> Path:
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(index, ensure_ascii=False, indent=2), encoding="utf-8")
    return destination
