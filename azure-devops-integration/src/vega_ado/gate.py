"""Release gate. This is not a Runner verdict."""

from __future__ import annotations


def decide(items: list[dict]) -> dict:
    """Each item needs runnerVerdict, verdictCode, builtFromCurrentSource, runId, required, requireL1Oracle, l1Available."""
    reasons = []
    incomplete = False
    for item in items:
        label = f"WI {item.get('workItemId')}"
        if not item.get("required", True):
            continue
        if not item.get("runId"):
            incomplete = True
            reasons.append(f"{label} 没有本次 runId")
            continue
        verdict = item.get("runnerVerdict")
        if verdict != "PASS":
            reasons.append(f"{label} Runner verdict 是 {verdict}")
        if item.get("verdictCode") == "OK_STALE_BUILD" or not item.get("builtFromCurrentSource"):
            reasons.append(f"{label} 不是当前源码构建")
        if item.get("requireL1Oracle") and item.get("l1Available") is not True:
            reasons.append(f"{label} 缺少必需的 L1 交叉验证")
    if incomplete and not any("Runner verdict" in reason for reason in reasons):
        decision = "INCOMPLETE_EVIDENCE"
    elif reasons:
        decision = "NOT_READY"
    else:
        decision = "READY_FOR_APPROVAL"
    return {
        "releaseDecision": decision,
        "blockerReasons": reasons,
        "pendingApprovals": ["发布负责人审批"] if decision == "READY_FOR_APPROVAL" else [],
    }
