#nullable enable
using Microsoft.Xna.Framework;

namespace FogMod.Models;

public struct CritterzBushFxInfo
{
    public string LocationName { get; init; }
    public Vector2 BushTile { get; init; }

    public CritterzBushFxInfo(string locationName, Vector2 bushTile)
    {
        LocationName = locationName;
        BushTile = bushTile;
    }
}
