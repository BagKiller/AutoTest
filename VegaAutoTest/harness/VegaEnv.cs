// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

namespace Vega.AutoTest.Harness;

/// <summary>
/// 测试环境常量唯一出处：被测 Vega.exe 路径、取证落盘目录、登录凭据、L1 API 基址。
///
/// 每项都可用环境变量覆盖；被测主仓库根按三级解析：VEGA_SW_ROOT 环境变量 >
/// 向上查找 Vega.sln（历史同仓布局兼容）> 报错指路。用属性 + Lazy 而非 const：
/// const 会在编译期内联进引用方程序集，改环境变量也不生效；Lazy 则保证设了
/// VEGA_AUTOTEST_EXE 时不必再去推导仓库根。
/// </summary>
public static class VegaEnv
{
    private static readonly Lazy<string> LazyRepoRoot = new(FindRepoRoot);
    private static readonly Lazy<string> LazyAutoTestRoot = new(FindAutoTestRoot);
    private static readonly Lazy<string> LazyVegaExe = new(() => Env("VEGA_AUTOTEST_EXE") ?? ResolveVegaExe());
    private static readonly Lazy<string> LazyOutDir =
        new(() => Env("VEGA_AUTOTEST_OUTDIR") ?? Path.Combine(AutoTestRoot, "runs", "manual"));

    /// <summary>被测 Vega 主仓库根：VEGA_SW_ROOT > 向上查找 Vega.sln。</summary>
    public static string RepoRoot => LazyRepoRoot.Value;

    /// <summary>本 VegaAutoTest 仓库根（向上查找 Vega.AutoTest.sln），落盘目录锚定于此而非主仓库。</summary>
    public static string AutoTestRoot => LazyAutoTestRoot.Value;

    /// <summary>被测 Vega.exe（RID 专属布局）。</summary>
    public static string VegaExe => LazyVegaExe.Value;

    /// <summary>取证文件落盘目录（默认 &lt;VegaAutoTest&gt;\runs\manual，保证对主仓库零写入）。</summary>
    public static string OutDir => LazyOutDir.Value;

    /// <summary>登录账号。未设置时使用超级用户 su，不再回落到测试账号 1。</summary>
    public static string UserName => Env("VEGA_AUTOTEST_USER") ?? "su";

    /// <summary>调用方显式放入的密码。未设置时向官方认证器读取，不再回落到 1。</summary>
    public static string? PasswordOverride => Env("VEGA_AUTOTEST_PASSWORD");

    /// <summary>官方认证器。登录密码只从这里的「超级用户密码」读取。</summary>
    public static string AuthenticatorExe { get; } = @"C:\AutoTest\runtime\AuthenticatorApp\Authenticator.exe";

    /// <summary>L1 AgentControlService 基址（本地部署，缺失时 oracle 弱断言）。</summary>
    public static string AgentBaseUrl => Env("VEGA_AUTOTEST_AGENT_URL") ?? "http://localhost:18901/vega-agent/";

    /// <summary>目标 Job 名称（树内 Contains 匹配；空 = 取第一个 Job 节点）。</summary>
    public static string? JobName => Env("VEGA_AUTOTEST_JOB");

    private static string? Env(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string FindRepoRoot()
    {
        // 显式环境变量优先：独立仓库布局下向上找 Vega.sln 必然失败，由 VEGA_SW_ROOT 指路
        var fromEnv = Env("VEGA_SW_ROOT");
        if (fromEnv is not null)
        {
            return fromEnv;
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Vega.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException(
            $"从 {AppContext.BaseDirectory} 向上未找到含 Vega.sln 的仓库根，且未设 VEGA_SW_ROOT。" +
            "请设置 VEGA_SW_ROOT 指向被测 Vega 主仓库根，或用 VEGA_AUTOTEST_EXE 与 VEGA_AUTOTEST_OUTDIR 直接指定路径。");
    }

    private static string FindAutoTestRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Vega.AutoTest.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException(
            $"从 {AppContext.BaseDirectory} 向上未找到 Vega.AutoTest.sln，无法定位 VegaAutoTest 根。");
    }

    private static string ResolveVegaExe()
    {
        var candidates = new[] { "Debug", "Release" }
            .Select(configuration => Path.Combine(
                RepoRoot, "Vega", "bin", configuration, "net8.0-windows10.0.19041.0", "win-x64", "Vega.exe"))
            .ToArray();

        return candidates.FirstOrDefault(File.Exists) ?? throw new FileNotFoundException(
            "未找到被测 Vega.exe，请先构建主程序，或用 VEGA_AUTOTEST_EXE 指定路径。已尝试：" +
            Environment.NewLine + string.Join(Environment.NewLine, candidates));
    }
}
