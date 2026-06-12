// ABOUTME: Resolves UDB-style ambient sound editor radii for map things.
// ABOUTME: Keeps sound-radius lookup testable outside the Avalonia renderer.

using DBuilder.Map;

namespace DBuilder.IO;

public sealed record AmbientSoundRadii(double? MinimumRadius, double? MaximumRadius);

public static class SoundRadiusDisplay
{
    private const string AmbientSoundClass = "AmbientSound";
    private const string AmbientSoundNoGravityClass = "AmbientSoundNoGravity";
    private const string AmbientSoundPrefix = "$AmbientSound";

    public static AmbientSoundRadii? ThingRadii(Thing thing, ThingTypeInfo? info, SndInfo sndInfo, bool doomMap)
    {
        if (info == null) return null;
        if (!TryAmbientSoundIndex(thing, info, doomMap, out int soundIndex)) return null;
        if (soundIndex == 0 || !sndInfo.AmbientSounds.TryGetValue(soundIndex, out AmbientSoundInfo? ambient)) return null;

        if (thing.Args[2] > 0 && thing.Args[3] > 0 && thing.Args[3] > thing.Args[2])
        {
            double scalar = thing.Args[4] != 0 ? thing.Args[4] : 1.0;
            return new AmbientSoundRadii(thing.Args[2] * scalar, thing.Args[3] * scalar);
        }

        return new AmbientSoundRadii(
            ambient.MinimumRadius > 0.0 ? ambient.MinimumRadius : null,
            ambient.MaximumRadius > 0.0 ? ambient.MaximumRadius : null);
    }

    private static bool TryAmbientSoundIndex(Thing thing, ThingTypeInfo info, bool doomMap, out int soundIndex)
    {
        soundIndex = 0;
        string className = info.ClassName;
        if (string.IsNullOrWhiteSpace(className)) return false;

        if (TryIndexFromClassName(className, out soundIndex)) return true;
        if (doomMap) return false;
        if (!className.Equals(AmbientSoundClass, StringComparison.OrdinalIgnoreCase)
            && !className.Equals(AmbientSoundNoGravityClass, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        soundIndex = thing.Args[0];
        return true;
    }

    private static bool TryIndexFromClassName(string className, out int soundIndex)
    {
        soundIndex = 0;
        if (!className.StartsWith(AmbientSoundPrefix, StringComparison.OrdinalIgnoreCase)) return false;
        ReadOnlySpan<char> suffix = className.AsSpan(AmbientSoundPrefix.Length);
        return suffix.Length > 0 && int.TryParse(suffix, out soundIndex);
    }
}
