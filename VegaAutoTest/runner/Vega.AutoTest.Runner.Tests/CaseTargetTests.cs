using Newtonsoft.Json;
using Vega.AutoTest.Runner;
using Xunit;

public class CaseTargetTests
{
    [Fact]
    public void WorkItemSelectionPrefersExactTargetAndNeverUsesOtherCommit()
    {
        var generic = new CaseConfig { Id = "generic", WorkItems = [7] };
        var old = new CaseConfig { Id = "old", TargetSha = "aaaa", WorkItems = [7] };
        var exact = new CaseConfig { Id = "exact", TargetSha = "bbbb", WorkItems = [7] };
        var registry = new RegistryConfig { Cases = [old, generic, exact] };
        Assert.Same(exact, CaseRegistry.FindByWorkItem(registry, 7, "BBBB"));
        Assert.Same(generic, CaseRegistry.FindByWorkItem(registry, 7, "cccc"));
        Assert.Same(generic, CaseRegistry.FindByWorkItem(registry, 7));
        registry.Cases.Remove(generic);
        Assert.Null(CaseRegistry.FindByWorkItem(registry, 7, "cccc"));
        Assert.Null(CaseRegistry.FindByWorkItem(registry, 7));
    }

    [Fact]
    public void TargetConstraintSurvivesRegistryDeserialization()
    {
        var testCase = JsonConvert.DeserializeObject<CaseConfig>("{\"id\":\"bound\",\"targetSha\":\"aaaa\"}")!;
        Assert.True(CaseRegistry.MatchesTarget(testCase, "AAAA"));
        Assert.False(CaseRegistry.MatchesTarget(testCase, "bbbb"));
        Assert.False(CaseRegistry.MatchesTarget(testCase, null));
        Assert.True(CaseRegistry.MatchesTarget(new CaseConfig(), null));
    }
}
