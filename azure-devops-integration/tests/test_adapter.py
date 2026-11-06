import copy
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
import zipfile
from unittest.mock import patch, MagicMock
import subprocess

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import adapter as a


class AdapterTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.runs = self.root / "runs"
        self.path = self.runs / "synthetic_wi3478" / "result.json"
        self.registry = {"cases": [{"id": "wi3478", "workItems": [3478], "layers": {"uia": {}}}]}
        self.mapping = {"schemaVersion": "1.0", "mappings": [{"workItemId": 3478, "runnerCaseId": "wi3478",
                        "layers": ["uia"], "adoTestCaseId": None, "planId": None, "suiteId": None,
                        "configurationId": None, "testPointId": None}]}
        self.result = {"schemaVersion": "1.0", "runId": self.path.parent.name, "workItemId": 3478,
                       "caseId": "wi3478", "verdict": "PASS", "verdictCode": "OK", "verdictReason": "synthetic",
                       "startedAt": "2026-10-26T00:00:00Z", "finishedAt": "2026-10-26T00:00:01Z", "durationMs": 1000,
                       "target": {"repoRoot": "synthetic", "gitCommit": "a" * 40, "gitBranch": "test",
                                  "gitDirty": False, "vegaExe": "synthetic.exe", "rebuilt": True,
                                  "builtFromCurrentSource": True},
                       "channels": {"l1Api": {"available": False}, "l2Uia": {"available": True},
                                    "assertions": {"available": True}},
                       "assertions": [{"id": "a1", "kind": "ui-value", "result": "PASS"}],
                       "summary": {"total": 1, "passed": 1, "failed": 0, "skipped": 0}, "tests": [],
                       "evidence": {"dir": str(self.path.parent), "files": ["report.md"]}, "exitCode": 0}
        self.write()

    def write(self):
        a.save(self.path, self.result)
        (self.path.parent / "report.md").write_text("SYNTHETIC TEST ONLY", encoding="utf-8")

    def validate(self, code=None):
        return a.validate_result(self.path, self.runs, self.registry, 3478, "wi3478",
                                 self.result["exitCode"] if code is None else code)

    def prepare(self, out=None):
        return a.prepare(self.path, self.runs, self.registry, self.mapping, 3478, "wi3478",
                         self.result["exitCode"], out or self.root / "out")

    def release(self):
        receipt = {"runId": self.result["runId"], "releaseId": "synthetic-release",
                   "layers": ["uia"],
                   "resultSha256": hashlib.sha256(self.path.read_bytes()).hexdigest(),
                   "gitCommit": "a" * 40, "exeSha256": "b" * 64, "environmentId": "isolated-test",
                   "automationCommit": "c" * 40, "buildId": "synthetic-build"}
        receipt_path = self.root / "receipt.json"
        a.save(receipt_path, receipt)
        return {"releaseId": "synthetic-release", "suiteName": "synthetic", "baseline": receipt,
                "items": [{"workItemId": 3478, "caseId": "wi3478", "required": True,
                           "layers": ["uia"],
                           "releaseId": "synthetic-release", "resultPath": str(self.path),
                           "executionReceipt": str(receipt_path), "processExitCode": self.result["exitCode"],
                           "requiresL1Oracle": False}],
                "approvals": {k: {"approved": True, "evidence": "synthetic-evidence", "approvedBy": "test"}
                              for k in ("manualTests", "criticalBugs", "deviceSafety", "stationConfiguration")}}

    def gate(self):
        return a.aggregate(self.release(), self.registry, self.runs)["releaseDecision"]

    def test_T01_T04_preserve_four_states_and_exact_bytes(self):
        for verdict, code in a.CODES.items():
            with self.subTest(verdict=verdict):
                self.result.update(verdict=verdict, exitCode=code)
                self.write()
                original = self.path.read_bytes()
                manifest = self.prepare(self.root / verdict)
                self.assertEqual(manifest["runnerVerdict"], verdict)
                self.assertEqual(manifest["runnerRunId"], self.result["runId"])
                with zipfile.ZipFile(self.root / verdict / self.result["runId"] / "evidence.zip") as z:
                    self.assertEqual(z.read("result.json"), original)
                self.assertEqual(self.path.read_bytes(), original)
                if verdict != "PASS":
                    self.assertEqual(self.gate(), "NOT_READY")

    def test_T05_stale_build(self):
        self.result["target"].update(rebuilt=False, builtFromCurrentSource=False)
        self.write()
        self.assertEqual(self.gate(), "NOT_READY")
        self.assertTrue(self.prepare()["warnings"])

    def test_T06_stale_code(self):
        self.result["verdictCode"] = "OK_STALE_BUILD"
        self.write()
        self.assertEqual(self.gate(), "NOT_READY")

    def test_T07_T08_trx_never_overrides_blocked(self):
        self.result.update(verdict="BLOCKED", exitCode=2, verdictCode="ZERO_ASSERTIONS_MATCHED")
        self.result["tests"] = [{"outcome": "Passed"}]
        self.write()
        self.assertEqual(self.prepare()["runnerVerdict"], "BLOCKED")

    def test_T09_missing_run_id_and_invalid_json(self):
        del self.result["runId"]
        self.write()
        with self.assertRaises(a.IntegrationError):
            self.validate()
        self.path.write_text("not json", encoding="utf-8")
        with self.assertRaises(ValueError):
            self.validate()

    def test_T10_exit_conflict(self):
        with self.assertRaises(a.IntegrationError):
            self.validate(1)

    def test_T11_traversal(self):
        for field, value in (("dir", "../../"), ("files", ["../secret.txt"]), ("files", [str(self.path)])):
            with self.subTest(field=field, value=value):
                self.result["evidence"] = {"dir": str(self.path.parent), "files": ["report.md"]}
                self.result["evidence"][field] = value
                self.write()
                with self.assertRaises(a.IntegrationError):
                    self.validate()

    def test_T12_mapping_and_points(self):
        self.mapping["mappings"].append(copy.deepcopy(self.mapping["mappings"][0]))
        with self.assertRaises(a.IntegrationError):
            self.prepare()
        m = {"adoTestCaseId": 10, "planId": 20, "suiteId": 30, "configurationId": 40, "testPointId": 50}
        snapshot = {"planId": 20, "suiteId": 30, "complete": True, "points": []}
        with self.assertRaises(a.IntegrationError):
            a.resolve_point(m, snapshot)
        point = {"id": 50, "testCaseId": 10, "configurationId": 40}
        snapshot["points"] = [point]
        self.assertEqual(a.resolve_point(m, snapshot), point)
        snapshot["points"].append(point)
        with self.assertRaises(a.IntegrationError):
            a.resolve_point(m, snapshot)

    def test_T13_T14_offline_blocked_without_remote(self):
        self.assertEqual(self.prepare()["ado"]["publishStatus"], "BLOCKED")
        self.assertFalse(a.load(a.ROOT / "config/compatibility.json")["verified"])

    def test_T15_local_retry_preserves_history(self):
        self.prepare()
        with self.assertRaises(a.IntegrationError):
            self.prepare()

    def test_T16_required_oracle(self):
        spec = self.release()
        spec["items"][0]["requiresL1Oracle"] = True
        self.assertEqual(a.aggregate(spec, self.registry, self.runs)["releaseDecision"], "NOT_READY")
        self.assertIn("未进行 L1 交叉验证", self.prepare()["warnings"])

    def test_T17_distinct_runs_preserved(self):
        first = self.prepare()
        self.path = self.runs / "synthetic_second" / "result.json"
        self.result.update(runId=self.path.parent.name)
        self.result["evidence"]["dir"] = str(self.path.parent)
        self.write()
        second = self.prepare()
        self.assertNotEqual(first["runnerRunId"], second["runnerRunId"])
        self.assertEqual(len(list((self.root / "out").glob("*/evidence.zip"))), 2)

    def test_T18_execute_disabled(self):
        self.assertEqual(a.main(["publish", "--work-item", "3478", "--case", "wi3478", "--result",
                                str(self.path), "--process-exit-code", "0", "--execute"]), 3)

    def test_release_decisions(self):
        self.assertEqual(self.gate(), "READY_FOR_APPROVAL")
        spec = self.release()
        spec["approvals"] = {}
        self.assertEqual(a.aggregate(spec, self.registry, self.runs)["releaseDecision"], "NEEDS_HUMAN_REVIEW")
        spec["baseline"] = {}
        self.assertEqual(a.aggregate(spec, self.registry, self.runs)["releaseDecision"], "INCOMPLETE_EVIDENCE")

    def test_corrupt_summary_and_rebuild_contract(self):
        self.result["summary"]["total"] = 2
        self.write()
        with self.assertRaises(a.IntegrationError):
            self.validate()
        self.result["summary"]["total"] = 1
        self.result["target"]["rebuilt"] = False
        self.write()
        with self.assertRaises(a.IntegrationError):
            self.validate()

    def test_duplicate_json_keys(self):
        self.path.write_text('{"runId":"a","runId":"b"}', encoding="utf-8")
        with self.assertRaises(a.IntegrationError):
            a.load(self.path)

    def test_runner_invocation_captures_exit_and_last_line(self):
        process = MagicMock()
        process.__enter__.return_value = process
        process.communicate.return_value = ("diagnostic\n" + str(self.path) + "\n", "")
        process.returncode = 2
        with patch.object(a, "AUTOTEST", self.root), patch.object(a.subprocess, "Popen", return_value=process) as launch:
            path, code = a.invoke(3478, "wi3478", ["uia"], self.root, 10, self.registry)
            self.assertEqual((path, code), (self.path, 2))
            self.assertNotIn("--rebuild", launch.call_args.args[0])

    def test_runner_timeout_is_integration_failure(self):
        process = MagicMock()
        process.__enter__.return_value = process
        process.communicate.side_effect = [subprocess.TimeoutExpired("dotnet", 1), ("", "")]
        with patch.object(a.subprocess, "Popen", return_value=process), patch.object(a.subprocess, "run"):
            with self.assertRaises(a.IntegrationError):
                a.invoke(3478, "wi3478", ["uia"], self.root, 1, self.registry)
        process.kill.assert_called_once()

    def test_missing_layer_coverage_blocks_release(self):
        spec = self.release()
        spec["items"][0]["layers"] = ["logic"]
        self.assertEqual(a.aggregate(spec, self.registry, self.runs)["releaseDecision"], "INCOMPLETE_EVIDENCE")


if __name__ == "__main__":
    unittest.main()
