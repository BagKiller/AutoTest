using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Vega.Utility.Helper;
using Xunit;

namespace Vega.AutoTest.Authored;

public sealed class DataDependencyCompatibilityTests
{
    [Fact]
    public void ProductFileApisPreservePolymorphismAndRejectMissingTypeInformation()
    {
        DependencyEvidence.Run(r =>
        {
            string directory = Path.Combine(Path.GetTempPath(), "VegaDependency-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                foreach (bool streaming in new[] { false, true })
                {
                    string prefix = streaming ? "streaming" : "file";
                    string path = Path.Combine(directory, prefix + ".json");
                    var input = new Envelope
                    {
                        Item = new DetailedPayload { Name = "晶圆 Ω", Count = 7 },
                        Values = new List<int> { 2, 5, 8 }
                    };
                    if (streaming) JsonHelper.SerializeLargeObject(input, path);
                    else JsonHelper.Serialize(input, path);
                    int callbacks = 0;
                    Envelope? callbackValue = null;
                    Action<Envelope> save = value => { callbacks++; callbackValue = value; };
                    Envelope? output = streaming
                        ? JsonHelper.DeserializeLargeObject<Envelope>(path, save, true)
                        : JsonHelper.Deserialize<Envelope>(path, save, true);
                    DependencyEvidence.Verify(r, prefix + ".type", nameof(DetailedPayload),
                        output?.Item?.GetType().Name ?? "null",
                        () => Assert.IsType<DetailedPayload>(output?.Item));
                    var detail = (DetailedPayload)output!.Item;
                    DependencyEvidence.Verify(r, prefix + ".unicode", "晶圆 Ω", detail.Name,
                        () => Assert.Equal("晶圆 Ω", detail.Name));
                    DependencyEvidence.Verify(r, prefix + ".count", "7", detail.Count.ToString(),
                        () => Assert.Equal(7, detail.Count));
                    DependencyEvidence.Verify(r, prefix + ".replacement", "2,5,8",
                        string.Join(",", output.Values),
                        () => Assert.Equal(new[] { 2, 5, 8 }, output.Values));
                    DependencyEvidence.Verify(r, prefix + ".callback-count", "1", callbacks.ToString(),
                        () => Assert.Equal(1, callbacks));
                    DependencyEvidence.Verify(r, prefix + ".callback-identity", "Returned instance",
                        ReferenceEquals(output, callbackValue).ToString(),
                        () => Assert.Same(output, callbackValue));
                    JsonHelper.Serialize(input, path, VegaJsonSettings.Default);
                    var rejection = Record.Exception(() =>
                    {
                        if (streaming) JsonHelper.DeserializeLargeObject<Envelope>(path, save, true);
                        else JsonHelper.Deserialize<Envelope>(path, save, true);
                    });
                    DependencyEvidence.Verify(r, prefix + ".missing-type", nameof(JsonSerializationException),
                        rejection?.GetType().Name ?? "No exception",
                        () => Assert.IsType<JsonSerializationException>(rejection));
                    DependencyEvidence.Verify(r, prefix + ".rejected-callback", "1", callbacks.ToString(),
                        () => Assert.Equal(1, callbacks));
                }
            }
            finally { Directory.Delete(directory, true); }
        });
    }

    public class Payload
    {
        public string Name { get; set; } = "";
    }

    public sealed class DetailedPayload : Payload
    {
        public int Count { get; set; }
    }

    public sealed class Envelope
    {
        public Payload Item { get; set; } = new Payload();
        public List<int> Values { get; set; } = new List<int> { 99 };
    }
}
