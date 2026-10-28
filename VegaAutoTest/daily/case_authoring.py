"""Bring product regression tests into Runner without changing their assertions.

This is executable *partial* coverage, never an end-to-end acceptance oracle.
Each SHA gets its own sources/project so later authoring cannot mutate frozen tests.
"""
import re
from pathlib import Path
from xml.sax.saxutils import escape


ADAPTER = r'''
global using System.IO;
using System.Reflection;
using Vega.AutoTest.Harness;
using Xunit;
using Xunit.Sdk;

namespace Vega.AutoTest.Generated {
public class ProductRegressionAdapter {
 private readonly Xunit.Abstractions.ITestOutputHelper output;
 public ProductRegressionAdapter(Xunit.Abstractions.ITestOutputHelper output) { this.output = output; }
 [Fact] public async Task RunOriginalRegressionAssertions() {
  var executed = 0;
  foreach (var type in typeof(ProductRegressionAdapter).Assembly.GetTypes().OrderBy(t => t.FullName)) {
   if (type == typeof(ProductRegressionAdapter)) continue;
   foreach (var method in type.GetMethods().Where(m => m.GetCustomAttribute<FactAttribute>() != null).OrderBy(m => m.Name)) {
    var fact = method.GetCustomAttribute<FactAttribute>()!;
    // A skipped original test is not evidence of a passing regression.
    if (fact.Skip != null) throw new InvalidOperationException($"Original test is skipped: {type.FullName}.{method.Name}: {fact.Skip}");
    var rows = method.GetCustomAttribute<TheoryAttribute>() != null
     ? method.GetCustomAttributes<DataAttribute>().SelectMany(a => a.GetData(method)).ToArray()
     : new[] { Array.Empty<object>() };
    if (rows.Length == 0) throw new InvalidOperationException($"No theory data: {type.FullName}.{method.Name}");
    foreach (var row in rows) {
     var id = $"{type.FullName}.{method.Name}[{executed++}]";
     var recorder = new AssertionRecorder(); object? instance = null;
     try {
      if (!method.IsStatic) {
       var parameters = type.GetConstructors().Single().GetParameters().Select(p =>
        p.ParameterType == typeof(Xunit.Abstractions.ITestOutputHelper) ? (object)output
         : throw new NotSupportedException($"Unsupported fixture dependency: {type.FullName}/{p.ParameterType}")).ToArray();
       instance = Activator.CreateInstance(type, parameters);
      }
      if (instance is IAsyncLifetime lifetime) await lifetime.InitializeAsync();
      var value = method.Invoke(instance, row);
      if (value is Task task) await task;
      else if (value is ValueTask valueTask) await valueTask;
      recorder.Pass(id,"product-regression",id,"Original assertions pass","Original method completed");
     } catch(Exception ex) {
      var actual = ex is TargetInvocationException invocation ? invocation.InnerException! : ex;
      recorder.Fail(id,"product-regression",id,"Original assertions pass",actual.ToString());
      System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(actual).Throw();
     } finally { try { if(instance is IAsyncLifetime lifetime) await lifetime.DisposeAsync(); (instance as IDisposable)?.Dispose(); } finally { recorder.Flush(); } }
    }
   }
  }
  Assert.True(executed > 0, "No original regression test executed");
 }
}}
'''


def generate(d, child, manifest, registry):
    commit = manifest['commits'][0]
    sha = manifest['head']
    test_sources = []
    candidates = [name for name in commit['files'] if '.Tests/' in name and name.endswith('.cs')]
    # A diff also lists deleted tests. Only retrieve files that exist at this SHA.
    existing = set(d.git('ls-tree', '-r', '--name-only', sha, repo=Path(manifest['source'])).splitlines()) if candidates else set()
    for name in commit['files']:
        if name not in candidates or name not in existing:
            continue
        source = d.git('show', f'{sha}:{name}', repo=Path(manifest['source']))
        if re.search(r'\[(?:Fact|Theory)(?:\s*\(|\s*\])', source):
            test_sources.append((name, source))
    destination = d.ROOT / 'probes' / 'generated' / sha[:12]
    cid = 'commit-' + sha[:12] + '-product-tests'
    if not test_sources:
        graph_project = d.ROOT / 'probes' / 'Vega.AutoTest.LockGraphV3' / 'Vega.AutoTest.LockGraphV3.csproj'
        if commit['files'] and all(name == 'packages.lock.json' for name in commit['files']) and graph_project.exists():
            cid = 'commit-' + sha[:12] + '-lock-graph'
            case = dict(id=cid, title='Committed dependency graph: Project/RID aware', targetSha=sha,
                workItems=commit.get('workItems', []), tags=['generated', 'partial-acceptance'], evidenceFiles=[],
                layers={'logic': dict(project=graph_project.relative_to(d.ROOT).as_posix(),
                    filter='FullyQualifiedName~DependencyLockLogic', timeoutSeconds=300, minAssertions=1,
                    requiresVegaExe=True, requiresL1Api=False)})
            old = next((c for c in registry['cases'] if c['id'] == cid), None)
            if old is not None and old != case:
                raise ValueError('Existing lock-graph case differs; create a separate version')
            if old is None:
                registry['cases'].append(case)
                d.save(d.REGISTRY, registry)
            result = dict(sha=sha, state='REUSED_REQUIRES_CHECK', caseId=cid, sources=[case['layers']['logic']['project']],
                boundary='Dependency graph structure only; isolated build and locked restore are separate requirements.')
            d.save(child / 'preparation' / 'authoring.json', result)
            return result
        result = dict(sha=sha, state='NEEDS_AUTHORING', caseId=None,
                      reason='No changed xUnit source found. AI must author behavioral fixtures; registry absence is not a blocker.')
        d.save(child / 'preparation' / 'authoring.json', result)
        return result
    sources = {f'Original/{index:02d}_{Path(name).name}': text for index, (name, text) in enumerate(test_sources)}
    namespaces = set()
    for _, text in test_sources:
        if 'TestPaths.' in text:
            match = re.search(r'namespace\s+([\w.]+)', text)
            if match:
                namespaces.add(match[1])
    # Original TestPaths assumes a product-local bin layout. The adapter must use
    # Runner's exact SHA worktree instead; no expected value/assertion is changed.
    sources['TestPaths.cs'] = '\n'.join(
        f'namespace {ns} {{ internal static class TestPaths {{ public static string RepoRoot => '
        'System.Environment.GetEnvironmentVariable("VEGA_SW_ROOT") ?? throw new System.InvalidOperationException("VEGA_SW_ROOT required"); } }'
        for ns in sorted(namespaces))
    sources['ProductRegressionAdapter.cs'] = ADAPTER
    # Runtime assemblies are copied from the isolated target, not a shared build.
    harness = escape(str(d.ROOT / 'harness' / 'Vega.AutoTest.Harness.csproj'))
    project_template = f'''<Project Sdk="Microsoft.NET.Sdk">
 <PropertyGroup><TargetFramework>net8.0-windows10.0.19041.0</TargetFramework><UseWPF>true</UseWPF>
 <ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><IsTestProject>true</IsTestProject><PlatformTarget>x64</PlatformTarget>
 <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally><NuGetAudit>false</NuGetAudit>
 <ProductBin>$(VEGA_SW_ROOT)\\Vega\\bin\\Debug\\net8.0-windows10.0.19041.0\\win-x64</ProductBin>
 <EnableDefaultCompileItems>false</EnableDefaultCompileItems><AssemblyName>ASSEMBLY_NAME</AssemblyName></PropertyGroup>
 <ItemGroup><PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0"/><PackageReference Include="xunit" Version="2.5.3"/>
 <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3"/><PackageReference Include="Moq" Version="4.20.70"/>
 <ProjectReference Include="{harness}"/><Using Include="Xunit"/>SOURCE_ITEMS
 <Reference Include="$(ProductBin)\\*.dll"/><Content Include="$(ProductBin)\\*.dll" Link="%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest"/>
 </ItemGroup></Project>'''
    groups = {}
    for index, (name, _) in enumerate(test_sources):
        groups.setdefault(name.split('/')[0], []).append(f'Original/{index:02d}_{Path(name).name}')
    layers = {}
    for index, (assembly, names) in enumerate(sorted(groups.items())):
        project_name = f'Generated.{index}.csproj'
        items = ''.join(f'<Compile Include="{escape(name)}"/>' for name in names + ['TestPaths.cs', 'ProductRegressionAdapter.cs'])
        sources[project_name] = project_template.replace('ASSEMBLY_NAME', assembly).replace('SOURCE_ITEMS', items)
        layers['logic' + str(index)] = dict(project=(destination / project_name).relative_to(d.ROOT).as_posix(),
            filter='FullyQualifiedName~Vega.AutoTest.Generated.ProductRegressionAdapter',
            timeoutSeconds=600, minAssertions=1, requiresVegaExe=True, requiresL1Api=False)
    # Preparation may repair adapters, but must never rewrite frozen sources.
    changed = any((destination / name).exists() and (destination / name).read_text(encoding='utf-8') != content
                  for name, content in sources.items())
    if changed:
        for path in (d.ROOT / 'daily' / 'runs').rglob('plan.json'):
            files = d.load(path, {}).get('frozenCases', {}).get('files', {})
            if any((d.ROOT / name).resolve().is_relative_to(destination.resolve()) for name in files):
                raise ValueError('Generated source is already frozen; create a separate version')
    for name, content in sources.items():
        path = destination / name
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists() or path.read_text(encoding='utf-8') != content:
            path.write_text(content, encoding='utf-8')
    case = dict(id=cid, title='Original product regression assertions at ' + sha[:12], targetSha=sha,
                workItems=commit.get('workItems', []), tags=['generated', 'partial-acceptance'], evidenceFiles=[],
                layers=layers)
    old = next((c for c in registry['cases'] if c['id'] == cid), None)
    if old is not None and old != case:
        for path in (d.ROOT / 'daily' / 'runs').rglob('plan.json'):
            if cid in {c['id'] for c in d.load(path, {}).get('frozenCases', {}).get('cases', [])}:
                raise ValueError('Generated case is already frozen; create a separate version')
        registry['cases'].remove(old)
        old = None
    if old is None:
        registry['cases'].append(case)
        d.save(d.REGISTRY, registry)
    result = dict(sha=sha, state='GENERATED_REQUIRES_CHECK', caseId=cid,
                  sources=[name for name, _ in test_sources],
                  boundary='Original unit assertions only; required UI/remote collector behavior remains separately reviewed.')
    d.save(child / 'preparation' / 'authoring.json', result)
    return result
