import * as fs from "fs/promises";
import * as path from "path";

interface ProjectInfo {
  name: string;
  layer: string;
  isInterface: boolean;
  references: string[];
  outputType: string;
  targetFramework: string;
}

function classifyLayer(projectName: string): {
  layer: string;
  isInterface: boolean;
} {
  const parts = projectName.split(".");
  if (parts.length < 2) return { layer: "Other", isInterface: false };

  const layerPart = parts[1];
  const subPart = parts[2] || "";

  const layerMap: Record<string, string> = {
    UI: "UI",
    BL: "BL",
    LD: "LD",
    PD: "PD",
    CD: "CD",
    Data: "Data",
    Platform: "Platform",
    Facade: "Facade",
    Utility: "Utility",
    Algo: "Algorithm",
    ThirdParty: "ThirdParty",
  };

  const layer = layerMap[layerPart] || "Other";
  const isInterface =
    subPart.startsWith("I") &&
    subPart.length > 1 &&
    subPart[1] === subPart[1].toUpperCase();

  return { layer, isInterface };
}

export async function analyzeSolution(
  workspaceRoot: string
): Promise<string> {
  const slnPath = path.join(workspaceRoot, "Vega.sln");
  const slnContent = await fs.readFile(slnPath, "utf-8");

  const projectMatches = [
    ...slnContent.matchAll(
      /Project\("[^"]+"\)\s*=\s*"([^"]+)",\s*"([^"]+)"/g
    ),
  ];

  const projects: ProjectInfo[] = [];

  for (const match of projectMatches) {
    const projName = match[1];
    const projPath = match[2];

    if (!projPath.endsWith(".csproj")) continue;

    const { layer, isInterface } = classifyLayer(projName);

    let references: string[] = [];
    let outputType = "Library";
    let targetFramework = "";

    const fullProjPath = path.join(
      workspaceRoot,
      projPath.replace(/\\/g, "/")
    );
    try {
      const csprojContent = await fs.readFile(fullProjPath, "utf-8");

      references = [
        ...csprojContent.matchAll(
          /<ProjectReference\s+Include="[^"]*\\([^"\\]+)\.csproj"/g
        ),
      ].map((m) => m[1]);

      const outputTypeMatch = csprojContent.match(
        /<OutputType>(\w+)<\/OutputType>/
      );
      if (outputTypeMatch) outputType = outputTypeMatch[1];

      const tfmMatch = csprojContent.match(
        /<TargetFramework>([\w.-]+)<\/TargetFramework>/
      );
      if (tfmMatch) targetFramework = tfmMatch[1];
    } catch {
      // skip inaccessible csproj
    }

    projects.push({
      name: projName,
      layer,
      isInterface,
      references,
      outputType,
      targetFramework,
    });
  }

  const lines: string[] = [
    `# Vega 解决方案结构分析`,
    "",
    `**共 ${projects.length} 个项目**`,
    "",
  ];

  const layerGroups = new Map<string, ProjectInfo[]>();
  for (const proj of projects) {
    if (!layerGroups.has(proj.layer)) layerGroups.set(proj.layer, []);
    layerGroups.get(proj.layer)!.push(proj);
  }

  const layerOrder = [
    "UI",
    "BL",
    "LD",
    "PD",
    "CD",
    "Data",
    "Platform",
    "Facade",
    "Algorithm",
    "Utility",
    "ThirdParty",
    "Other",
  ];

  const layerDescriptions: Record<string, string> = {
    UI: "用户界面层 - WPF MVVM 模块",
    BL: "业务逻辑层 - 服务编排与业务流程",
    LD: "逻辑设备层 - 硬件无关的设备抽象",
    PD: "物理设备层 - 直接硬件控制",
    CD: "通信通道层 - 串口/TCP/SDK 通信",
    Data: "数据层 - 数据模型定义",
    Platform: "平台层 - 数据库/Provider/基础设施",
    Facade: "外观层 - GEM/SECS 集成",
    Algorithm: "算法层 - 图像处理与分析",
    Utility: "工具层 - 共享工具类",
    ThirdParty: "第三方层 - 外部 SDK 封装",
    Other: "其他",
  };

  lines.push("## 层级概览");
  lines.push("");
  lines.push("| 层级 | 项目数 | 说明 |");
  lines.push("|------|--------|------|");
  for (const layer of layerOrder) {
    const group = layerGroups.get(layer);
    if (group) {
      const desc = layerDescriptions[layer] || "";
      lines.push(`| ${layer} | ${group.length} | ${desc} |`);
    }
  }
  lines.push("");

  lines.push("## 依赖关系矩阵（层级间）");
  lines.push("");
  lines.push("```");
  lines.push("依赖流向 (→ 表示依赖):");
  lines.push("");
  lines.push("  UI → BL → LD → PD → CD");
  lines.push("  ↓         ↓         ↓");
  lines.push("  Data    Data    ThirdParty");
  lines.push("  ↓         ↓");
  lines.push("  Platform  Platform");
  lines.push("```");
  lines.push("");

  for (const layer of layerOrder) {
    const group = layerGroups.get(layer);
    if (!group || group.length === 0) continue;

    const ifaces = group.filter((p) => p.isInterface);
    const impls = group.filter((p) => !p.isInterface);

    lines.push(
      `## ${layer} 层 (${group.length} 个项目)`
    );
    lines.push(`*${layerDescriptions[layer] || ""}*`);
    lines.push("");

    if (ifaces.length > 0) {
      lines.push("**接口项目:**");
      for (const p of ifaces) {
        lines.push(`- 📘 \`${p.name}\``);
        if (p.references.length > 0) {
          lines.push(
            `  依赖: ${p.references.map((r) => `\`${r}\``).join(", ")}`
          );
        }
      }
      lines.push("");
    }

    if (impls.length > 0) {
      lines.push("**实现项目:**");
      for (const p of impls) {
        const typeTag = p.outputType === "Exe" ? " 🚀可执行" : "";
        lines.push(`- 📗 \`${p.name}\`${typeTag}`);
        if (p.references.length > 0) {
          lines.push(
            `  依赖: ${p.references.map((r) => `\`${r}\``).join(", ")}`
          );
        }
      }
      lines.push("");
    }
  }

  return lines.join("\n");
}

export async function getProjectDependencyTree(
  workspaceRoot: string,
  projectName: string
): Promise<string> {
  const entries = await fs.readdir(workspaceRoot, { withFileTypes: true });
  const projDir = entries.find(
    (e) =>
      e.isDirectory() &&
      e.name.toLowerCase() === projectName.toLowerCase()
  );

  if (!projDir) {
    const matches = entries
      .filter(
        (e) =>
          e.isDirectory() &&
          e.name.toLowerCase().includes(projectName.toLowerCase())
      )
      .map((e) => e.name)
      .slice(0, 10);

    let result = `未找到项目 "${projectName}"。`;
    if (matches.length > 0) {
      result += `\n\n可能匹配的项目:\n${matches.map((m) => `- ${m}`).join("\n")}`;
    }
    return result;
  }

  const csprojFiles = (
    await fs.readdir(path.join(workspaceRoot, projDir.name))
  ).filter((f) => f.endsWith(".csproj"));

  if (csprojFiles.length === 0) {
    return `项目 "${projDir.name}" 中未找到 .csproj 文件。`;
  }

  const csprojPath = path.join(workspaceRoot, projDir.name, csprojFiles[0]);
  const csprojContent = await fs.readFile(csprojPath, "utf-8");

  const directRefs = [
    ...csprojContent.matchAll(
      /<ProjectReference\s+Include="[^"]*\\([^"\\]+)\.csproj"/g
    ),
  ].map((m) => m[1]);

  const packageRefs = [
    ...csprojContent.matchAll(
      /<PackageReference\s+Include="([^"]+)"/g
    ),
  ].map((m) => m[1]);

  const lines: string[] = [
    `# 项目依赖树: ${projDir.name}`,
    "",
  ];

  const { layer, isInterface } = classifyLayer(projDir.name);
  lines.push(`- **层级**: ${layer}${isInterface ? " (接口)" : " (实现)"}`);
  lines.push("");

  if (directRefs.length > 0) {
    lines.push(`## 项目引用 (${directRefs.length} 个)`);
    for (const ref of directRefs) {
      const { layer: refLayer } = classifyLayer(ref);
      lines.push(`- \`${ref}\` [${refLayer}]`);
    }
    lines.push("");
  }

  if (packageRefs.length > 0) {
    lines.push(`## NuGet 包引用 (${packageRefs.length} 个)`);
    const galaxyPkgs = packageRefs.filter((p) =>
      p.toLowerCase().includes("galaxy")
    );
    const otherPkgs = packageRefs.filter(
      (p) => !p.toLowerCase().includes("galaxy")
    );

    if (galaxyPkgs.length > 0) {
      lines.push("**Galaxy 平台包:**");
      for (const pkg of galaxyPkgs) {
        lines.push(`- 📦 ${pkg}`);
      }
    }
    if (otherPkgs.length > 0) {
      lines.push("**第三方包:**");
      for (const pkg of otherPkgs) {
        lines.push(`- 📦 ${pkg}`);
      }
    }
    lines.push("");
  }

  lines.push("## 被谁引用");
  const allProjDirs = entries
    .filter((e) => e.isDirectory() && e.name.startsWith("Vega"))
    .map((e) => e.name);

  const dependents: string[] = [];
  for (const pd of allProjDirs) {
    const pdCsproj = (
      await fs.readdir(path.join(workspaceRoot, pd))
    ).filter((f) => f.endsWith(".csproj"));
    for (const cf of pdCsproj) {
      try {
        const content = await fs.readFile(
          path.join(workspaceRoot, pd, cf),
          "utf-8"
        );
        if (content.includes(projDir.name)) {
          dependents.push(pd);
        }
      } catch {
        continue;
      }
    }
  }

  if (dependents.length > 0) {
    for (const dep of dependents) {
      const { layer: depLayer } = classifyLayer(dep);
      lines.push(`- ← \`${dep}\` [${depLayer}]`);
    }
  } else {
    lines.push("- 无其他项目引用此项目");
  }

  return lines.join("\n");
}
