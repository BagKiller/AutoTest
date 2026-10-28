import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { spawn } from "node:child_process";
import { randomBytes } from "node:crypto";
import { readFileSync, existsSync } from "node:fs";
import path from "node:path";
import { z } from "zod";

// dist -> vega-autotest-mcp -> code_indicator -> VegaAutomation
const AUTOMATION_ROOT = path.resolve(import.meta.dirname, "..", "..", "..");
const DEFAULT_RUNNER = path.join(
  AUTOMATION_ROOT,
  "VegaAutoTest",
  "runner",
  "Vega.AutoTest.Runner"
);
const RUNNER_DIR = process.env.VEGA_AUTOTEST_RUNNER || DEFAULT_RUNNER;
const RUN_TIMEOUT_MS = 40 * 60 * 1000;

type Job = {
  state: "running" | "done" | "error";
  workItemId: number;
  exitCode?: number;
  result?: unknown;
  stderr?: string;
  message?: string;
};

const jobs = new Map<string, Job>();

function repoRoot(): string | undefined {
  if (process.env.VEGA_SW_ROOT) return process.env.VEGA_SW_ROOT;
  const cfg = path.join(AUTOMATION_ROOT, "code_indicator", "vega-agent", "config.json");
  try {
    const parsed = JSON.parse(readFileSync(cfg, "utf8")) as { vega_workspace_root?: string };
    return parsed.vega_workspace_root || undefined;
  } catch {
    return undefined;
  }
}

function runnerArgs(args: string[]): string[] {
  const cmd = ["run", "--project", RUNNER_DIR, "--no-build", "--", ...args];
  const root = repoRoot();
  if (root && !args.includes("--repo-root")) cmd.push("--repo-root", root);
  return cmd;
}

function runnerEnv(): NodeJS.ProcessEnv {
  const env = { ...process.env };
  const root = repoRoot();
  if (root) env.VEGA_SW_ROOT = root;
  return env;
}

function runRunner(args: string[], timeoutMs: number): Promise<{ code: number; stdout: string; stderr: string }> {
  return new Promise((resolve, reject) => {
    const child = spawn("dotnet", runnerArgs(args), {
      env: runnerEnv(),
      windowsHide: true,
    });
    let stdout = "";
    let stderr = "";
    const timer = setTimeout(() => {
      child.kill();
      reject(new Error(`Runner 超时（${timeoutMs}ms）`));
    }, timeoutMs);
    child.stdout.on("data", (chunk) => {
      stdout += chunk.toString();
    });
    child.stderr.on("data", (chunk) => {
      stderr += chunk.toString();
    });
    child.on("error", (err) => {
      clearTimeout(timer);
      reject(err);
    });
    child.on("close", (code) => {
      clearTimeout(timer);
      resolve({ code: code ?? 1, stdout, stderr });
    });
  });
}

function text(payload: unknown) {
  return { content: [{ type: "text" as const, text: JSON.stringify(payload, null, 2) }] };
}

const server = new McpServer({ name: "vega-autotest", version: "1.0.0" });

server.registerTool(
  "autotest_list_cases",
  { description: "列出已登记用例。未登记时先由 AI 阅读需求/diff、补写真实测试和数据、构建发现并登记，再执行；不能以缺登记结束任务。" },
  async () => {
    if (!existsSync(RUNNER_DIR)) return text({ status: "error", message: `Runner 不存在: ${RUNNER_DIR}` });
    try {
      const ran = await runRunner(["list-cases"], 120_000);
      return text({ status: ran.code === 0 ? "success" : "error", output: ran.stdout.trim(), stderr: ran.stderr.slice(-1000) });
    } catch (err) {
      return text({ status: "error", message: String(err) });
    }
  }
);

server.registerTool(
  "autotest_doctor",
  { description: "VegaAutoTest 环境自检：Vega.exe、18901、构建脚本、git、dotnet。" },
  async () => {
    try {
      const ran = await runRunner(["doctor"], 120_000);
      return text({ status: ran.code === 0 ? "success" : "error", output: ran.stdout.trim() });
    } catch (err) {
      return text({ status: "error", message: String(err) });
    }
  }
);

server.registerTool(
  "autotest_start_verification",
  {
    description:
      "异步启动 Runner 验证。判定只来自 result.json，禁止凭代码阅读宣布已修复。uia 层会拉起真实 Vega 界面。",
    inputSchema: {
      work_item_id: z.number().describe("ADO 工作项号"),
      rebuild: z.boolean().optional().describe("是否先重编被测程序，默认 false"),
      layers: z.string().optional().describe("logic,uia 或其中一层，默认 logic,uia"),
    },
  },
  async ({ work_item_id, rebuild, layers }) => {
    if (!existsSync(RUNNER_DIR)) return text({ status: "error", message: `Runner 不存在: ${RUNNER_DIR}` });
    const layerArg = layers ?? "logic,uia";
    const args = ["verify", "--work-item", String(work_item_id), "--layers", layerArg];
    if (rebuild) args.push("--rebuild");
    const jobId = randomBytes(4).toString("hex");
    jobs.set(jobId, { state: "running", workItemId: work_item_id });
    void runRunner(args, RUN_TIMEOUT_MS)
      .then((ran) => {
        const lines = ran.stdout.trim().split(/\r?\n/).filter(Boolean);
        const resultPath = lines.at(-1) ?? "";
        let result: unknown = null;
        if (resultPath && existsSync(resultPath)) {
          result = JSON.parse(readFileSync(resultPath, "utf8"));
        }
        jobs.set(jobId, {
          state: "done",
          workItemId: work_item_id,
          exitCode: ran.code,
          result,
          stderr: ran.stderr.slice(-2000),
        });
      })
      .catch((err: unknown) => {
        jobs.set(jobId, { state: "error", workItemId: work_item_id, message: String(err) });
      });
    return text({
      status: "success",
      job_id: jobId,
      message: `验证已启动（工作项 #${work_item_id}）。用 autotest_get_verification_status 轮询。`,
    });
  }
);

server.registerTool(
  "autotest_get_verification_status",
  {
    description: "轮询 autotest_start_verification 的 job。done 时只回 verdict 摘要，必须原样转述。",
    inputSchema: { job_id: z.string().describe("start 返回的 job_id") },
  },
  async ({ job_id }) => {
    const job = jobs.get(job_id);
    if (!job) return text({ status: "error", message: `未知 job: ${job_id}` });
    if (job.state === "running") {
      return text({ status: "success", state: "running", message: `工作项 #${job.workItemId} 仍在验证` });
    }
    if (job.state === "error") return text({ status: "error", message: job.message });
    const result = job.result as {
      runId?: string;
      verdict?: string;
      verdictCode?: string;
      verdictReason?: string;
      summary?: unknown;
      target?: unknown;
      channels?: unknown;
      durationMs?: number;
      assertions?: { result?: string }[];
      evidence?: { dir?: string };
    } | null;
    if (!result) {
      return text({ status: "error", message: `退出码 ${job.exitCode} 但没有 result.json`, stderr: job.stderr });
    }
    return text({
      status: "success",
      state: "done",
      runId: result.runId,
      verdict: result.verdict,
      verdictCode: result.verdictCode,
      verdictReason: result.verdictReason,
      summary: result.summary,
      target: result.target,
      channels: result.channels,
      durationMs: result.durationMs,
      failedAssertions: (result.assertions ?? []).filter((a) => a.result === "FAIL"),
      evidenceDir: result.evidence?.dir,
      exitCode: job.exitCode,
    });
  }
);

const transport = new StdioServerTransport();
await server.connect(transport);
