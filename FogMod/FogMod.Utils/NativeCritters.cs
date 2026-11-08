#nullable enable
using System;
using System.Collections.Generic;
using StardewValley;

namespace FogMod.Utils;

public static class NativeCritters
{
    // Full type names of the native critters suppressed while Critterz are
    // on. Full names (not short names) so another mod's own Rabbit/Squirrel
    // types keep working.
    private static readonly HashSet<string> SuppressedTypeNames = new()
    {
        "StardewValley.BellsAndWhistles.Rabbit",
        "StardewValley.BellsAndWhistles.Squirrel",
    };

    public static bool ShouldSuppress(Type? type) =>
        type?.FullName is string fullName && SuppressedTypeNames.Contains(fullName);

    // Remove every suppressed critter currently in the location: anything
    // added before Critterz were enabled, or through a path that bypasses
    // GameLocation.addCritter. Returns the number removed.
    public static int SweepSuppressedCritters(GameLocation? location)
    {
        if (location?.critters == null)
            return 0;
        return location.critters.RemoveAll(c => ShouldSuppress(c.GetType()));
    }
}
