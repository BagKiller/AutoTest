"""P6 闸门闭环自测。只用临时 git 仓库，不碰被测主仓库。"""

import os
import subprocess
import sys
import tempfile
import types
from pathlib import Path

# 自测不依赖本机是否装了 psutil；execute_cmd 的白名单与进程枚举无关。
if "psutil" not in sys.modules:
    sys.modules["psutil"] = types.ModuleType("psutil")

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from tools.policy import check_pull_request_allowed, check_push_allowed  # noqa: E402
from tools.repair import (  # noqa: E402
    commit_repair,
    reset_repair_session,
    start_code_repair,
    write_repair_file,
)
from tools.system_ops import execute_cmd  # noqa: E402


def git(repo: Path, *args: str) -> None:
    env = {
        **os.environ,
        "GIT_AUTHOR_NAME": "repair-test",
        "GIT_AUTHOR_EMAIL": "repair-test@example.com",
        "GIT_COMMITTER_NAME": "repair-test",
        "GIT_COMMITTER_EMAIL": "repair-test@example.com",
    }
    subprocess.run(["git", *args], cwd=repo, check=True, env=env,
                   capture_output=True, text=True)


def expect(cond: bool, label: str) -> None:
    if not cond:
        raise SystemExit(f"FAIL {label}")
    print(f"OK   {label}")


def main() -> None:
    os.environ["GIT_AUTHOR_NAME"] = "repair-test"
    os.environ["GIT_AUTHOR_EMAIL"] = "repair-test@example.com"
    os.environ["GIT_COMMITTER_NAME"] = "repair-test"
    os.environ["GIT_COMMITTER_EMAIL"] = "repair-test@example.com"

    with tempfile.TemporaryDirectory() as tmp:
        repo = Path(tmp)
        git(repo, "init", "-b", "master")
        git(repo, "commit", "--allow-empty", "-m", "init")
        reset_repair_session()

        blocked = write_repair_file("src/A.cs", "class A {}\n", str(repo))
        expect(blocked["status"] == "blocked", "未开工不能写文件")

        started = start_code_repair(4242, str(repo))
        expect(started["status"] == "success" and started["branch"] == "auto/wi-4242",
               "干净仓库开工并建分支")

        protected = write_repair_file("Vega.sln", "nope\n", str(repo))
        expect(protected["status"] == "blocked" and not (repo / "Vega.sln").exists(),
               "受保护路径不落盘")

        outside = write_repair_file("../outside.txt", "x\n", str(repo))
        expect(outside["status"] == "blocked", "拒绝逃出仓库")

        huge = "x\n" * 401
        too_big = write_repair_file("src/Huge.cs", huge, str(repo))
        expect(too_big["status"] == "blocked" and not (repo / "src" / "Huge.cs").exists(),
               "超过行数上限不落盘")

        written = write_repair_file("src/A.cs", "class A {}\n", str(repo))
        expect(written["status"] == "success" and (repo / "src" / "A.cs").is_file(),
               "允许路径写入")

        committed = commit_repair("test: 闸门内提交")
        expect(committed["status"] == "success" and committed["pushed"] is False,
               "提交成功且未 push")
        head = subprocess.run(["git", "rev-parse", "--abbrev-ref", "HEAD"], cwd=repo,
                              capture_output=True, text=True, check=True)
        expect(head.stdout.strip() == "auto/wi-4242", "提交落在 auto/wi-4242")

        (repo / "src" / "Dirty.cs").write_text("dirty\n", encoding="utf-8")
        dirty = start_code_repair(4243, str(repo))
        expect(dirty["status"] == "blocked", "脏工作区拒绝再次开工")

    push = check_push_allowed()
    pr = check_pull_request_allowed()
    expect(push["allowed"] is False and pr["allowed"] is False, "push 与 PR 仍被拒绝")

    created = execute_cmd("git branch evil-name")
    expect(created["status"] == "blocked", "execute_cmd 不能借 git branch 建分支")
    listed = execute_cmd("git branch --show-current")
    expect(listed["status"] == "success", "git branch --show-current 仍放行")

    print("repair flow ok")


if __name__ == "__main__":
    main()
