// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Reflection;
using System.Text;

namespace Vega.AutoTest.Spike;

/// <summary>
/// 为测试宿主生成服务替身源码。
///
/// RecipePropertyBase 通过 [AutoResolveDependency] 注入五个服务，Galaxy 容器在任一服务
/// 未注册时会 Resolve→CreateInstance 无限递归直到栈溢出，因此必须把它们全部注册。
/// 这些接口合计上百个成员，手写不现实，这里反射生成。
/// </summary>
public class StubGenerator
{
    [Fact]
    public void GenerateStubs()
    {
        var targets = new (string TypeName, string StubName)[]
        {
            ("Galaxy.Platform.INavigations.INavigationService, Galaxy.Platform.INavigations", "StubNavigationService"),
            ("Vega.Utility.Common.IConfigsManager, Vega.Utility.Common", "StubConfigsManager"),
            ("Galaxy.Platform.ICalibrations.ICalibrationService, Galaxy.Platform.ICalibrations", "StubCalibrationService"),
            ("Galaxy.Platform.IAccessControl.IAccessControlService, Galaxy.Platform.IAccessControl", "StubAccessControlService"),
        };

        var sb = new StringBuilder();
        sb.AppendLine("// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.");
        sb.AppendLine();
        sb.AppendLine("// 本文件由 StubGenerator.GenerateStubs 反射生成，请勿手工编辑。");
        sb.AppendLine("#nullable disable");
        sb.AppendLine("#pragma warning disable CS0067 // 替身不触发事件");
        sb.AppendLine();
        sb.AppendLine("namespace Vega.AutoTest.Spike;");
        sb.AppendLine();

        var missing = new List<string>();

        foreach (var (typeName, stubName) in targets)
        {
            var type = Type.GetType(typeName);
            if (type is null)
            {
                missing.Add(typeName);
                continue;
            }

            EmitStub(sb, type, stubName);
        }

        System.IO.File.WriteAllText(
            System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "StubServices.cs"),
            sb.ToString());

        Assert.Empty(missing);
    }

    private static void EmitStub(StringBuilder sb, Type iface, string stubName)
    {
        var allInterfaces = new List<Type> { iface };
        allInterfaces.AddRange(iface.GetInterfaces());

        sb.AppendLine($"public sealed class {stubName} : {CSharpName(iface)}");
        sb.AppendLine("{");

        var emitted = new HashSet<string>();

        foreach (var t in allInterfaces)
        {
            foreach (var p in t.GetProperties())
            {
                var key = $"P:{p.Name}";
                if (!emitted.Add(key))
                {
                    continue;
                }

                var accessors = (p.CanRead ? " get; " : string.Empty) + (p.CanWrite ? "set; " : string.Empty);
                sb.AppendLine($"    public {CSharpName(p.PropertyType)} {p.Name} {{{accessors}}}");
            }

            foreach (var e in t.GetEvents())
            {
                var key = $"E:{e.Name}";
                if (!emitted.Add(key))
                {
                    continue;
                }

                sb.AppendLine($"    public event {CSharpName(e.EventHandlerType!)} {e.Name};");
            }

            foreach (var m in t.GetMethods())
            {
                if (m.IsSpecialName)
                {
                    continue;
                }

                var ps = m.GetParameters();
                var key = $"M:{m.Name}({string.Join(",", ps.Select(x => CSharpName(x.ParameterType)))})";
                if (!emitted.Add(key))
                {
                    continue;
                }

                var generic = m.IsGenericMethodDefinition
                    ? "<" + string.Join(", ", m.GetGenericArguments().Select(a => a.Name)) + ">"
                    : string.Empty;

                var args = string.Join(", ", ps.Select((x, i) =>
                {
                    var mod = x.IsOut ? "out " : x.ParameterType.IsByRef ? "ref " : string.Empty;
                    return $"{mod}{CSharpName(x.ParameterType)} p{i}";
                }));

                sb.AppendLine($"    public {CSharpName(m.ReturnType)} {m.Name}{generic}({args})");
                sb.AppendLine("    {");
                foreach (var (x, i) in ps.Select((x, i) => (x, i)).Where(t2 => t2.x.IsOut))
                {
                    sb.AppendLine($"        p{i} = default;");
                }

                if (m.ReturnType != typeof(void))
                {
                    sb.AppendLine($"        return default;");
                }

                sb.AppendLine("    }");
            }
        }

        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static string CSharpName(Type t)
    {
        if (t.IsByRef)
        {
            return CSharpName(t.GetElementType()!);
        }

        if (t == typeof(void))
        {
            return "void";
        }

        if (t.IsArray)
        {
            return CSharpName(t.GetElementType()!) + "[]";
        }

        if (t.IsGenericParameter)
        {
            return t.Name;
        }

        if (t.IsGenericType)
        {
            var def = t.GetGenericTypeDefinition().FullName!;
            var bare = def[..def.IndexOf('`')].Replace('+', '.');
            var args = string.Join(", ", t.GetGenericArguments().Select(CSharpName));
            return $"global::{bare}<{args}>";
        }

        return "global::" + (t.FullName ?? t.Name).Replace('+', '.');
    }
}
