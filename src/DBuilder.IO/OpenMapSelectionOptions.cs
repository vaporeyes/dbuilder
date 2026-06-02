// ABOUTME: Carries UDB-style options chosen while opening or switching a map.
// ABOUTME: Applies only options supported by the active game configuration.

namespace DBuilder.IO;

public readonly record struct OpenMapSelectionOptions(bool LongTextureNamesSupported, bool UseLongTextureNames)
{
    public static OpenMapSelectionOptions FromMapOptions(MapOptions? options, bool longTextureNamesSupported)
        => new(longTextureNamesSupported, longTextureNamesSupported && options?.UseLongTextureNames == true);

    public OpenMapSelectionOptions WithUseLongTextureNames(bool enabled)
        => new(LongTextureNamesSupported, LongTextureNamesSupported && enabled);

    public void ApplyTo(MapOptions options)
    {
        options.UseLongTextureNames = LongTextureNamesSupported && UseLongTextureNames;
    }
}
