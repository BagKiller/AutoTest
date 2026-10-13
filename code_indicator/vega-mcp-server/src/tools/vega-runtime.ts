const AGENT_BASE_URL =
  process.env.VEGA_AGENT_URL || "http://localhost:18900/vega-agent";

interface ApiResponse {
  [key: string]: unknown;
  error?: string;
}

async function callAgent(
  method: "GET" | "POST",
  endpoint: string,
  body?: Record<string, unknown>
): Promise<ApiResponse> {
  const url = `${AGENT_BASE_URL}${endpoint}`;

  try {
    const options: RequestInit = {
      method,
      headers: { "Content-Type": "application/json" },
    };
    if (body) {
      options.body = JSON.stringify(body);
    }

    const response = await fetch(url, { ...options, signal: AbortSignal.timeout(10000) });
    return (await response.json()) as ApiResponse;
  } catch (err) {
    const error = err as Error;
    if (error.name === "TypeError" || error.message?.includes("fetch")) {
      return {
        error: "无法连接到 Vega Agent HTTP Service",
        hint: "请确保 Vega 程序已启动且在 Simulator 模式下运行",
        url,
        details: error.message,
      };
    }
    return { error: error.message };
  }
}

export async function pingAgent(): Promise<string> {
  const result = await callAgent("GET", "/ping");
  if (result.error) {
    return [
      "# Vega Agent 连接状态: 离线",
      "",
      `**错误**: ${result.error}`,
      result.hint ? `**提示**: ${result.hint}` : "",
      "",
      "## 排查步骤",
      "1. 确认 Vega 程序已启动（Simulator 模式）",
      "2. 确认 AgentHttpService 已在 ComponentConfig.json 中注册",
      "3. 如果端口被拒，以管理员运行:",
      "   `netsh http add urlacl url=http://+:18900/vega-agent/ user=Everyone listen=yes`",
    ].join("\n");
  }

  return [
    "# Vega Agent 连接状态: 在线 ✅",
    "",
    `**服务名**: ${result.service}`,
    `**时间戳**: ${result.timestamp}`,
    `**端口**: ${result.port}`,
  ].join("\n");
}

export async function getSystemStatus(): Promise<string> {
  const result = await callAgent("GET", "/status");
  if (result.error) {
    return `# 无法获取状态\n\n**错误**: ${result.error}\n${result.hint || ""}`;
  }

  const lines = [
    "# Vega 系统实时状态",
    "",
    `**工具类型**: ${result.toolType}`,
    `**时间戳**: ${result.timestamp}`,
    "",
  ];

  const motion = result.motion as Record<string, unknown> | undefined;
  if (motion) {
    lines.push("## Motion (运动系统)");
    if (motion.error) {
      lines.push(`- **状态**: 错误 - ${motion.error}`);
    } else {
      lines.push(`- **可用**: ${motion.available ? "是" : "否"}`);
    }
    lines.push("");
  }

  const wafer = result.wafer as Record<string, unknown> | undefined;
  if (wafer) {
    lines.push("## Wafer (晶圆状态)");
    if (wafer.error) {
      lines.push(`- **状态**: 错误 - ${wafer.error}`);
    } else {
      lines.push(`- **已装载**: ${wafer.isLoaded ? "是" : "否"}`);
      lines.push(`- **当前晶圆**: ${wafer.loadedWafer}`);
    }
    lines.push("");
  }

  const efem = result.efem as Record<string, unknown> | undefined;
  if (efem) {
    lines.push("## EFEM (设备前端模块)");
    lines.push(`- **可用**: ${efem.available ? "是" : "否"}`);
    lines.push("");
  }

  return lines.join("\n");
}

export async function getMotionStatus(): Promise<string> {
  const result = await callAgent("GET", "/status/motion");
  if (result.error) {
    return `# Motion 状态获取失败\n\n**错误**: ${result.error}`;
  }
  return `# Motion 运动系统状态\n\n${JSON.stringify(result, null, 2)}`;
}

export async function getWaferStatus(): Promise<string> {
  const result = await callAgent("GET", "/status/wafer");
  if (result.error) {
    return `# Wafer 状态获取失败\n\n**错误**: ${result.error}`;
  }

  const lines = [
    "# 晶圆状态",
    "",
    `- **晶圆在载台上**: ${result.isWaferOnStage ? "是" : "否"}`,
    `- **晶圆已装载**: ${result.isWaferLoaded ? "是" : "否"}`,
    `- **当前晶圆 ID**: ${result.loadedWaferId}`,
  ];
  return lines.join("\n");
}

export async function moveAxis(
  axis: string,
  position: number
): Promise<string> {
  const result = await callAgent("POST", "/motion/move", { axis, position });
  if (result.error) {
    return `# 运动指令失败\n\n**错误**: ${result.error}`;
  }
  return [
    "# 运动指令已发送",
    "",
    `- **轴**: ${result.axis}`,
    `- **目标位置**: ${result.position}`,
    `- **消息**: ${result.message}`,
  ].join("\n");
}

export async function loadWafer(
  loadPort: number,
  slot: number
): Promise<string> {
  const result = await callAgent("POST", "/wafer/load", { loadPort, slot });
  if (result.error) {
    return `# 装片指令失败\n\n**错误**: ${result.error}`;
  }
  return [
    "# 装片指令已发送",
    "",
    `- **LoadPort**: ${result.port}`,
    `- **Slot**: ${result.slot}`,
    `- **消息**: ${result.message}`,
  ].join("\n");
}

export async function unloadWafer(): Promise<string> {
  const result = await callAgent("POST", "/wafer/unload", {});
  if (result.error) {
    return `# 卸片指令失败\n\n**错误**: ${result.error}`;
  }
  return `# 卸片指令已发送\n\n**消息**: ${result.message}`;
}

export async function sendCommand(
  endpoint: string,
  params: Record<string, unknown>
): Promise<string> {
  const method = Object.keys(params).length > 0 ? "POST" : "GET";
  const result = await callAgent(
    method as "GET" | "POST",
    endpoint,
    method === "POST" ? params : undefined
  );
  return JSON.stringify(result, null, 2);
}
