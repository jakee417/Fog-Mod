#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using System;
using FogMod.Models;
using FogMod.Utils;

namespace FogMod;

public partial class FogMod : Mod
{
    private void DrawFloatingFogParticles(SpriteBatch spriteBatch, Color fogColor)
    {
        Vector2 playerWorldCenter = Utilities.GetPlayerWorldCenter();
        foreach (var p in floatingParticles)
        {
            Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, p.Position);
            Color color = ComposeParticleColor(p, fogColor, playerWorldCenter);
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

    private void DrawExplosionSmokeParticles(SpriteBatch spriteBatch, Color fogColor)
    {
        Vector2 playerWorldCenter = Utilities.GetPlayerWorldCenter();
        foreach (var p in explosionSmokeParticles)
        {
            Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, p.Position);
            Color color = ComposeSmokeColor(p, fogColor, playerWorldCenter);
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

    private void DrawExplosionFlashes(SpriteBatch spriteBatch)
    {
        if (explosionFlashInfos == null || explosionFlashInfos.Count == 0) return;
        if (whitePixel == null) return;

        for (int i = 0; i < explosionFlashInfos.Count; i++)
        {
            var flashInfo = explosionFlashInfos[i];
            DrawExplosionFlash(spriteBatch, flashInfo);
        }
    }

    private void DrawExplosionFlash(SpriteBatch spriteBatch, ExplosionFlashInfo flashInfo)
    {
        Point location = new Point((int)flashInfo.CenterWorld.X, (int)flashInfo.CenterWorld.Y);
        if (flashInfo.TimeLeft <= 0f || !grid.GetExtendedBounds().Contains(location))
            return;
        float baseAlpha = MathHelper.Clamp(flashInfo.TimeLeft / Constants.ExplosionFlashDurationSeconds * (Game1.currentLocation.IsOutdoors ? 0.2f : 0.4f), 0f, 1f);
        Color flashColor = new Color(255, 160, 60) * baseAlpha;
        var vp = Game1.graphics.GraphicsDevice.Viewport;
        Rectangle full = new Rectangle(0, 0, vp.Width, vp.Height);
        spriteBatch.Draw(whitePixel, full, flashColor * 1.0f);
        spriteBatch.Draw(whitePixel, full, flashColor * 0.55f * flashInfo.TimeLeft);
        spriteBatch.Draw(whitePixel, full, flashColor * 0.25f * flashInfo.TimeLeft);
    }
}