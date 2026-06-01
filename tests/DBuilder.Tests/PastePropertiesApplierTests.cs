// ABOUTME: Tests selective UDB-style paste-properties application to map elements.
// ABOUTME: Verifies copied properties do not replace geometry links or editor selection state.

using DBuilder.Geometry;
using DBuilder.Map;

namespace DBuilder.Tests;

public class PastePropertiesApplierTests
{
    [Fact]
    public void ApplyVertexCopiesSelectedPropertiesWithoutMovingTarget()
    {
        Vertex source = new(new Vector2D(10, 20))
        {
            ZFloor = 16,
            ZCeiling = 128,
            Selected = true,
        };
        source.Fields["comment"] = "source";
        Vertex target = new(new Vector2D(40, 50))
        {
            ZFloor = 0,
            ZCeiling = 64,
            Selected = false,
        };
        target.Fields["old"] = 1;

        PastePropertiesApplier.Apply(
            source,
            target,
            Keys(PastePropertiesKeys.VertexZFloor, PastePropertiesKeys.VertexFields));

        Assert.Equal(new Vector2D(40, 50), target.Position);
        Assert.Equal(16, target.ZFloor);
        Assert.Equal(64, target.ZCeiling);
        Assert.False(target.Selected);
        Assert.Equal("source", target.Fields["comment"]);
        Assert.False(target.Fields.ContainsKey("old"));
    }

    [Fact]
    public void ApplyLinedefCopiesLineAndMatchingSidePropertiesWithoutReplacingGeometry()
    {
        Linedef source = CreateLineWithSides(0, 0, 64, 0);
        source.Action = 80;
        source.Activate = 3;
        source.Tags.AddRange([9, 11]);
        source.Args[0] = 77;
        source.UdmfFlags.Add("blocking");
        source.Front!.HighTexture = "STONE2";
        source.Front.OffsetX = 24;
        source.Back!.LowTexture = "BROWN1";
        source.Back.OffsetY = 32;

        Linedef target = CreateLineWithSides(10, 10, 10, 80);
        Vertex originalStart = target.Start;
        Vertex originalEnd = target.End;
        target.Action = 1;
        target.Activate = 0;
        target.Tags.Add(1);
        target.Args[0] = 2;
        target.Front!.HighTexture = "STARTAN";
        target.Front.OffsetX = 1;
        target.Back!.LowTexture = "ASHWALL";
        target.Back.OffsetY = 2;

        PastePropertiesApplier.Apply(
            source,
            target,
            Keys(
                PastePropertiesKeys.LinedefAction,
                PastePropertiesKeys.LinedefArguments,
                PastePropertiesKeys.LinedefActivation,
                PastePropertiesKeys.LinedefTag,
                PastePropertiesKeys.LinedefFlags,
                PastePropertiesKeys.SidedefUpperTexture,
                PastePropertiesKeys.SidedefOffsetX,
                PastePropertiesKeys.SidedefLowerTexture,
                PastePropertiesKeys.SidedefOffsetY));

        Assert.Same(originalStart, target.Start);
        Assert.Same(originalEnd, target.End);
        Assert.Equal(80, target.Action);
        Assert.Equal(3, target.Activate);
        Assert.Equal([9, 11], target.Tags);
        Assert.Equal(77, target.Args[0]);
        Assert.Contains("blocking", target.UdmfFlags);
        Assert.Equal("STONE2", target.Front.HighTexture);
        Assert.Equal(24, target.Front.OffsetX);
        Assert.Equal("BROWN1", target.Back.LowTexture);
        Assert.Equal(32, target.Back.OffsetY);
    }

    [Fact]
    public void ApplyLinedefArgumentsCopiesArg0StringWithoutCustomFields()
    {
        Linedef source = CreateLineWithSides(0, 0, 64, 0);
        source.Args[0] = 5;
        source.Fields["arg0str"] = "DoorScript";
        source.Fields["comment"] = "do not copy";
        Linedef target = CreateLineWithSides(0, 0, 0, 64);
        target.Fields["comment"] = "keep";

        PastePropertiesApplier.Apply(source, target, Keys(PastePropertiesKeys.LinedefArguments));

        Assert.Equal(5, target.Args[0]);
        Assert.Equal("DoorScript", target.Fields["arg0str"]);
        Assert.Equal("keep", target.Fields["comment"]);
    }

    [Fact]
    public void ApplySectorCopiesSelectedBuiltInFieldsAndLeavesOthers()
    {
        Sector source = new()
        {
            FloorHeight = 8,
            CeilHeight = 160,
            FloorTexture = "FLOOR0_1",
            CeilTexture = "CEIL1_1",
            Brightness = 192,
            Special = 7,
        };
        source.Tags.Add(12);
        source.UdmfFlags.Add("hidden");
        source.Fields["comment"] = "sector";
        Sector target = new()
        {
            FloorHeight = 0,
            CeilHeight = 128,
            FloorTexture = "OLD",
            CeilTexture = "OLDCEIL",
            Brightness = 96,
            Special = 1,
        };
        target.Tags.Add(1);

        PastePropertiesApplier.Apply(
            source,
            target,
            Keys(
                PastePropertiesKeys.SectorFloorHeight,
                PastePropertiesKeys.SectorCeilingTexture,
                PastePropertiesKeys.SectorTag,
                PastePropertiesKeys.SectorFields));

        Assert.Equal(8, target.FloorHeight);
        Assert.Equal(128, target.CeilHeight);
        Assert.Equal("OLD", target.FloorTexture);
        Assert.Equal("CEIL1_1", target.CeilTexture);
        Assert.Equal(96, target.Brightness);
        Assert.Equal(1, target.Special);
        Assert.Equal([12], target.Tags);
        Assert.DoesNotContain("hidden", target.UdmfFlags);
        Assert.Equal("sector", target.Fields["comment"]);
    }

    [Fact]
    public void ApplyThingCopiesSelectedPropertiesWithoutMovingTarget()
    {
        Thing source = new(new Vector2D(10, 20), 3004, 90)
        {
            Height = 24,
            Pitch = 10,
            Roll = 20,
            ScaleX = 1.5,
            ScaleY = 2.0,
            Action = 226,
            Tag = 7,
        };
        source.Args[0] = 1;
        source.Fields["arg0str"] = "Wake";
        source.Fields["comment"] = "thing";
        Thing target = new(new Vector2D(100, 200), 1, 0)
        {
            Height = 0,
            Pitch = 0,
            Roll = 0,
            ScaleX = 1.0,
            ScaleY = 1.0,
            Action = 0,
            Tag = 0,
        };
        target.Fields["comment"] = "keep";

        PastePropertiesApplier.Apply(
            source,
            target,
            Keys(
                PastePropertiesKeys.ThingType,
                PastePropertiesKeys.ThingAngle,
                PastePropertiesKeys.ThingScale,
                PastePropertiesKeys.ThingArguments));

        Assert.Equal(new Vector2D(100, 200), target.Position);
        Assert.Equal(3004, target.Type);
        Assert.Equal(90, target.Angle);
        Assert.Equal(0, target.Height);
        Assert.Equal(0, target.Pitch);
        Assert.Equal(0, target.Roll);
        Assert.Equal(1.5, target.ScaleX);
        Assert.Equal(2.0, target.ScaleY);
        Assert.Equal(0, target.Action);
        Assert.Equal(0, target.Tag);
        Assert.Equal(1, target.Args[0]);
        Assert.Equal("Wake", target.Fields["arg0str"]);
        Assert.Equal("keep", target.Fields["comment"]);
    }

    [Fact]
    public void EnabledKeysReturnsCheckedOptionsFromResult()
    {
        PastePropertiesOption action = new(PastePropertiesKeys.LinedefAction, "Action", true);
        PastePropertiesOption flags = new(PastePropertiesKeys.LinedefFlags, "Flags", false);
        PastePropertiesOptionsResult result = new(
            true,
            null,
            [
                new PastePropertiesOptionsTab(
                    PastePropertiesElementKind.Linedef,
                    "Linedefs",
                    [action, flags]),
            ]);

        ISet<string> keys = PastePropertiesApplier.EnabledKeys(result);

        Assert.Contains(PastePropertiesKeys.LinedefAction, keys);
        Assert.DoesNotContain(PastePropertiesKeys.LinedefFlags, keys);
    }

    private static ISet<string> Keys(params string[] keys)
        => keys.ToHashSet(StringComparer.Ordinal);

    private static Linedef CreateLineWithSides(double x1, double y1, double x2, double y2)
    {
        Linedef line = new(new Vertex(new Vector2D(x1, y1)), new Vertex(new Vector2D(x2, y2)));
        line.AttachFront(new Sidedef(line, isFront: true));
        line.AttachBack(new Sidedef(line, isFront: false));
        return line;
    }
}
