#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text.Json;
using System.Windows;
using Vega.AutoTest.Harness;
using Vega.Data.Jobs;
using Vega.Data.Measurements;
using Vega.Shared;
using Vega.UI.Jobs;
using Xunit;

namespace Vega.AutoTest.Authored;

internal static class NamingOracle
{
    internal static void Is(AssertionRecorder r, string id, bool condition, string expected, string actual)
    {
        if (!condition)
        {
            r.Fail(id, "behavior", id, expected, actual);
            throw new Xunit.Sdk.XunitException(id + ": expected " + expected + "; actual " + actual);
        }
        r.Pass(id, "behavior", id, expected, actual);
    }

    internal static void Equal<T>(AssertionRecorder r, string id, T expected, T actual) =>
        Is(r, id, EqualityComparer<T>.Default.Equals(expected, actual),
            JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));

    internal static void Stamp(AssertionRecorder r, string id, string name, string prefix, DateTime before, DateTime after)
    {
        var lower = new DateTime(before.Ticks - before.Ticks % TimeSpan.TicksPerMillisecond);
        bool valid = name.StartsWith(prefix, StringComparison.Ordinal) && name.Length == prefix.Length + 17 &&
            DateTime.TryParseExact(name.Substring(prefix.Length), "yyyyMMddHHmmssfff", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var time) && time >= lower && time <= after;
        Is(r, id, valid, prefix + " + 17-digit local runtime timestamp within call interval", name);
    }
}

public class OfflineNamingRulesRegression
{
    [Fact]
    public void NormalizationCompositionAutomaticNamesAndResourcesFollowContract()
    {
        var r = new AssertionRecorder();
        try
        {
            NamingOracle.Equal(r, "maximum-custom-length", 100, OfflineJobNameRules.MaxLength);
            (string? Input, string? Expected)[] rows =
            {
                (null, null), ("", null), (" \t\r\n ", null),
                ("  Alpha 中_01-ß  ", "Alpha 中_01-ß"), ("A B", "A B"),
                (new string('A', 100), new string('A', 100)),
                (new string('B', 101), new string('B', 100)),
                ("a/b\\c:d*e?f\"g<h>i|j", "abcdefghij"),
                ("/\\:*?\"<>|", null), ("\0", null), ("A\0B", "AB"), ("  A/ B  ", "A B")
            };
            for (int i = 0; i < rows.Length; i++)
                NamingOracle.Equal(r, "normalize-" + i, rows[i].Expected, OfflineJobNameRules.Normalize(rows[i].Input));

            string[] validNames = { "", "Job-001", "批次_01", "Alpha Beta", new string('A', 100) };
            for (int i = 0; i < validNames.Length; i++)
                NamingOracle.Equal(r, "valid-characters-" + i, false, OfflineJobNameRules.ContainsInvalidChars(validNames[i]));

            var forbidden = "\\/:*?\"<>|".Concat(Enumerable.Range(0, 32).Select(i => (char)i)).ToArray();
            for (int i = 0; i < forbidden.Length; i++)
            {
                string input = "A" + forbidden[i] + "B";
                NamingOracle.Equal(r, "detect-forbidden-" + i, true, OfflineJobNameRules.ContainsInvalidChars(input));
                NamingOracle.Equal(r, "remove-forbidden-" + i, "AB", OfflineJobNameRules.Normalize(input));
            }
            string?[] emptyNames = { null, "", " \t", "/:*?", "\0" };
            for (int i = 0; i < emptyNames.Length; i++)
                NamingOracle.Equal(r, "fallback-" + i, "Auto_20261030143000123",
                    OfflineJobNameRules.Compose(emptyNames[i], "Auto_20261030143000123"));

            (string Input, string Prefix)[] customRows =
            {
                ("TestRun01", "TestRun01"), (" My/Name ", "MyName"),
                (new string('X', 101), new string('X', 100)), ("A\0B", "AB")
            };
            for (int i = 0; i < customRows.Length; i++)
            {
                var before = DateTime.Now;
                string name = OfflineJobNameRules.Compose(customRows[i].Input, "DO_NOT_USE");
                var after = DateTime.Now;
                NamingOracle.Stamp(r, "compose-runtime-" + i, name, customRows[i].Prefix + "_", before, after);
                NamingOracle.Equal(r, "composed-length-" + i, customRows[i].Prefix.Length + 18, name.Length);
            }
            (Func<string> Generate, string Prefix)[] generators =
            {
                (OfflineJobNameRules.AutoRunCurrentRecipeJobName, "RunCurRecipeOfflineJob_"),
                (OfflineJobNameRules.AutoWorkSpaceJobName, "WorkSpaceOfflineJob_"),
                (() => OfflineJobNameRules.AutoJobQueueJobName("Model_A"), "Model_A_Offline_")
            };
            for (int i = 0; i < generators.Length; i++)
            {
                var before = DateTime.Now;
                string name = generators[i].Generate();
                NamingOracle.Stamp(r, "automatic-name-" + i, name, generators[i].Prefix, before, DateTime.Now);
            }

            var resourceType = Assembly.Load("Vega.Utility.Common").GetType("Vega.Utility.Common.Properties.Resources", true)!;
            var manager = (ResourceManager)resourceType.GetProperty("ResourceManager",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!;
            (string Key, string English, string Chinese)[] translations =
            {
                ("OfflineJobName", "JobName:", "Job 名称:"),
                ("OfflineJobNameInvalidChars", "Job name contains characters not allowed in file names (\\ / : * ? \" < > |).",
                    "Job 名称含文件名非法字符（\\ / : * ? \" < > |）。"),
                ("OfflineJobNameTooLong", "Job name exceeds {0} characters.", "Job 名称超过 {0} 个字符。")
            };
            foreach (var row in translations)
            {
                NamingOracle.Equal(r, row.Key + "-en", row.English, manager.GetString(row.Key, CultureInfo.GetCultureInfo("en-US")));
                NamingOracle.Equal(r, row.Key + "-zh", row.Chinese, manager.GetString(row.Key, CultureInfo.GetCultureInfo("zh-Hans")));
            }
        }
        finally { r.Flush(); }
    }
}

public class OfflineNamingDialogRegression
{
    [Fact]
    public void RecipeOptionsCommitAfterValidationAndWorkspaceCancellationPreservesInputContract()
    {
        var r = new AssertionRecorder();
        try
        {
            var type = Assembly.Load("Vega.UI.Inspection").GetType("Vega.UI.Inspection.OfflineParamsDialogViewModel", true)!;
            var ctor = type.GetConstructors().Where(c => c.GetParameters().Any(p =>
                p.ParameterType == typeof(OfflineRecipeRunOptions))).OrderBy(c => c.GetParameters().Length).First();
            string?[] edits = { null, "", " \t ", "  TestRun01  ", new string('A', 100), "RunCurRecipeOfflineJob_20000101000000000" };
            for (int i = 0; i < edits.Length; i++)
            {
                var options = new OfflineRecipeRunOptions { LoopCount = 7, CustomJobName = "previous", PortNum = 1, SlotID = 2 };
                var paths = new List<OfflineCollectionPath>();
                var args = ctor.GetParameters().Select(p =>
                    p.ParameterType == typeof(OfflineRecipeRunOptions) ? (object)options :
                    p.ParameterType.IsInstanceOfType(paths) ? paths :
                    p.HasDefaultValue ? p.DefaultValue :
                    p.ParameterType == typeof(bool) ? true :
                    throw new InvalidOperationException("Unsupported constructor parameter: " + p)).ToArray();
                var before = DateTime.Now;
                object vm = ctor.Invoke(args);
                string seed = (string)type.GetProperty("JobName")!.GetValue(vm)!;
                NamingOracle.Stamp(r, "recipe-seed-" + i, seed, "RunCurRecipeOfflineJob_", before, DateTime.Now);
                NamingOracle.Equal(r, "recipe-name-visible-" + i, Visibility.Visible,
                    (Visibility)type.GetProperty("JobNameVisibility")!.GetValue(vm)!);
                type.GetProperty("JobName")!.SetValue(vm, edits[i] ?? seed);
                type.GetProperty("LoopCountText")!.SetValue(vm, "2");
                bool accepted = (bool)type.GetMethod("TrySyncDialogOptionsBack", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(vm, null)!;
                NamingOracle.Equal(r, "recipe-options-accepted-" + i, true, accepted);
                string? expected = string.IsNullOrWhiteSpace(edits[i]) ? null : edits[i]!.Trim();
                NamingOracle.Equal(r, "recipe-custom-writeback-" + i, expected, options.CustomJobName);
                NamingOracle.Equal(r, "recipe-loop-writeback-" + i, 2, options.LoopCount);
            }

            string[] seeds = { "WorkSpaceOfflineJob_20000102030405006", "", new string('W', 100) };
            for (int i = 0; i < seeds.Length; i++)
            {
                var config = new OfflineConfigInfo { JobName = seeds[i] };
                var vm = new OfflineConfigDialogViewModel(config);
                NamingOracle.Equal(r, "workspace-prefill-" + i, seeds[i], vm.JobName);
                vm.JobName = "EditedButCancelled";
                vm.CancelButtonClick();
                NamingOracle.Equal(r, "workspace-cancel-" + i, seeds[i], config.JobName);
                vm.CloseButtonClick();
                NamingOracle.Equal(r, "workspace-close-" + i, seeds[i], config.JobName);
            }

            var job = (Job)Activator.CreateInstance(typeof(Job))!;
            var parameters = new Dictionary<long, List<OfflineRunParams>>();
            var messages = new[]
            {
                new WorkSpaceJobOfflineStartMessage(job, parameters),
                new WorkSpaceJobOfflineStartMessage(job, parameters, "TestRun01")
            };
            for (int i = 0; i < messages.Length; i++)
            {
                NamingOracle.Equal(r, "message-custom-name-" + i, i == 0 ? null : "TestRun01", messages[i].CustomJobName);
                NamingOracle.Is(r, "message-parameters-" + i, ReferenceEquals(parameters, messages[i].offlineParamsByLot),
                    "original offline parameter dictionary", "reference preserved=" + ReferenceEquals(parameters, messages[i].offlineParamsByLot));
            }
        }
        finally { r.Flush(); }
    }
}
