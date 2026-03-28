#nullable enable

namespace FogMod.Models;

public class Vec2
{
    public float X { get; set; }
    public float Y { get; set; }
}

public class DownUpOffsets
{
    public Vec2 GunOffset { get; set; } = new();
    public Vec2 GunPivot { get; set; } = new();
}

public class SideOffsets
{
    public Vec2 SpecialOffset { get; set; } = new();
    public Vec2 GunOriginOffset { get; set; } = new();
}

public class ShotgunOffsets
{
    public float GunScale { get; set; } = 4f;
    public float ArmScale { get; set; } = 4f;
    public int SmokeCount { get; set; } = 24;
    public float SmokeRadius { get; set; } = 24f;
    public int ExplosionPitch { get; set; } = 0;
    public DownUpOffsets Down { get; set; } = new();
    public SideOffsets Right { get; set; } = new();
    public SideOffsets Left { get; set; } = new();
    public DownUpOffsets Up { get; set; } = new();
}
