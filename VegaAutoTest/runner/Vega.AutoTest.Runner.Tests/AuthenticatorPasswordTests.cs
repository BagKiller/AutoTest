using Vega.AutoTest.Harness;
using Xunit;

public class AuthenticatorPasswordTests
{
    [Fact]
    public void ComponentRootConditionAlignsSimulatorGemAndGeneration()
    {
        const string json = """
            {
              "ToolConfigCollection": {
                "SelectedToolType": "Vega300",
                "ToolConfigs": [
                  {"ToolType": "Vega100", "RootCondition": "Vega100,Simulator,GemSim,Gen2", "UserDataFolder": "ResourcesDaily_Vega100-PIC"},
                  {"ToolType": "Vega300", "RootCondition": "Vega300,Tool,Gem,Gen1", "UserDataFolder": "ResourcesDaily_Vega300-HWv1"}
                ]
              }
            }
            """;

        var target = AuthenticatorPassword.FromComponent(json, 12);
        Assert.Equal("ResourcesDaily_Vega300-HWv1", AuthenticatorPassword.ReadUserDataFolder(json));
        Assert.Equal("Vega300", target.Model);
        Assert.False(target.Simulator);
        Assert.True(target.Gem);
        Assert.Equal("Gen1", target.Generation);
        Assert.Equal(12, target.MachineId);
        Assert.Equal("Vega300,Tool,Gem,Gen1", target.RootCondition);
    }

    [Fact]
    public void SelectedToolTypeIsTheAuthenticatorModel()
    {
        const string json = """
            {"ToolConfigCollection": {"SelectedToolType": "Vega100-Coherent", "ToolConfigs": [
              {"ToolType": "Vega100-Coherent", "RootCondition": "Vega100,Simulator,GemSim,Gen2"}]}}
            """;
        var target = AuthenticatorPassword.FromComponent(json, 0);
        Assert.Equal("Vega100-Coherent", target.Model);
        Assert.True(target.Simulator);
        Assert.False(target.Gem);
        Assert.Equal("Gen2", target.Generation);
    }

    [Fact]
    public void ProjectResourcesConfigIsPreferredOverTheBuildOutputCopy()
    {
        var root = Path.Combine(Path.GetTempPath(), "vega-component-" + Guid.NewGuid().ToString("N"));
        var project = Path.Combine(root, "Vega_SW", "Vega");
        var bin = Path.Combine(project, "bin", "Debug", "net8.0-windows10.0.19041.0", "win-x64");
        var previous = Environment.GetEnvironmentVariable("VEGA_AUTOTEST_COMPONENT_CONFIG");
        Environment.SetEnvironmentVariable("VEGA_AUTOTEST_COMPONENT_CONFIG", null);
        try
        {
            Directory.CreateDirectory(Path.Combine(project, "Resources"));
            Directory.CreateDirectory(Path.Combine(bin, "Resources"));
            static string Config(string type) => "{\"ToolConfigCollection\":{\"SelectedToolType\":\"" + type
                + "\",\"ToolConfigs\":[{\"ToolType\":\"" + type + "\",\"RootCondition\":\"" + type + ",Simulator,GemSim,Gen2\"}]}}";
            File.WriteAllText(Path.Combine(project, "Resources", "ComponentConfig.json"), Config("Vega300"));
            File.WriteAllText(Path.Combine(bin, "Resources", "ComponentConfig.json"), Config("Vega100"));
            var exe = Path.Combine(bin, "Vega.exe");

            Assert.Equal(project, AuthenticatorPassword.ProjectDirectory(bin));
            var chosen = AuthenticatorPassword.ResolveComponentConfig(exe, out var note);
            Assert.Equal(Path.Combine(project, "Resources", "ComponentConfig.json"), chosen);
            Assert.Contains("Vega100", note);
            Assert.Equal("Vega300", AuthenticatorPassword.LoadTarget(exe, chosen).Model);

            // Without the project copy the build output copy is used.
            File.Delete(chosen);
            Assert.Equal(Path.Combine(bin, "Resources", "ComponentConfig.json"),
                AuthenticatorPassword.ResolveComponentConfig(exe, out note));
            Assert.Null(note);

            // Explicit override wins.
            var custom = Path.Combine(root, "custom.json");
            File.WriteAllText(custom, Config("Vega500"));
            Environment.SetEnvironmentVariable("VEGA_AUTOTEST_COMPONENT_CONFIG", custom);
            Assert.Equal("Vega500", AuthenticatorPassword.LoadTarget(exe).Model);
        }
        finally
        {
            Environment.SetEnvironmentVariable("VEGA_AUTOTEST_COMPONENT_CONFIG", previous);
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void MachineIdUsesTheFirstDocumentThatActuallyContainsIt()
    {
        const string referenceOnly = """{"Sections":[{"Name":"General","Source":"General"}]}""";
        const string general = """{"Name":"Machine ID","Value":"7"}""";
        Assert.Null(AuthenticatorPassword.TryReadMachineId(referenceOnly));
        Assert.Equal(7, AuthenticatorPassword.FirstMachineId([referenceOnly, general]));
        Assert.Equal(0, AuthenticatorPassword.FirstMachineId([referenceOnly]));
    }

    [Fact]
    public void MissingPasswordOverrideIsNotTheOldFixedPassword()
    {
        var previousUser = Environment.GetEnvironmentVariable("VEGA_AUTOTEST_USER");
        var previousPassword = Environment.GetEnvironmentVariable("VEGA_AUTOTEST_PASSWORD");
        Environment.SetEnvironmentVariable("VEGA_AUTOTEST_USER", null);
        Environment.SetEnvironmentVariable("VEGA_AUTOTEST_PASSWORD", null);
        try
        {
            Assert.Equal("su", VegaEnv.UserName);
            Assert.Null(VegaEnv.PasswordOverride);
            Assert.Equal(@"C:\AutoTest\runtime\AuthenticatorApp\Authenticator.exe", VegaEnv.AuthenticatorExe);
            var called = false;
            var password = AuthenticatorPassword.Resolve(new EvidenceLog(), () =>
            {
                called = true;
                return "generated";
            });
            Assert.True(called);
            Assert.Equal("generated", password);
        }
        finally
        {
            Environment.SetEnvironmentVariable("VEGA_AUTOTEST_USER", previousUser);
            Environment.SetEnvironmentVariable("VEGA_AUTOTEST_PASSWORD", previousPassword);
        }
    }

    [Fact]
    public void ExplicitPasswordDoesNotOpenAuthenticator()
    {
        var previous = Environment.GetEnvironmentVariable("VEGA_AUTOTEST_PASSWORD");
        Environment.SetEnvironmentVariable("VEGA_AUTOTEST_PASSWORD", "from-env");
        try
        {
            var password = AuthenticatorPassword.Resolve(new EvidenceLog(), () => throw new InvalidOperationException("should not read"));
            Assert.Equal("from-env", password);
        }
        finally
        {
            Environment.SetEnvironmentVariable("VEGA_AUTOTEST_PASSWORD", previous);
        }
    }
}
