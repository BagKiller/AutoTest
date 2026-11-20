import json
import tempfile
import unittest
from pathlib import Path

from vega_ado.contract import ContractError, validate_result
from vega_ado.evidence import index_evidence
from vega_ado.gate import decide
from vega_ado.mapping import load_registry, resolve_point, validate_map
from vega_ado.publish import publish_dry_run

ROOT = Path(__file__).resolve().parents[1]
REGISTRY = Path(r"C:\AutoTest\AutoTest\VegaAutoTest\cases\registry.json")


def write_result(runs: Path, name: str, **overrides) -> Path:
    folder = runs / name
    folder.mkdir(parents=True)
    payload = {
        "schemaVersion": "1.0",
        "runId": name,
        "workItemId": 3478,
        "caseId": "wi3478",
        "verdict": "PASS",
        "verdictCode": "OK",
        "verdictReason": "OK",
        "exitCode": 0,
        "target": {
            "gitCommit": "abc",
            "gitDirty": False,
            "builtFromCurrentSource": True,
            "vegaExe": "Vega.exe",
        },
        "channels": {"l1Api": {"available": True}},
        "assertions": [{"id": "a1", "result": "PASS"}],
        "summary": {"total": 1, "passed": 1, "failed": 0, "skipped": 0},
        "evidence": {"dir": str(folder)},
    }
    payload.update(overrides)
    if "target" in overrides:
        payload["target"] = {
            "gitCommit": "abc",
            "gitDirty": False,
            "builtFromCurrentSource": True,
            "vegaExe": "Vega.exe",
            **overrides["target"],
        }
    path = folder / "result.json"
    path.write_text(json.dumps(payload), encoding="utf-8")
    return path


def check(path: Path, runs: Path, **kwargs):
    data = json.loads(path.read_text(encoding="utf-8"))
    return validate_result(data, result_path=path, runs_root=runs, **kwargs)


class OfflineTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.tmp = Path(self._tmp.name)

    def tearDown(self):
        self._tmp.cleanup()

    def test_t01_pass_keeps_verdict(self):
        path = write_result(self.tmp, "run-pass")
        checked = check(path, self.tmp)
        self.assertEqual(checked["verdict"], "PASS")
        self.assertEqual(checked["runId"], "run-pass")

    def test_t02_fail_exit_1(self):
        path = write_result(self.tmp, "run-fail", verdict="FAIL", verdictCode="ASSERTION_FAILED", exitCode=1)
        self.assertEqual(check(path, self.tmp)["verdict"], "FAIL")

    def test_t03_blocked_blocks_release(self):
        path = write_result(self.tmp, "run-blocked", verdict="BLOCKED", verdictCode="DEVICE_NEVER_IDLE", exitCode=2,
                            target={"builtFromCurrentSource": False})
        checked = check(path, self.tmp)
        decision = decide([{
            "workItemId": 3478, "required": True, "runId": checked["runId"],
            "runnerVerdict": checked["verdict"], "verdictCode": checked["verdictCode"],
            "builtFromCurrentSource": checked["builtFromCurrentSource"], "requireL1Oracle": False,
        }])
        self.assertEqual(decision["releaseDecision"], "NOT_READY")

    def test_t04_error(self):
        path = write_result(self.tmp, "run-error", verdict="ERROR", verdictCode="TEST_HOST_FAILED", exitCode=3)
        self.assertEqual(check(path, self.tmp)["verdict"], "ERROR")

    def test_t05_stale_binary_not_ready(self):
        path = write_result(self.tmp, "run-stale", target={"builtFromCurrentSource": False})
        checked = check(path, self.tmp)
        decision = decide([{
            "workItemId": 3478, "required": True, "runId": checked["runId"], "runnerVerdict": "PASS",
            "verdictCode": "OK", "builtFromCurrentSource": False, "requireL1Oracle": False,
        }])
        self.assertEqual(decision["releaseDecision"], "NOT_READY")

    def test_t06_ok_stale_build(self):
        path = write_result(self.tmp, "run-ok-stale", verdictCode="OK_STALE_BUILD",
                            target={"builtFromCurrentSource": False})
        checked = check(path, self.tmp)
        decision = decide([{
            "workItemId": 3478, "required": True, "runId": checked["runId"], "runnerVerdict": "PASS",
            "verdictCode": "OK_STALE_BUILD", "builtFromCurrentSource": False, "requireL1Oracle": False,
        }])
        self.assertEqual(decision["releaseDecision"], "NOT_READY")

    def test_t07_runner_blocked_wins_over_trx_story(self):
        path = write_result(self.tmp, "run-trx", verdict="BLOCKED", verdictCode="UI_NOT_READY", exitCode=2)
        self.assertEqual(check(path, self.tmp)["verdict"], "BLOCKED")

    def test_t08_zero_assertions_cannot_be_pass(self):
        path = write_result(self.tmp, "run-zero", verdict="PASS", verdictCode="ZERO_ASSERTIONS_MATCHED", exitCode=0)
        with self.assertRaises(ContractError) as caught:
            check(path, self.tmp)
        self.assertEqual(caught.exception.code, "CONTRACT_CONFLICT")

    def test_t09_missing_run_id(self):
        path = write_result(self.tmp, "run-noid", runId="")
        with self.assertRaises(ContractError):
            check(path, self.tmp)

    def test_t10_exit_and_verdict_conflict(self):
        path = write_result(self.tmp, "run-conflict", verdict="FAIL", exitCode=0)
        with self.assertRaises(ContractError) as caught:
            check(path, self.tmp, process_exit=0)
        self.assertEqual(caught.exception.code, "CONTRACT_CONFLICT")

    def test_t11_path_traversal(self):
        path = write_result(self.tmp, "run-travel", evidence={"dir": str(self.tmp.parent)})
        with self.assertRaises(ContractError) as caught:
            check(path, self.tmp)
        self.assertEqual(caught.exception.code, "PATH_TRAVERSAL")

    def test_t12_missing_point_is_not_publishable(self):
        registry = load_registry(REGISTRY)
        mapping = json.loads((ROOT / "config" / "test-case-map.sample.json").read_text(encoding="utf-8"))
        rows = validate_map(mapping, registry)
        self.assertTrue(rows)
        self.assertTrue(all(row["publishable"] is False for row in rows))
        with self.assertRaises(ContractError):
            resolve_point(mapping["mappings"], 3478, None, None, None)

    def test_t13_and_t18_execute_refused(self):
        path = write_result(self.tmp, "run-auth")
        with self.assertRaises(ContractError) as caught:
            publish_dry_run(path, runs_root=self.tmp, mapping=None, out_dir=self.tmp / "out",
                            log_path=self.tmp / "out" / "log.json", execute=True, approved=False)
        self.assertEqual(caught.exception.code, "NOT_AUTHORIZED")

    def test_t14_duplicate_point_fails(self):
        registry = {"cases": [{"id": "wi3478", "workItems": [3478], "layers": {"uia": {}}}]}
        mapping = {"schemaVersion": "1.0", "mappings": [
            {"workItemId": 3478, "runnerCaseId": "wi3478", "layers": ["uia"], "planId": 1, "suiteId": 2,
             "adoTestCaseId": 3, "configurationId": 4, "testPointId": 5},
            {"workItemId": 3478, "runnerCaseId": "wi3478", "layers": ["uia"], "planId": 1, "suiteId": 2,
             "adoTestCaseId": 3, "configurationId": 4, "testPointId": 6},
        ]}
        with self.assertRaises(ContractError) as caught:
            validate_map(mapping, registry)
        self.assertEqual(caught.exception.code, "MAP_INVALID")

    def test_t15_published_key_is_not_duplicated(self):
        path = write_result(self.tmp, "run-retry")
        log = self.tmp / "out" / "publication-log.json"
        log.parent.mkdir()
        log.write_text(json.dumps([{
            "key": "None|None|run-retry", "publishStatus": "PUBLISHED", "testRunId": 42,
        }]), encoding="utf-8")
        manifest = publish_dry_run(path, runs_root=self.tmp, mapping=None, out_dir=self.tmp / "out", log_path=log)
        self.assertEqual(manifest["ado"]["publishStatus"], "ALREADY_PUBLISHED")
        self.assertEqual(manifest["ado"]["testRunId"], 42)

    def test_t16_l1_missing_blocks_when_required(self):
        path = write_result(self.tmp, "run-l1", channels={"l1Api": {"available": False}})
        checked = check(path, self.tmp)
        decision = decide([{
            "workItemId": 3478, "required": True, "runId": checked["runId"], "runnerVerdict": "PASS",
            "verdictCode": "OK", "builtFromCurrentSource": True, "requireL1Oracle": True,
            "l1Available": checked["l1Available"],
        }])
        self.assertEqual(decision["releaseDecision"], "NOT_READY")

    def test_t17_two_run_ids_stay_distinct(self):
        first = write_result(self.tmp, "run-a")
        second = write_result(self.tmp, "run-b")
        self.assertNotEqual(check(first, self.tmp)["runId"], check(second, self.tmp)["runId"])
        self.assertTrue(index_evidence(first.parent, self.tmp)["files"])

    def test_invalid_json_rejected(self):
        folder = self.tmp / "bad"
        folder.mkdir()
        path = folder / "result.json"
        path.write_text("{", encoding="utf-8")
        with self.assertRaises(ContractError) as caught:
            publish_dry_run(path, runs_root=self.tmp, mapping=None, out_dir=self.tmp / "out",
                            log_path=self.tmp / "out" / "log.json")
        self.assertEqual(caught.exception.code, "INVALID_JSON")


if __name__ == "__main__":
    unittest.main()
