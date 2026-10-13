import * as fs from "fs/promises";
import * as path from "path";
import { execFile } from "child_process";
import { promisify } from "util";

const execFileAsync = promisify(execFile);

interface SearchResult {
  file: string;
  line: number;
  content: string;
  context?: string[];
}

async function ripgrepSearch(
  workspaceRoot: string,
  pattern: string,
  fileGlob?: string,
  maxResults: number = 30
): Promise<SearchResult[]> {
  const args = [
    "--json",
    "--max-count",
    "5",
    "-n",
    "--no-heading",
  ];

  if (fileGlob) {
    args.push("--glob", fileGlob);
  }

  args.push("--glob", "!**/bin/**");
  args.push("--glob", "!**/obj/**");
  args.push("--glob", "!**/node_modules/**");
  args.push("--glob", "!**/.git/**");

  args.push(pattern, workspaceRoot);

  try {
    const { stdout } = await execFileAsync("rg", args, {
      maxBuffer: 10 * 1024 * 1024,
    });

    const results: SearchResult[] = [];
    for (const line of stdout.split("\n")) {
      if (!line.trim()) continue;
      try {
        const parsed = JSON.parse(line);
        if (parsed.type === "match") {
          results.push({
            file: parsed.data.path.text,
            line: parsed.data.line_number,
            content: parsed.data.lines.text.trim(),
          });
          if (results.length >= maxResults) break;
        }
      } catch {
        continue;
      }
    }
    return results;
  } catch {
    return fallbackSearch(workspaceRoot, pattern, fileGlob, maxResults);
  }
}

async function fallbackSearch(
  workspaceRoot: string,
  pattern: string,
  fileGlob?: string,
  maxResults: number = 30
): Promise<SearchResult[]> {
  const results: SearchResult[] = [];
  const regex = new RegExp(pattern, "i");

  async function searchDir(dir: string) {
    if (results.length >= maxResults) return;

    try {
      const entries = await fs.readdir(dir, { withFileTypes: true });
      for (const entry of entries) {
        if (results.length >= maxResults) break;
        if (
          ["bin", "obj", "node_modules", ".git", ".vs"].includes(entry.name)
        )
          continue;

        const fullPath = path.join(dir, entry.name);
        if (entry.isDirectory()) {
          await searchDir(fullPath);
        } else if (
          entry.name.endsWith(".cs") ||
          entry.name.endsWith(".xaml") ||
          entry.name.endsWith(".json")
        ) {
          if (
            fileGlob &&
            !entry.name.match(
              new RegExp(fileGlob.replace("*", ".*"))
            )
          )
            continue;

          try {
            const content = await fs.readFile(fullPath, "utf-8");
            const lines = content.split("\n");
            for (let i = 0; i < lines.length; i++) {
              if (regex.test(lines[i])) {
                results.push({
                  file: fullPath,
                  line: i + 1,
                  content: lines[i].trim(),
                });
                if (results.length >= maxResults) return;
              }
            }
          } catch {
            continue;
          }
        }
      }
    } catch {
      // skip
    }
  }

  await searchDir(workspaceRoot);
  return results;
}

export async function searchCode(
  workspaceRoot: string,
  pattern: string,
  options: {
    fileType?: string;
    layer?: string;
    maxResults?: number;
  } = {}
): Promise<string> {
  const { fileType, layer, maxResults = 30 } = options;

  let fileGlob: string | undefined;
  if (fileType) {
    fileGlob = `*.${fileType}`;
  }

  let searchRoot = workspaceRoot;
  if (layer) {
    const layerPrefix = `Vega.${layer}.`;
    const entries = await fs.readdir(workspaceRoot, { withFileTypes: true });
    const matchingDirs = entries
      .filter((e) => e.isDirectory() && e.name.startsWith(layerPrefix))
      .map((e) => e.name);

    if (matchingDirs.length === 0) {
      return `未找到 "${layer}" 层的项目目录。有效层级: UI, BL, LD, PD, CD, Data, Platform, Facade`;
    }
  }

  const results = await ripgrepSearch(
    searchRoot,
    pattern,
    fileGlob,
    maxResults
  );

  if (results.length === 0) {
    return `在项目中未找到匹配 "${pattern}" 的代码。\n\n**提示**: 尝试更宽泛的搜索词，或者检查拼写。`;
  }

  const lines: string[] = [
    `# 代码搜索: "${pattern}"`,
    ``,
    `找到 ${results.length} 个匹配${results.length >= maxResults ? " (已截断)" : ""}:`,
    "",
  ];

  const byFile = new Map<string, SearchResult[]>();
  for (const r of results) {
    const relPath = path.relative(workspaceRoot, r.file);
    if (!byFile.has(relPath)) byFile.set(relPath, []);
    byFile.get(relPath)!.push(r);
  }

  for (const [file, matches] of byFile) {
    const parts = file.split(path.sep);
    const project = parts[0] || "";

    lines.push(`## ${project}`);
    lines.push(`📄 \`${file}\``);

    for (const m of matches) {
      lines.push(`- L${m.line}: \`${m.content.substring(0, 120)}\``);
    }
    lines.push("");
  }

  return lines.join("\n");
}

export async function searchXamlBindings(
  workspaceRoot: string,
  bindingName: string
): Promise<string> {
  const results = await ripgrepSearch(
    workspaceRoot,
    bindingName,
    "*.xaml",
    20
  );

  const csResults = await ripgrepSearch(
    workspaceRoot,
    bindingName,
    "*.cs",
    20
  );

  const lines: string[] = [
    `# XAML 绑定追踪: "${bindingName}"`,
    "",
  ];

  if (results.length > 0) {
    lines.push(`## XAML 中的引用 (${results.length} 处)`);
    for (const r of results) {
      const relPath = path.relative(workspaceRoot, r.file);
      lines.push(`- \`${relPath}\` L${r.line}:`);
      lines.push(`  \`${r.content.substring(0, 150)}\``);
    }
    lines.push("");
  }

  if (csResults.length > 0) {
    lines.push(`## C# 中的定义/引用 (${csResults.length} 处)`);
    for (const r of csResults) {
      const relPath = path.relative(workspaceRoot, r.file);
      lines.push(`- \`${relPath}\` L${r.line}:`);
      lines.push(`  \`${r.content.substring(0, 150)}\``);
    }
  }

  if (results.length === 0 && csResults.length === 0) {
    lines.push(`未找到 "${bindingName}" 的绑定或定义。`);
  }

  return lines.join("\n");
}
