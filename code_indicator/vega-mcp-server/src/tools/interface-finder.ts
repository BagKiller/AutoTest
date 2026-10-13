import * as fs from "fs/promises";
import * as path from "path";

interface DeviceTriple {
  name: string;
  interfaceProject: string;
  interfaceFile?: string;
  implementations: Array<{
    project: string;
    className: string;
    isSim: boolean;
    condition: string;
  }>;
}

async function findFileByName(
  baseDir: string,
  fileName: string
): Promise<string | null> {
  try {
    const entries = await fs.readdir(baseDir, { withFileTypes: true });
    for (const entry of entries) {
      if (
        ["bin", "obj", "node_modules", ".git", ".vs"].includes(entry.name)
      )
        continue;
      const fullPath = path.join(baseDir, entry.name);
      if (entry.isFile() && entry.name === fileName) return fullPath;
      if (entry.isDirectory()) {
        const result = await findFileByName(fullPath, fileName);
        if (result) return result;
      }
    }
  } catch {
    // skip
  }
  return null;
}

async function findCsFilesInProject(
  projectDir: string
): Promise<string[]> {
  const results: string[] = [];
  try {
    const entries = await fs.readdir(projectDir, { withFileTypes: true });
    for (const entry of entries) {
      if (
        ["bin", "obj", "node_modules", ".git"].includes(entry.name)
      )
        continue;
      const fullPath = path.join(projectDir, entry.name);
      if (entry.isDirectory()) {
        results.push(...(await findCsFilesInProject(fullPath)));
      } else if (entry.name.endsWith(".cs")) {
        results.push(fullPath);
      }
    }
  } catch {
    // skip
  }
  return results;
}

export async function findInterfaceTriple(
  workspaceRoot: string,
  deviceName: string
): Promise<string> {
  const lines: string[] = [
    `# 接口三件套查找: ${deviceName}`,
    "",
  ];

  const entries = await fs.readdir(workspaceRoot, { withFileTypes: true });
  const projectDirs = entries
    .filter((e) => e.isDirectory() && e.name.startsWith("Vega."))
    .map((e) => ({ name: e.name, path: path.join(workspaceRoot, e.name) }));

  const term = deviceName.toLowerCase();

  const interfaceProjects = projectDirs.filter(
    (p) =>
      p.name.toLowerCase().includes(term) &&
      (p.name.includes(".I") || p.name.startsWith("Vega.PD.I") ||
        p.name.startsWith("Vega.LD.I") ||
        p.name.startsWith("Vega.BL.I") ||
        p.name.startsWith("Vega.CD.I"))
  );

  const implProjects = projectDirs.filter(
    (p) =>
      p.name.toLowerCase().includes(term) &&
      !p.name.toLowerCase().includes("sim") &&
      !interfaceProjects.some((ip) => ip.name === p.name)
  );

  const simProjects = projectDirs.filter(
    (p) =>
      p.name.toLowerCase().includes(term) &&
      p.name.toLowerCase().includes("sim")
  );

  if (
    interfaceProjects.length === 0 &&
    implProjects.length === 0 &&
    simProjects.length === 0
  ) {
    const suggestions = projectDirs
      .filter((p) => {
        const parts = p.name.split(".");
        return parts.some((part) =>
          part.toLowerCase().includes(term.substring(0, 4))
        );
      })
      .map((p) => p.name)
      .slice(0, 10);

    lines.push(`未找到与 "${deviceName}" 相关的项目。`);
    if (suggestions.length > 0) {
      lines.push("");
      lines.push("**可能相关的项目**:");
      for (const s of suggestions) {
        lines.push(`- ${s}`);
      }
    }
    return lines.join("\n");
  }

  lines.push(`## 接口项目 (Interface)`);
  for (const ip of interfaceProjects) {
    lines.push(`- 📘 **${ip.name}**`);
    const csFiles = await findCsFilesInProject(ip.path);
    for (const f of csFiles) {
      const relPath = path.relative(workspaceRoot, f);
      const content = await fs.readFile(f, "utf-8");

      const interfaces = [
        ...content.matchAll(
          /public\s+interface\s+(\w+)(?:\s*:\s*([^\n{]+))?/g
        ),
      ];
      for (const iface of interfaces) {
        lines.push(`  - 接口: \`${iface[1]}\``);
        if (iface[2]) {
          lines.push(`    继承: \`${iface[2].trim()}\``);
        }

        const methods = [
          ...content.matchAll(
            /^\s+(?:Task(?:<[\w<>,?\s]+>)?|void|[\w<>,?\s]+)\s+(\w+)\s*\([^)]*\)\s*;/gm
          ),
        ];
        if (methods.length > 0) {
          lines.push(`    方法 (${methods.length} 个):`);
          for (const m of methods.slice(0, 15)) {
            lines.push(`      - \`${m[1]}()\``);
          }
          if (methods.length > 15) {
            lines.push(`      - ... 还有 ${methods.length - 15} 个`);
          }
        }
      }
      lines.push(`  - 文件: \`${relPath}\``);
    }
  }

  lines.push("");
  lines.push(`## 实现项目 (Implementation)`);
  for (const ip of implProjects) {
    lines.push(`- 📗 **${ip.name}**`);
    const csFiles = await findCsFilesInProject(ip.path);
    for (const f of csFiles) {
      const fileName = path.basename(f);
      if (fileName.startsWith("obj") || fileName.endsWith(".Designer.cs"))
        continue;
      lines.push(
        `  - \`${path.relative(workspaceRoot, f)}\``
      );
    }
  }

  lines.push("");
  lines.push(`## 仿真项目 (Simulation)`);
  if (simProjects.length === 0) {
    lines.push("- 未找到仿真项目");
  }
  for (const sp of simProjects) {
    lines.push(`- 📙 **${sp.name}**`);
    const csFiles = await findCsFilesInProject(sp.path);
    for (const f of csFiles) {
      const fileName = path.basename(f);
      if (fileName.startsWith("obj") || fileName.endsWith(".Designer.cs"))
        continue;
      lines.push(
        `  - \`${path.relative(workspaceRoot, f)}\``
      );
    }
  }

  lines.push("");
  lines.push("## 架构提示");

  const layerPrefix = interfaceProjects[0]?.name.split(".")[1] || "";
  if (layerPrefix === "PD") {
    lines.push(
      "这是一个 **物理设备(PD)层** 组件。PD 层直接与硬件交互。"
    );
    lines.push("- **向上**: 被 LD 层（逻辑设备层）调用");
    lines.push("- **向下**: 通过 CD 层（通信通道层）与硬件通信");
    lines.push(
      '- **开发模式**: 先用 Sim 仿真开发，再对接真实硬件'
    );
  } else if (layerPrefix === "LD") {
    lines.push(
      "这是一个 **逻辑设备(LD)层** 组件。LD 层提供硬件无关的设备抽象。"
    );
    lines.push("- **向上**: 被 BL 层（业务逻辑层）Service 调用");
    lines.push("- **向下**: 调用 PD 层（物理设备层）");
  } else if (layerPrefix === "BL") {
    lines.push(
      "这是一个 **业务逻辑(BL)层** 组件。BL 层编排设备操作和业务流程。"
    );
    lines.push("- **向上**: 被 UI 层 ViewModel 调用");
    lines.push("- **向下**: 调用 LD 层（逻辑设备层）");
  } else if (layerPrefix === "CD") {
    lines.push(
      "这是一个 **通信通道(CD)层** 组件。CD 层是最底层，直接处理通信协议。"
    );
    lines.push("- **向上**: 被 PD 层（物理设备层）调用");
    lines.push("- **通信方式**: 串口/TCP Socket/第三方SDK");
  }

  return lines.join("\n");
}

export async function listAllDevices(
  workspaceRoot: string,
  layer?: string
): Promise<string> {
  const entries = await fs.readdir(workspaceRoot, { withFileTypes: true });
  const projectDirs = entries
    .filter((e) => e.isDirectory() && e.name.startsWith("Vega."))
    .map((e) => e.name);

  const layerFilter = layer?.toUpperCase() || "";

  const deviceMap = new Map<
    string,
    { interfaces: string[]; impls: string[]; sims: string[] }
  >();

  for (const proj of projectDirs) {
    const parts = proj.split(".");
    if (parts.length < 3) continue;

    const projLayer = parts[1];
    if (layerFilter && projLayer !== layerFilter) continue;

    const isInterface =
      parts[2].startsWith("I") && parts[2][1] === parts[2][1].toUpperCase();
    const isSim = proj.toLowerCase().includes("sim");

    let deviceBase = parts[2];
    if (isInterface) deviceBase = deviceBase.substring(1);
    deviceBase = deviceBase.replace(/Sim$/i, "");

    const key = `${projLayer}.${deviceBase}`;
    if (!deviceMap.has(key))
      deviceMap.set(key, { interfaces: [], impls: [], sims: [] });

    const entry = deviceMap.get(key)!;
    if (isInterface) entry.interfaces.push(proj);
    else if (isSim) entry.sims.push(proj);
    else entry.impls.push(proj);
  }

  const lines: string[] = [
    `# Vega 设备/服务一览${layerFilter ? ` (${layerFilter} 层)` : ""}`,
    "",
    `共 ${deviceMap.size} 个设备/服务:`,
    "",
  ];

  const layerGroups = new Map<string, string[]>();
  for (const key of deviceMap.keys()) {
    const layer = key.split(".")[0];
    if (!layerGroups.has(layer)) layerGroups.set(layer, []);
    layerGroups.get(layer)!.push(key);
  }

  for (const [layer, keys] of layerGroups) {
    const layerNames: Record<string, string> = {
      PD: "Physical Device (物理设备层)",
      LD: "Logic Device (逻辑设备层)",
      BL: "Business Layer (业务逻辑层)",
      CD: "Communication Channel (通信通道层)",
      UI: "UI Layer (界面层)",
      Facade: "Facade (外观层)",
      Platform: "Platform (平台层)",
    };
    lines.push(`## ${layerNames[layer] || layer}`);
    lines.push("");

    for (const key of keys) {
      const deviceName = key.split(".")[1];
      const entry = deviceMap.get(key)!;

      const status =
        entry.interfaces.length > 0 && entry.impls.length > 0
          ? "✅ 完整"
          : entry.interfaces.length > 0
            ? "⚠️ 仅接口"
            : "📦 仅实现";

      const simStatus = entry.sims.length > 0 ? " | 🔷 有仿真" : "";

      lines.push(`- **${deviceName}** ${status}${simStatus}`);
      if (entry.interfaces.length > 0)
        lines.push(`  - 接口: ${entry.interfaces.join(", ")}`);
      if (entry.impls.length > 0)
        lines.push(`  - 实现: ${entry.impls.join(", ")}`);
      if (entry.sims.length > 0)
        lines.push(`  - 仿真: ${entry.sims.join(", ")}`);
    }
    lines.push("");
  }

  return lines.join("\n");
}
