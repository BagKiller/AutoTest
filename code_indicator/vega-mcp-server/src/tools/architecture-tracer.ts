import * as fs from "fs/promises";
import * as path from "path";

interface TraceResult {
  className: string;
  filePath: string;
  layer: string;
  dependencies: string[];
  dependents: string[];
  interfaces: string[];
}

const LAYER_ORDER: Record<string, number> = {
  UI: 0,
  BL: 1,
  LD: 2,
  PD: 3,
  CD: 4,
  Data: 5,
  Platform: 6,
  Facade: 7,
  Utility: 8,
  Algo: 9,
  ThirdParty: 10,
};

function classifyProject(projectName: string): {
  layer: string;
  isInterface: boolean;
} {
  const parts = projectName.split(".");
  if (parts.length < 2 || parts[0] !== "Vega")
    return { layer: "Unknown", isInterface: false };

  const layerPart = parts[1];

  if (layerPart === "UI") return { layer: "UI", isInterface: false };
  if (layerPart === "BL") {
    return { layer: "BL", isInterface: parts[2]?.startsWith("I") ?? false };
  }
  if (layerPart === "LD") {
    return { layer: "LD", isInterface: parts[2]?.startsWith("I") ?? false };
  }
  if (layerPart === "PD") {
    return { layer: "PD", isInterface: parts[2]?.startsWith("I") ?? false };
  }
  if (layerPart === "CD") {
    return { layer: "CD", isInterface: parts[2]?.startsWith("I") ?? false };
  }
  if (layerPart === "Data") return { layer: "Data", isInterface: false };
  if (layerPart === "Platform")
    return {
      layer: "Platform",
      isInterface: parts[2]?.startsWith("I") ?? false,
    };
  if (layerPart === "Facade")
    return {
      layer: "Facade",
      isInterface: parts[2]?.startsWith("I") ?? false,
    };
  if (layerPart === "Utility") return { layer: "Utility", isInterface: false };
  if (layerPart === "Algo") return { layer: "Algo", isInterface: false };
  if (layerPart === "ThirdParty")
    return { layer: "ThirdParty", isInterface: false };

  return { layer: "Unknown", isInterface: false };
}

async function findCsFiles(dir: string): Promise<string[]> {
  const results: string[] = [];
  try {
    const entries = await fs.readdir(dir, { withFileTypes: true });
    for (const entry of entries) {
      const fullPath = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        if (
          ["bin", "obj", "node_modules", ".git", ".vs"].includes(entry.name)
        )
          continue;
        results.push(...(await findCsFiles(fullPath)));
      } else if (entry.name.endsWith(".cs")) {
        results.push(fullPath);
      }
    }
  } catch {
    // skip inaccessible directories
  }
  return results;
}

async function findProjectDirs(workspaceRoot: string): Promise<string[]> {
  const entries = await fs.readdir(workspaceRoot, { withFileTypes: true });
  return entries
    .filter(
      (e) =>
        e.isDirectory() &&
        e.name.startsWith("Vega.") &&
        !["bin", "obj"].includes(e.name)
    )
    .map((e) => path.join(workspaceRoot, e.name));
}

export async function traceClass(
  workspaceRoot: string,
  className: string
): Promise<string> {
  const lines: string[] = [`# 类追踪: ${className}`, ""];

  const projectDirs = await findProjectDirs(workspaceRoot);

  let foundFile: string | null = null;
  let foundProject: string | null = null;
  let fileContent: string | null = null;

  for (const projectDir of projectDirs) {
    const csFiles = await findCsFiles(projectDir);
    for (const f of csFiles) {
      try {
        const content = await fs.readFile(f, "utf-8");
        const classPattern = new RegExp(
          `\\b(class|interface|enum|struct)\\s+${escapeRegex(className)}\\b`
        );
        if (classPattern.test(content)) {
          foundFile = f;
          foundProject = path.basename(projectDir);
          fileContent = content;
          break;
        }
      } catch {
        continue;
      }
    }
    if (foundFile) break;
  }

  if (!foundFile || !foundProject || !fileContent) {
    return `未在项目中找到类 "${className}"。\n\n**提示**: 该类可能位于 GalaxyPlatform 外部依赖中，或者类名有误。\n尝试搜索相近的名称。`;
  }

  const { layer, isInterface } = classifyProject(foundProject);

  lines.push(`## 定位信息`);
  lines.push(`- **所在项目**: ${foundProject}`);
  lines.push(`- **所在层级**: ${layer}${isInterface ? " (接口)" : " (实现)"}`);
  lines.push(`- **文件路径**: ${path.relative(workspaceRoot, foundFile)}`);
  lines.push("");

  const namespaceMatch = fileContent.match(/namespace\s+([\w.]+)/);
  if (namespaceMatch) {
    lines.push(`- **命名空间**: ${namespaceMatch[1]}`);
  }

  const inheritanceMatch = fileContent.match(
    new RegExp(
      `(class|interface)\\s+${escapeRegex(className)}\\s*(?:<[^>]*>)?\\s*:\\s*([^{]+)`
    )
  );
  if (inheritanceMatch) {
    const bases = inheritanceMatch[2]
      .split(",")
      .map((b) => b.trim())
      .filter((b) => b.length > 0);
    lines.push(`- **继承/实现**: ${bases.map((b) => `\`${b}\``).join(", ")}`);
  }

  lines.push("");
  lines.push(`## 依赖注入分析`);

  const ctorMatch = fileContent.match(
    new RegExp(
      `(public|internal|protected)\\s+${escapeRegex(className)}\\s*\\(([^)]*?)\\)`,
      "s"
    )
  );
  if (ctorMatch) {
    const params = ctorMatch[2]
      .split(",")
      .map((p) => p.trim())
      .filter((p) => p.length > 0);
    if (params.length > 0) {
      lines.push(`**构造函数注入的依赖** (${params.length} 个):`);
      for (const param of params) {
        const parts = param.split(/\s+/);
        if (parts.length >= 2) {
          const typeName = parts.slice(0, -1).join(" ");
          const paramName = parts[parts.length - 1];
          lines.push(`- \`${typeName}\` ${paramName}`);
        }
      }
    } else {
      lines.push("无构造函数参数注入");
    }
  }

  const autoResolveMatches = fileContent.matchAll(
    /\[AutoResolveDependency[^\]]*\]\s*\n\s*public\s+([\w<>,\s?]+)\s+(\w+)/g
  );
  const autoResolved = [...autoResolveMatches];
  if (autoResolved.length > 0) {
    lines.push("");
    lines.push(`**属性注入 [AutoResolveDependency]** (${autoResolved.length} 个):`);
    for (const m of autoResolved) {
      lines.push(`- \`${m[1].trim()}\` ${m[2]}`);
    }
  }

  lines.push("");
  lines.push(`## 层级调用链`);
  lines.push("");
  lines.push(
    "根据 Vega 架构规范，依赖流向: UI → BL → LD → PD → CD"
  );
  lines.push("");

  const layerNum = LAYER_ORDER[layer] ?? -1;
  if (layerNum === 0) {
    lines.push(
      "**当前位于 UI 层** — 这是最顶层，通过 ViewModel 调用 BL 服务。"
    );
    lines.push(
      "→ 向下追踪: 查看构造函数中注入的 I*Service 接口"
    );
  } else if (layerNum === 1) {
    lines.push(
      "**当前位于 BL 层** — 业务逻辑编排层。"
    );
    lines.push("→ 向上: 被 UI 层 ViewModel 调用");
    lines.push("→ 向下: 调用 LD 层逻辑设备");
  } else if (layerNum === 2) {
    lines.push(
      "**当前位于 LD 层** — 逻辑设备抽象层。"
    );
    lines.push("→ 向上: 被 BL 层 Service 调用");
    lines.push("→ 向下: 调用 PD 层物理设备");
  } else if (layerNum === 3) {
    lines.push(
      "**当前位于 PD 层** — 物理设备实现层。"
    );
    lines.push("→ 向上: 被 LD 层逻辑设备调用");
    lines.push("→ 向下: 通过 CD 层通信通道与硬件交互");
  } else if (layerNum === 4) {
    lines.push(
      "**当前位于 CD 层** — 通信通道层（最底层）。"
    );
    lines.push("→ 向上: 被 PD 层物理设备调用");
    lines.push("→ 直接与硬件通信 (串口/TCP/SDK)");
  }

  const publicMethods = [
    ...fileContent.matchAll(
      /public\s+(?:async\s+)?(?:virtual\s+)?(?:override\s+)?(?:Task(?:<[\w<>,?\s]+>)?|void|[\w<>,?\s]+)\s+(\w+)\s*\(/g
    ),
  ];
  const methodNames = publicMethods
    .map((m) => m[1])
    .filter(
      (n) =>
        n !== className &&
        !["Dispose", "ToString", "GetHashCode", "Equals"].includes(n)
    );

  if (methodNames.length > 0) {
    lines.push("");
    lines.push(`## 公开方法 (${methodNames.length} 个)`);
    for (const mn of methodNames.slice(0, 30)) {
      lines.push(`- \`${mn}()\``);
    }
    if (methodNames.length > 30) {
      lines.push(`- ... 还有 ${methodNames.length - 30} 个方法`);
    }
  }

  return lines.join("\n");
}

export async function traceDependencyChain(
  workspaceRoot: string,
  startClass: string,
  direction: "up" | "down" = "down"
): Promise<string> {
  const lines: string[] = [
    `# 依赖链追踪: ${startClass} (${direction === "down" ? "向下" : "向上"})`,
    "",
  ];

  const projectDirs = await findProjectDirs(workspaceRoot);
  const classToProject = new Map<string, string>();
  const projectRefs = new Map<string, string[]>();

  for (const projectDir of projectDirs) {
    const projName = path.basename(projectDir);
    const csprojFiles = (await fs.readdir(projectDir)).filter((f) =>
      f.endsWith(".csproj")
    );

    for (const csproj of csprojFiles) {
      try {
        const content = await fs.readFile(
          path.join(projectDir, csproj),
          "utf-8"
        );
        const refs = [
          ...content.matchAll(/<ProjectReference\s+Include="[^"]*\\([^"\\]+)\.csproj"/g),
        ].map((m) => m[1]);
        projectRefs.set(projName, refs);
      } catch {
        continue;
      }
    }

    const csFiles = await findCsFiles(projectDir);
    for (const f of csFiles) {
      try {
        const content = await fs.readFile(f, "utf-8");
        const classMatches = content.matchAll(
          /(?:class|interface)\s+(\w+)/g
        );
        for (const m of classMatches) {
          classToProject.set(m[1], projName);
        }
      } catch {
        continue;
      }
    }
  }

  const startProject = classToProject.get(startClass);
  if (!startProject) {
    return `未找到类 "${startClass}" 所在的项目。`;
  }

  lines.push(`**起点**: ${startClass} in ${startProject}`);
  lines.push("");

  if (direction === "down") {
    lines.push("## 向下依赖 (当前类依赖什么)");
    const refs = projectRefs.get(startProject) || [];
    const vegaRefs = refs.filter((r) => r.startsWith("Vega."));

    for (const ref of vegaRefs) {
      const { layer } = classifyProject(ref);
      lines.push(`- → \`${ref}\` [${layer}]`);
    }
  } else {
    lines.push("## 向上依赖 (谁依赖当前类)");
    for (const [proj, refs] of projectRefs) {
      if (refs.includes(startProject)) {
        const { layer } = classifyProject(proj);
        lines.push(`- ← \`${proj}\` [${layer}]`);
      }
    }
  }

  return lines.join("\n");
}

function escapeRegex(str: string): string {
  return str.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}
