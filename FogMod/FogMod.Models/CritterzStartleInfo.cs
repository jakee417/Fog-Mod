#nullable enable

namespace FogMod.Models;

public struct CritterzStartleInfo
{
    public int CritterzId { get; init; }

    public CritterzStartleInfo(int critterzId)
    {
        CritterzId = critterzId;
    }
}
