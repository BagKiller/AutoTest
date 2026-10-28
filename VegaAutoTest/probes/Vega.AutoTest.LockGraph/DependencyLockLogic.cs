using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Vega.AutoTest.Harness;
using Xunit;

namespace Vega.AutoTest.LockGraph;
public class DependencyLockLogic
{
    [Fact]
    public void CommittedLockHasResolvableDependencyGraph()
    {
        var recorder = new AssertionRecorder();
        try
        {
            var repo = Environment.GetEnvironmentVariable("VEGA_SW_ROOT") ?? throw new InvalidOperationException("VEGA_SW_ROOT required");
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "packages.lock.json")));
            Assert.Equal(2, document.RootElement.GetProperty("version").GetInt32());
            var frameworks = document.RootElement.GetProperty("dependencies").EnumerateObject().ToArray();
            Assert.NotEmpty(frameworks);
            foreach (var framework in frameworks)
            {
                Assert.StartsWith("net8.0-windows", framework.Name);
                var nodes = framework.Value.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);
                Assert.NotEmpty(nodes);
                foreach (var node in nodes)
                {
                    Assert.True(node.Value.TryGetProperty("type", out var type), "Missing node type: " + node.Key);
                    // Project references intentionally omit package version/contentHash.
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
            recorder.Pass("dependencies.lock.graph.v2", "logic", "Committed graph integrity", "All project/package nodes typed; package resolutions and graph references present", "Matched; locked restore not established");
        }
        catch (Exception error)
        {
            recorder.Fail("dependencies.lock.graph.v2", "logic", "Committed graph integrity", "Well-formed dependency graph", error.ToString());
            throw;
        }
        finally { recorder.Flush(); }
    }
}
