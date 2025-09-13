#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using System;

namespace FogMod;

public partial class FogMod : Mod
{
    public Texture2D? whitePixel { get; set; }

    private void DrawFloatingFogParticles(SpriteBatch spriteBatch, Color fogColor)
    {
        foreach (var p in floatingParticles)
        {
            Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, p.Position);
            Color color = ComposeParticleColor(p, fogColor);
            if (p.Texture is Texture2D tex)
            {
                Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
                float scale = p.Scale * Constants.FogCloudScale;
                spriteBatch.Draw(
                    tex,
                    position: screenPos,
                    sourceRectangle: null,
                    color: color,
                    rotation: 0f,
                    origin: origin,
                    scale: scale,
                    effects: SpriteEffects.None,
                    layerDepth: 0.8f
                );
            }
        }
    }

    private void DrawFogBank(SpriteBatch spriteBatch, Color fogColor)
    {
        int i = 0;
        Vector2 v = default(Vector2);
        float timeOfDay = Config.EnableTimeOfDayFog ? ComputeTimeOfDayOpacityMultiplier() : 1.0f;
        float dailyMult = GetDailyFogAlphaMultiplier();
        float a = Constants.DefaultFogAlpha * 2.5f * timeOfDay * dailyMult;
        a = MathHelper.Clamp(a, 0f, 1f);
        for (float x = -256 + (int)(fogPos.X % 256f); x < (float)Game1.graphics.GraphicsDevice.Viewport.Width; x += 256f)
        {
            for (float y = -256 + (int)(fogPos.Y % 256f); y < (float)Game1.graphics.GraphicsDevice.Viewport.Height; y += 256f)
            {
                v.X = (int)x;
                v.Y = (int)y;
                spriteBatch.Draw(Game1.mouseCursors, v, fogSource, (a > 0f) ? (fogColor * a) : fogColor, 0f, Vector2.Zero, 4.001f, SpriteEffects.None, 1f);
                i++;
            }
        }
        numFogBankChunks = i;
    }

    private void DrawExplosionSmokeParticles(SpriteBatch spriteBatch, Color fogColor)
    {
        foreach (var p in explosionSmokeParticles)
        {
            Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, p.Position);
            Color color = ComposeSmokeColor(p, fogColor);
            if (p.Texture is Texture2D tex)
            {
                Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
                float lifeT = MathHelper.Clamp(p.AgeSeconds / Math.Max(0.001f, Constants.SmokeGrowthSeconds), 0f, 1f);
                float growth = 1f + 1.0f * lifeT;
                float scale = p.Scale * Constants.FogCloudScale * growth;
                spriteBatch.Draw(
                    tex,
                    position: screenPos,
                    sourceRectangle: null,
                    color: color,
                    rotation: 0f,
                    origin: origin,
                    scale: scale,
                    effects: SpriteEffects.None,
                    layerDepth: 0.81f
                );
            }
        }
    }
}