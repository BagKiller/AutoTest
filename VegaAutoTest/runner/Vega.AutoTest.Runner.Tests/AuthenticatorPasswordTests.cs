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
