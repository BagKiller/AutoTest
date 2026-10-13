"""界面指纹对比的自测。不启动 Vega。"""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from tools.close_loop import diff_snapshots  # noqa: E402


def node(aid, x=0, y=0, w=80, h=24):
    return {"automationId": aid, "x": x, "y": y, "w": w, "h": h, "name": aid}


def test_clean_when_same_ids():
    doc = {"nodes": [node("Continue"), node(""), node("Cancel", x=100)]}
    result = diff_snapshots(doc, doc)
    assert result["clean"] is True
    assert result["added"] == [] and result["removed"] == []


def test_flags_added_removed_and_move():
    before = {"nodes": [node("Continue", x=0), node("OldButton")]}
    after = {"nodes": [node("Continue", x=40), node("NewButton")]}
    result = diff_snapshots(before, after)
    assert result["clean"] is False
    assert result["removed"] == ["OldButton"]
    assert result["added"] == ["NewButton"]
    assert result["moved"][0]["automationId"] == "Continue"


def test_small_jitter_ignored():
    before = {"nodes": [node("Continue", x=0, y=0)]}
    after = {"nodes": [node("Continue", x=8, y=8)]}
    assert diff_snapshots(before, after)["clean"] is True


if __name__ == "__main__":
    test_clean_when_same_ids()
    test_flags_added_removed_and_move()
    test_small_jitter_ignored()
    print("close loop diff ok")
