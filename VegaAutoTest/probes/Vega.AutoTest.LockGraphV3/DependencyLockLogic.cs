using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Vega.AutoTest.Harness;
using Xunit;

namespace Vega.AutoTest.LockGraphV3;
public class DependencyLockLogic
{
    internal static void Validate(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Equal(2, document.RootElement.GetProperty("version").GetInt32());
        var frameworks = document.RootElement.GetProperty("dependencies").EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        Assert.NotEmpty(frameworks);
        foreach (var framework in frameworks)
        {
            Assert.StartsWith("net8.0-windows", framework.Key);
            var nodes = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            if (framework.Key.Contains('/'))
            {
                Assert.True(frameworks.TryGetValue(framework.Key.Split('/')[0], out var parent), "RID target requires its base framework");
                foreach (var node in parent.EnumerateObject()) nodes[node.Name] = node.Value;
            }
            foreach (var node in framework.Value.EnumerateObject()) nodes[node.Name] = node.Value;
            Assert.NotEmpty(nodes);
            foreach (var node in nodes)
            {
                Assert.True(node.Value.TryGetProperty("type", out var type), "Missing node type: " + node.Key);
                if (type.GetString() != "Project")
                {
                    Assert.True(node.Value.TryGetProperty("resolved", out var resolved), "Missing package resolution: " + node.Key);
                    Assert.False(string.IsNullOrWhiteSpace(resolved.GetString()));
                }
                if (node.Value.TryGetProperty("dependencies", out var dependencies))
                    foreach (var dependency in dependencies.EnumerateObject())
                        Assert.True(nodes.ContainsKey(dependency.Name), $"Missing dependency node: {node.Key} -> {dependency.Name}");
            }
        }
    }

    [Fact]
    public void CommittedLockHasResolvableDependencyGraph()
    {
        var recorder = new AssertionRecorder();
        try
        {
            var repo = Environment.GetEnvironmentVariable("VEGA_SW_ROOT") ?? throw new InvalidOperationException("VEGA_SW_ROOT required");
            Validate(File.ReadAllText(Path.Combine(repo, "packages.lock.json")));
            recorder.Pass("dependencies.lock.graph.v3", "logic", "Graph integrity with RID inheritance", "Project and package nodes, inherited RID dependencies resolve", "Matched; not proof of locked restore");
        }
        catch (Exception error)
        {
            recorder.Fail("dependencies.lock.graph.v3", "logic", "Graph integrity with RID inheritance", "Resolvable graph", error.ToString());
            throw;
        }
        finally { recorder.Flush(); }
    }
}

// Fixture-only engineering checks, excluded from the registered product case.
public class GraphContractTests
{
    [Fact] public void ProjectNodeNeedsNoResolvedVersion() => DependencyLockLogic.Validate("""{"version":2,"dependencies":{"net8.0-windows":{"project":{"type":"Project"}}}}""");
    [Fact] public void RidInheritsBaseNodes() => DependencyLockLogic.Validate("""{"version":2,"dependencies":{"net8.0-windows":{"a":{"type":"Direct","resolved":"1"}},"net8.0-windows/win-x64":{"b":{"type":"Direct","resolved":"1","dependencies":{"a":"1"}}}}}""");
    [Fact] public void DanglingNodeIsRejected() => Assert.ThrowsAny<Exception>(()=>DependencyLockLogic.Validate("""{"version":2,"dependencies":{"net8.0-windows":{"a":{"type":"Direct","resolved":"1","dependencies":{"absent":"1"}}}}}"""));
    [Fact] public void PackageWithoutResolutionIsRejected() => Assert.ThrowsAny<Exception>(()=>DependencyLockLogic.Validate("""{"version":2,"dependencies":{"net8.0-windows":{"a":{"type":"Direct"}}}}"""));
}
