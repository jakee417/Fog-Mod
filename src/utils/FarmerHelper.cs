#nullable enable
using StardewValley;

namespace FogMod;

public static class FarmerHelper
{
    public static void raiseHands(Farmer who)
    {
        who.completelyStopAnimatingOrDoingAction();
        who.faceDirection(2);
        who.freezePause = 2500;

        FarmerSprite.AnimationFrame[] frames = new FarmerSprite.AnimationFrame[3]
        {
                new FarmerSprite.AnimationFrame(57, 0),
                new FarmerSprite.AnimationFrame(57, 2000, secondaryArm: false, flip: false, delegate(Farmer whom) {}),
                new FarmerSprite.AnimationFrame((short)who.FarmerSprite.CurrentFrame, 500, secondaryArm: false, flip: false)
        };
        who.FarmerSprite.animateOnce(frames);
        who.canMove = false;
    }
}