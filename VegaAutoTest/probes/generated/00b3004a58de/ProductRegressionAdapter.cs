
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
