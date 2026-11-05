using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Vega.AutoTest.Harness;
using Vega.Data.Measurements;
using Xunit;

namespace Vega.AutoTest.Authored;

[Collection("LPD contracts")]
public class LpdLegacyDocumentRegressionTests
{
    [Fact]
    public void LegacySnapshotPayloadIsIgnoredWithoutLosingFollowingWaferFields()
    {
        var a = new AssertionRecorder();
        try
        {
            var map = new BsonClassMap<WaferRunInfo>();
            map.AutoMap();
            // Preserve product BSON mappings; avoid the constructor's unrelated machine-dependent Recipe initializer.
            map.SetCreator(() => LpdTest.Empty<WaferRunInfo>());
            map.Freeze();
            var serializer = new BsonClassMapSerializer<WaferRunInfo>(map);
            for (int variant = 0; variant < 3; variant++)
            {
                var document = new BsonDocument { { "_id", 3757L }, { "WaferID", "W3757" } };
                if (variant == 1) document.Add("CalibrationCurveSnapshot", BsonNull.Value);
                if (variant == 2) document.Add("CalibrationCurveSnapshot", new BsonDocument
                {
                    { "FilmCurves", new BsonArray { new BsonDocument
                        { { "Key", "legacy" }, { "Points", new BsonArray { new BsonDocument { { "X", 1 }, { "Y", 2 } } } } } } },
                    { "NormalCurves", new BsonArray() }
                });
                document.Add("LotID", "L3757");
                document.Add("EquipmentID", "E3757");
                using var reader = new BsonDocumentReader(document);
                var run = serializer.Deserialize(BsonDeserializationContext.CreateRoot(reader),
                    new BsonDeserializationArgs { NominalType = typeof(WaferRunInfo) });
                LpdTest.Equal(a, "legacy wafer identity", 3757L, run.ID);
                LpdTest.Equal(a, "legacy wafer label", "W3757", run.WaferID);
                LpdTest.Equal(a, "field after retired payload", "L3757", run.LotID);
                LpdTest.Equal(a, "equipment after retired payload", "E3757", run.EquipmentID);
            }
        }
        finally { a.Flush(); }
    }
}
