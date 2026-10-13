import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import * as path from "path";

import {
  listComponents,
  findComponent,
  getActiveComponents,
} from "./tools/component-explorer.js";
import {
  traceClass,
  traceDependencyChain,
} from "./tools/architecture-tracer.js";
import {
  findInterfaceTriple,
  listAllDevices,
} from "./tools/interface-finder.js";
import {
  analyzeSolution,
  getProjectDependencyTree,
} from "./tools/project-structure.js";
import { searchCode, searchXamlBindings } from "./tools/code-search.js";
import {
  pingAgent,
  getSystemStatus,
  getWaferStatus,
  moveAxis,
  loadWafer,
  unloadWafer,
  sendCommand,
} from "./tools/vega-runtime.js";

const WORKSPACE_ROOT =
  process.env.VEGA_WORKSPACE_ROOT || path.resolve(process.cwd(), "..", "..");

const server = new McpServer({
  name: "vega-codebase-explorer",
  version: "1.0.0",
});

const registerTool = (
  name: string,
  description: string,
  inputSchema: Record<string, z.ZodTypeAny>,
  cb: (...args: any[]) => any
) =>
  server.registerTool(
    name,
    Object.keys(inputSchema).length === 0
      ? { description }
      : { description, inputSchema },
    cb
  );

// ===== Tool 1: 组件浏览器 =====
registerTool(
  "vega_list_components",
  `列出 Vega 系统中在 ComponentConfig.json 中注册的所有组件。
可按层级(CD/PD/LD/BL)或条件(Simulator/Tool/Vega300)过滤。
帮助理解系统加载了哪些模块以及它们之间的关系。`,
  {
    layer: z
      .string()
      .optional()
      .describe(
        "按层级过滤: Communication/Physical/Logic/Business/Platform/Facade"
      ),
    condition: z
      .string()
      .optional()
      .describe(
        "按条件过滤: Simulator, Tool, Vega200, Vega300, Vega500, Gen1, Gen2, Gen3"
      ),
  },
  async ({ layer, condition }) => {
    const result = await listComponents(WORKSPACE_ROOT, layer, condition);
    return { content: [{ type: "text" as const, text: result }] };
  }
);

// ===== Tool 2: 组件搜索 =====
registerTool(
  "vega_find_component",
  `在 ComponentConfig.json 中搜索特定组件。
输入组件名、接口名或实现类名的部分关键词即可。
返回组件的完整注册信息，包括所有实现变体（真实/仿真）和条件。`,
  {
    search_term: z
      .string()
      .describe("搜索关键词，如 'EFEM', 'Motion', 'Laser', 'Camera'"),
  },
  async ({ search_term }) => {
    const result = await findComponent(WORKSPACE_ROOT, search_term);
    return { content: [{ type: "text" as const, text: result }] };
  }
);

// ===== Tool 3: 激活组件列表 =====
registerTool(
  "vega_active_components",
  `显示特定工具类型下实际激活的组件列表。
根据 ToolConfig 中的 RootCondition 和 Features 解析条件匹配，
显示在该配置下实际会被加载的组件及其启动顺序。`,
  {
    tool_type: z
      .string()
      .describe("工具类型: Vega200, Vega300, 或 Vega500"),
  },
  async ({ tool_type }) => {
    const result = await getActiveComponents(WORKSPACE_ROOT, tool_type);
    return { content: [{ type: "text" as const, text: result }] };
  }
);

// ===== Tool 4: 类追踪 =====
registerTool(
  "vega_trace_class",
  `深度追踪一个 C# 类或接口。
分析它所在的层级、依赖注入的服务、继承关系、公开方法等。
帮助理解一个类在 Vega 架构中的角色和位置。`,
  {
    class_name: z
      .string()
      .describe("要追踪的类名或接口名，如 'WaferHandlerService', 'IEFEM', 'MotionDevice'"),
  },
  async ({ class_name }) => {
    const result = await traceClass(WORKSPACE_ROOT, class_name);
    return { content: [{ type: "text" as const, text: result }] };
  }
);

// ===== Tool 5: 依赖链 =====
registerTool(
  "vega_dependency_chain",
  `追踪一个类的项目级依赖链。
向下：查看该类所在项目依赖了哪些项目。
向上：查看哪些项目依赖了该类所在的项目。
帮助理解模块间的耦合关系。`,
  {
    class_name: z.string().describe("起始类名"),
    direction: z
      .enum(["up", "down"])
      .default("down")
      .describe("追踪方向: down=向下依赖, up=被谁依赖"),
  },
  async ({ class_name, direction }) => {
    const result = await traceDependencyChain(
      WORKSPACE_ROOT,
      class_name,
      direction
    );
    return { content: [{ type: "text" as const, text: result }] };
  }
);

// ===== Tool 6: 接口三件套 =====
registerTool(
  "vega_find_device",
  `查找设备/服务的"接口-实现-仿真"三件套。
输入设备关键词（如 Camera, EFEM, Motion），自动查找：
- 接口项目 (Vega.PD.IXxx / Vega.LD.IXxx)
- 实现项目 (Vega.PD.Xxx)
- 仿真项目 (Vega.PD.XxxSim)
同时显示接口定义的方法列表。`,
  {
    device_name: z
      .string()
      .describe("设备关键词: Camera, EFEM, Motion, Laser, DAQ, Optics, FFU 等"),
  },
  async ({ device_name }) => {
    const result = await findInterfaceTriple(WORKSPACE_ROOT, device_name);
    return { content: [{ type: "text" as const, text: result }] };
  }
);

// ===== Tool 7: 设备总览 =====
registerTool(
  "vega_list_devices",
  `列出 Vega 项目中所有设备和服务的概览。
按层级分组显示，标注每个设备是否有完整的接口/实现/仿真三件套。
帮助快速了解系统的硬件抽象全貌。`,
  {
    layer: z
      .string()
      .optional()
      .describe("可选层级过滤: PD, LD, BL, CD"),
  },
  async ({ layer }) => {
    const result = await listAllDevices(WORKSPACE_ROOT, layer);
    return { content: [{ type: "text" as const, text: result }] };
  }
);

// ===== Tool 8: 解决方案分析 =====
registerTool(
  "vega_solution_structure",
  `分析 Vega.sln 解决方案的完整结构。
显示所有项目按层级分组，每个项目的依赖关系、输出类型等。
适合初次了解项目全貌时使用。`,
  {},
  async () => {
    const result = await analyzeSolution(WORKSPACE_ROOT);
    return { content: [{ type: "text" as const, text: result }] };
  }
);

// ===== Tool 9: 项目依赖树 =====
registerTool(
  "vega_project_deps",
  `查看特定项目的完整依赖树。
包括项目引用、NuGet 包引用，以及反向查找谁引用了这个项目。
帮助理解一个模块在系统中的耦合情况。`,
  {
    project_name: z
      .string()
      .describe("项目名称，如 'Vega.BL.WaferRun', 'Vega.LD.EFEM'"),
  },
  async ({ project_name }) => {
    const result = await getProjectDependencyTree(
      WORKSPACE_ROOT,
      project_name
    );
    return { content: [{ type: "text" as const, text: result }] };
  }
);

// ===== Tool 10: 代码搜索 =====
registerTool(
  "vega_search_code",
  `在 Vega 代码库中搜索代码。
支持正则表达式，可按文件类型和层级过滤。
比通用搜索更智能：自动排除 bin/obj，按项目分组显示结果。`,
  {
    pattern: z.string().describe("搜索模式（支持正则），如 'SetOpticalMode', 'IWaferRunService'"),
    file_type: z
      .string()
      .optional()
      .describe("文件类型过滤: cs, xaml, json"),
    layer: z
      .string()
      .optional()
      .describe("层级过滤: UI, BL, LD, PD, CD"),
    max_results: z.number().optional().describe("最大结果数，默认 30"),
  },
  async ({ pattern, file_type, layer, max_results }) => {
    const result = await searchCode(WORKSPACE_ROOT, pattern, {
      fileType: file_type,
      layer,
      maxResults: max_results,
    });
    return { content: [{ type: "text" as const, text: result }] };
  }
);

// ===== Tool 11: XAML 绑定追踪 =====
registerTool(
  "vega_trace_binding",
  `追踪 WPF XAML 绑定。
给定一个 Command 或 Property 名称，同时搜索 XAML 和 C# 代码，
帮助理解 MVVM 中 View 和 ViewModel 之间的绑定关系。`,
  {
    binding_name: z
      .string()
      .describe("绑定名称，如 'StartInspectionCommand', 'IsRunning'"),
  },
  async ({ binding_name }) => {
    const result = await searchXamlBindings(WORKSPACE_ROOT, binding_name);
    return { content: [{ type: "text" as const, text: result }] };
  }
);

// ===== Tool 12: 架构概览 =====
registerTool(
  "vega_architecture_overview",
  `获取 Vega 项目的架构概览。
提供系统的高层设计说明，包括分层架构、设计模式、关键概念等。
适合初次接触 Vega 项目的开发者。`,
  {},
  async () => {
    const overview = `# Vega 上位机系统架构概览

## 系统定位
Vega 是半导体晶圆检测设备的上位机软件平台，支持 SP1(Vega200)、SP3(Vega300)、SP5(Vega500) 三种机型。
控制对象包括：运动系统、光学系统、相机、激光器、EFEM（设备前端模块）、数据采集硬件。

## 分层架构 (从上到下)

\`\`\`
┌─────────────────────────────────────────────┐
│  UI 层 (Vega.UI.*)                          │
│  WPF + MVVM (CommunityToolkit.Mvvm)         │
│  模块: Jobs, Calibration, Inspection,       │
│        Diagnostics, Instrument, Workspace   │
├─────────────────────────────────────────────┤
│  BL 业务逻辑层 (Vega.BL.*)                  │
│  服务编排: WaferHandler, WaferRun,          │
│  WaferAlignment, Analysis, DataReporting    │
├─────────────────────────────────────────────┤
│  LD 逻辑设备层 (Vega.LD.*)                   │
│  硬件抽象: EFEM, Motion, Optics,            │
│  DataCollector, WaferReceiver, BSS          │
├─────────────────────────────────────────────┤
│  PD 物理设备层 (Vega.PD.*)                   │
│  硬件实现: EFEMDevice, MotionDevice,        │
│  Camera, Laser, DAQ, Motors, Cylinder       │
│  每个设备都有 Sim 仿真版本                    │
├─────────────────────────────────────────────┤
│  CD 通信通道层 (Vega.CD.*)                   │
│  通信协议: TCP, COM串口, 第三方SDK           │
└─────────────────────────────────────────────┘
\`\`\`

## 核心设计原则
1. **依赖倒置**: 上层只依赖下层接口(I*)，不依赖具体实现
2. **仿真优先**: 每个PD设备都有Sim版本，无硬件也能开发
3. **条件加载**: ComponentConfig.json 根据机型/模式动态加载组件
4. **接口隔离**: 每层的接口和实现分为独立项目

## 关键配置
- \`ComponentConfig.json\`: 组件注册表，定义加载哪些模块
- \`MachineConfiguration/\`: 各设备的参数配置 (JSON)
- \`version.props\`: Galaxy平台依赖配置

## 依赖注入
- 使用 Autofac 容器
- 构造函数注入 + \`[AutoResolveDependency]\` 属性注入
- \`XActivator.Activator.CreateInstance<T>()\` 动态创建

## 事件模式
- Before/After 事件对: \`BeforePutWaferToStage\` / \`AfterPutWaferToStage\`
- 同步/异步方法对: \`LoadWafer()\` / \`LoadWaferAsync()\`

## 使用本 MCP 的建议
1. 先用 \`vega_architecture_overview\` 了解全貌（就是这个）
2. 用 \`vega_solution_structure\` 看解决方案中有哪些项目
3. 用 \`vega_list_components\` 查看注册的组件
4. 用 \`vega_find_device\` 查看特定设备的三件套
5. 用 \`vega_trace_class\` 深入分析某个类
6. 用 \`vega_trace_binding\` 追踪 UI 绑定链路
`;
    return { content: [{ type: "text" as const, text: overview }] };
  }
);

// ===== Runtime Tools: 实时操控运行中的 Vega 上位机 =====

server.tool(
  "vega_ping",
  `检查 Vega Agent HTTP Service 是否在线。
用于验证与运行中的 Vega 上位机程序的连接状态。
Vega 必须在 Simulator 模式下运行才会启动 Agent 服务。`,
  {},
  async () => {
    const result = await pingAgent();
    return { content: [{ type: "text" as const, text: result }] };
  }
);

server.tool(
  "vega_system_status",
  `获取运行中的 Vega 系统的实时状态。
包括工具类型、运动系统状态、晶圆状态、EFEM 状态等。
需要 Vega 程序在 Simulator 模式下运行。`,
  {},
  async () => {
    const result = await getSystemStatus();
    return { content: [{ type: "text" as const, text: result }] };
  }
);

server.tool(
  "vega_wafer_status",
  `获取晶圆的实时状态。
查看晶圆是否在载台上、是否已装载、当前晶圆 ID 等。`,
  {},
  async () => {
    const result = await getWaferStatus();
    return { content: [{ type: "text" as const, text: result }] };
  }
);

server.tool(
  "vega_move_axis",
  `发送运动轴移动指令到 Vega 上位机。
控制指定轴移动到目标位置。
⚠️ 这是一个操作指令，会实际控制设备运动。`,
  {
    axis: z.string().describe("运动轴名称，如 'X', 'Z', 'Theta'"),
    position: z.number().describe("目标位置 (单位取决于轴类型)"),
  },
  async ({ axis, position }) => {
    const result = await moveAxis(axis, position);
    return { content: [{ type: "text" as const, text: result }] };
  }
);

server.tool(
  "vega_load_wafer",
  `发送装片指令到 Vega 上位机。
从指定 LoadPort 的指定 Slot 取出晶圆并装载到载台。
⚠️ 这是一个操作指令，会实际控制 EFEM 和运动系统。`,
  {
    load_port: z.number().default(1).describe("LoadPort 编号 (默认 1)"),
    slot: z.number().default(1).describe("Slot 编号 (默认 1)"),
  },
  async ({ load_port, slot }) => {
    const result = await loadWafer(load_port, slot);
    return { content: [{ type: "text" as const, text: result }] };
  }
);

server.tool(
  "vega_unload_wafer",
  `发送卸片指令到 Vega 上位机。
将载台上的晶圆卸载回原始 Slot。
⚠️ 这是一个操作指令，会实际控制 EFEM 和运动系统。`,
  {},
  async () => {
    const result = await unloadWafer();
    return { content: [{ type: "text" as const, text: result }] };
  }
);

server.tool(
  "vega_raw_command",
  `向 Vega Agent 发送自定义 HTTP 请求。
这是一个低级工具，用于调用尚未封装的 Agent API 端点。
传入端点路径和参数即可。`,
  {
    endpoint: z
      .string()
      .describe("API 端点路径，如 '/status/optics', '/motion/move'"),
    params: z
      .string()
      .default("{}")
      .describe("JSON 格式的参数，如 '{\"axis\":\"X\",\"position\":100}'"),
  },
  async ({ endpoint, params }) => {
    let parsedParams: Record<string, unknown> = {};
    try {
      parsedParams = JSON.parse(params);
    } catch {
      return {
        content: [{ type: "text" as const, text: `JSON 解析错误: ${params}` }],
      };
    }
    const result = await sendCommand(endpoint, parsedParams);
    return { content: [{ type: "text" as const, text: result }] };
  }
);

async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error(
    `Vega Codebase Explorer MCP Server started. Workspace: ${WORKSPACE_ROOT}`
  );
}

main().catch((err) => {
  console.error("Failed to start MCP server:", err);
  process.exit(1);
});
