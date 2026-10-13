import * as fs from "fs/promises";
import * as path from "path";

interface ComponentEntry {
  Name: string;
  Interface: string;
  Implementation: string;
  RunOrder: number;
  Condition?: string;
  NameRegister?: boolean;
  EnableInterceptor?: boolean;
  ConfigSelection?: string;
  ExtraMappings?: Array<{ Name: string; Interface: string }>;
}

interface ToolConfig {
  ToolType: string;
  RootCondition: string;
  Features: string;
}

interface ComponentConfig {
  ToolConfigCollection: {
    SelectedToolType: string;
    ToolConfigs: ToolConfig[];
  };
  Components: ComponentEntry[];
}

export interface LayerInfo {
  name: string;
  range: string;
  prefix: string;
}

const LAYERS: LayerInfo[] = [
  { name: "Communication Channel (通信通道层)", range: "800-999", prefix: "Vega.CD." },
  { name: "Physical Device (物理设备层)", range: "1000-1999", prefix: "Vega.PD." },
  { name: "Logic Device (逻辑设备层)", range: "2000-2999", prefix: "Vega.LD." },
  { name: "Business Layer (业务逻辑层)", range: "3000-9999", prefix: "Vega.BL." },
  { name: "Platform/Base Services (平台/基础服务)", range: "1-799", prefix: "Vega.Platform.|Vega.Utility.|Galaxy." },
  { name: "Facade (外观层)", range: "3000-9999", prefix: "Vega.Facade." },
];

function detectLayer(component: ComponentEntry): string {
  const implStr = component.Implementation;
  for (const layer of LAYERS) {
    const prefixes = layer.prefix.split("|");
    if (prefixes.some((p) => implStr.startsWith(p))) {
      return layer.name;
    }
  }
  return "Unknown";
}

function parseCondition(condition: string): string {
  if (!condition) return "所有配置";
  return condition
    .split(",")
    .map((c) => c.trim())
    .join(" + ");
}

export async function loadComponentConfig(
  workspaceRoot: string
): Promise<ComponentConfig> {
  const configPath = path.join(
    workspaceRoot,
    "Vega",
    "Resources",
    "ComponentConfig.json"
  );
  const content = await fs.readFile(configPath, "utf-8");
  const cleaned = content.replace(
    /\/\/.*$/gm,
    ""
  );
  return JSON.parse(cleaned);
}

export async function listComponents(
  workspaceRoot: string,
  filterLayer?: string,
  filterCondition?: string
): Promise<string> {
  const config = await loadComponentConfig(workspaceRoot);

  const toolConfig = config.ToolConfigCollection;
  let lines: string[] = [
    `# Vega 组件注册表`,
    ``,
    `**当前选中工具类型**: ${toolConfig.SelectedToolType}`,
    `**可用工具配置**:`,
  ];

  for (const tc of toolConfig.ToolConfigs) {
    lines.push(
      `  - ${tc.ToolType}: RootCondition=[${tc.RootCondition}], Features=[${tc.Features}]`
    );
  }
  lines.push("");

  const grouped = new Map<string, ComponentEntry[]>();
  for (const comp of config.Components) {
    const layer = detectLayer(comp);
    if (filterLayer && !layer.toLowerCase().includes(filterLayer.toLowerCase()))
      continue;
    if (
      filterCondition &&
      comp.Condition &&
      !comp.Condition.toLowerCase().includes(filterCondition.toLowerCase())
    )
      continue;

    if (!grouped.has(layer)) grouped.set(layer, []);
    grouped.get(layer)!.push(comp);
  }

  const layerOrder = LAYERS.map((l) => l.name);
  const sortedLayers = [...grouped.keys()].sort(
    (a, b) => layerOrder.indexOf(a) - layerOrder.indexOf(b)
  );

  for (const layer of sortedLayers) {
    const comps = grouped.get(layer)!;
    lines.push(`## ${layer} (${comps.length} 个组件)`);
    lines.push("");

    const uniqueNames = [...new Set(comps.map((c) => c.Name))];
    for (const name of uniqueNames) {
      const variants = comps.filter((c) => c.Name === name);
      const first = variants[0];
      const interfaceName = first.Interface.split(",")[0].trim();

      lines.push(`### ${name}`);
      lines.push(`- **接口**: \`${interfaceName}\``);
      lines.push(`- **RunOrder**: ${first.RunOrder}`);

      if (variants.length === 1) {
        const implName = first.Implementation.split(",")[0].trim();
        lines.push(`- **实现**: \`${implName}\``);
        lines.push(`- **条件**: ${parseCondition(first.Condition || "")}`);
      } else {
        lines.push(`- **实现变体** (${variants.length} 个):`);
        for (const v of variants) {
          const implName = v.Implementation.split(",")[0].trim();
          lines.push(
            `  - \`${implName}\` → 条件: ${parseCondition(v.Condition || "")}`
          );
        }
      }

      if (first.NameRegister) lines.push(`- **命名注册**: 是 (按Name解析)`);
      if (first.EnableInterceptor) lines.push(`- **拦截器**: 已启用`);
      if (first.ConfigSelection)
        lines.push(`- **配置选择**: ${first.ConfigSelection}`);
      lines.push("");
    }
  }

  return lines.join("\n");
}

export async function findComponent(
  workspaceRoot: string,
  searchTerm: string
): Promise<string> {
  const config = await loadComponentConfig(workspaceRoot);
  const term = searchTerm.toLowerCase();

  const matches = config.Components.filter(
    (c) =>
      c.Name.toLowerCase().includes(term) ||
      c.Interface.toLowerCase().includes(term) ||
      c.Implementation.toLowerCase().includes(term)
  );

  if (matches.length === 0) {
    return `未找到匹配 "${searchTerm}" 的组件。`;
  }

  const lines: string[] = [`# 搜索结果: "${searchTerm}" (${matches.length} 个匹配)`];

  const byName = new Map<string, ComponentEntry[]>();
  for (const m of matches) {
    if (!byName.has(m.Name)) byName.set(m.Name, []);
    byName.get(m.Name)!.push(m);
  }

  for (const [name, variants] of byName) {
    lines.push("");
    lines.push(`## ${name}`);
    lines.push(`- **层级**: ${detectLayer(variants[0])}`);
    lines.push(`- **接口**: \`${variants[0].Interface}\``);
    lines.push(`- **RunOrder**: ${variants[0].RunOrder}`);

    lines.push(`- **所有实现**:`);
    for (const v of variants) {
      const implName = v.Implementation.split(",")[0].trim();
      const isSim =
        implName.toLowerCase().includes("sim") &&
        !implName.toLowerCase().includes("simple");
      const tag = isSim ? " 🔷仿真" : " 🔶真实";
      lines.push(
        `  - \`${implName}\`${tag} → 条件: ${parseCondition(v.Condition || "")}`
      );
    }

    const interfaceParts = variants[0].Interface.split(",");
    const interfaceType = interfaceParts[0].trim();
    const interfaceAssembly = interfaceParts[1]?.trim() || "";
    lines.push("");
    lines.push(`### 相关项目`);
    lines.push(`- **接口项目**: ${interfaceAssembly}`);

    const implAssemblies = [
      ...new Set(variants.map((v) => v.Implementation.split(",")[1]?.trim())),
    ];
    for (const asm of implAssemblies) {
      if (asm) lines.push(`- **实现项目**: ${asm}`);
    }

    lines.push("");
    lines.push(`### 建议查看的文件`);
    lines.push(
      `- 接口定义: \`${interfaceAssembly}/${interfaceType.split(".").pop()}.cs\``
    );
    for (const v of variants) {
      const implType = v.Implementation.split(",")[0].trim();
      const implAsm = v.Implementation.split(",")[1]?.trim();
      lines.push(`- 实现: \`${implAsm}/${implType.split(".").pop()}.cs\``);
    }
  }

  return lines.join("\n");
}

export async function getActiveComponents(
  workspaceRoot: string,
  toolType: string
): Promise<string> {
  const config = await loadComponentConfig(workspaceRoot);

  const toolConfig = config.ToolConfigCollection.ToolConfigs.find(
    (tc) => tc.ToolType.toLowerCase() === toolType.toLowerCase()
  );

  if (!toolConfig) {
    const available = config.ToolConfigCollection.ToolConfigs.map(
      (tc) => tc.ToolType
    ).join(", ");
    return `未找到工具类型 "${toolType}"。可用类型: ${available}`;
  }

  const rootConditions = toolConfig.RootCondition.split(",").map((c) =>
    c.trim().toLowerCase()
  );
  const features = toolConfig.Features.split(",").map((f) =>
    f.trim().toLowerCase()
  );

  const lines: string[] = [
    `# ${toolConfig.ToolType} 激活组件列表`,
    ``,
    `**RootCondition**: ${toolConfig.RootCondition}`,
    `**Features**: ${toolConfig.Features}`,
    ``,
  ];

  function matchesCondition(condition: string): boolean {
    if (!condition) return true;

    const parts = condition.split(",").map((c) => c.trim().toLowerCase());
    return parts.every((part) => {
      if (part.includes("|")) {
        const alternatives = part.split("|").map((a) => a.trim());
        return alternatives.some(
          (alt) => rootConditions.includes(alt) || features.includes(alt)
        );
      }
      return rootConditions.includes(part) || features.includes(part);
    });
  }

  const active: ComponentEntry[] = [];
  const namesSeen = new Set<string>();

  for (const comp of config.Components) {
    if (matchesCondition(comp.Condition || "")) {
      if (!comp.NameRegister && namesSeen.has(comp.Name)) continue;
      namesSeen.add(comp.Name);
      active.push(comp);
    }
  }

  active.sort((a, b) => a.RunOrder - b.RunOrder);

  lines.push(`共 ${active.length} 个激活组件，按启动顺序排列：`);
  lines.push("");

  let currentRunOrder = -1;
  for (const comp of active) {
    if (comp.RunOrder !== currentRunOrder) {
      currentRunOrder = comp.RunOrder;
      lines.push(`### RunOrder ${currentRunOrder}`);
    }
    const implName = comp.Implementation.split(",")[0].trim();
    const layer = detectLayer(comp);
    lines.push(
      `- **${comp.Name}** → \`${implName}\` [${layer}]`
    );
  }

  return lines.join("\n");
}
