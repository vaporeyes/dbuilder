// ABOUTME: Carries UDB-style options chosen while opening or switching a map.
// ABOUTME: Applies only options supported by the active game configuration.

namespace DBuilder.IO;

public readonly record struct OpenMapSelectionOptions(bool StrictPatches, bool LongTextureNamesSupported, bool UseLongTextureNames)
{
    public static OpenMapSelectionOptions FromMapOptions(MapOptions? options, bool longTextureNamesSupported)
        => new(options?.StrictPatches == true, longTextureNamesSupported, longTextureNamesSupported && options?.UseLongTextureNames == true);

    public OpenMapSelectionOptions WithUseLongTextureNames(bool enabled)
        => new(StrictPatches, LongTextureNamesSupported, LongTextureNamesSupported && enabled);

    public OpenMapSelectionOptions WithStrictPatches(bool enabled)
        => new(enabled, LongTextureNamesSupported, UseLongTextureNames);

    public void ApplyTo(MapOptions options)
    {
        options.StrictPatches = StrictPatches;
        options.UseLongTextureNames = LongTextureNamesSupported && UseLongTextureNames;
    }
}
