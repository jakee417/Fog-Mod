#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using System;

namespace FogMod
{
    public partial class FogMod : Mod
    {
        private void DrawFloatingFogParticles(SpriteBatch spriteBatch, Color fogColor)
        {
            Vector2 playerWorldCenter = GetPlayerWorldCenter();
            foreach (var p in floatingParticles)
            {
                Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, p.Position);
                Color color = ComposeParticleColor(p, fogColor, playerWorldCenter);
                if (p.Texture is Texture2D tex)
                {
                    Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
                    float scale = p.Scale * FogCloudScale;
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
            Vector2 playerWorldCenter = GetPlayerWorldCenter();
            foreach (var p in explosionSmokeParticles)
            {
                Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, p.Position);
                Color color = ComposeSmokeColor(p, fogColor, playerWorldCenter);
                if (p.Texture is Texture2D tex)
                {
                    Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
                    float lifeT = MathHelper.Clamp(p.AgeSeconds / Math.Max(0.001f, SmokeGrowthSeconds), 0f, 1f);
                    float growth = 1f + 1.0f * lifeT;
                    float scale = p.Scale * FogCloudScale * growth;
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
            float baseAlpha = MathHelper.Clamp(flashInfo.TimeLeft / ExplosionFlashDurationSeconds * (Game1.currentLocation.IsOutdoors ? 0.2f : 0.4f), 0f, 1f);
            Color flashColor = new Color(255, 160, 60) * baseAlpha;
            var vp = Game1.graphics.GraphicsDevice.Viewport;
            Rectangle full = new Rectangle(0, 0, vp.Width, vp.Height);
            spriteBatch.Draw(whitePixel, full, flashColor * 1.0f);
            spriteBatch.Draw(whitePixel, full, flashColor * 0.55f * flashInfo.TimeLeft);
            spriteBatch.Draw(whitePixel, full, flashColor * 0.25f * flashInfo.TimeLeft);
        }

        private void DrawSingleGrouse(SpriteBatch spriteBatch, NetGrouse g)
        {
            if (!Utility.isOnScreen(g.Position, 64))
                return;

            float deltaSeconds = (float)Game1.currentGameTime.ElapsedGameTime.TotalSeconds;
            g.UpdateGrouseAnimationState(deltaSeconds);
            g.UpdateFacingDirection();

            PlayGrouseNoise(g);
            if (g.State == GrouseState.Perched)
                DrawPerchedGrouse(spriteBatch, g);
            else
                DrawMovingGrouse(spriteBatch, g);
        }

        private void DrawPerchedGrouse(SpriteBatch spriteBatch, NetGrouse g)
        {
            if (grouseTexture == null || g.Alpha <= 0f)
                return;

            // Calculate hide/show animation
            float hideProgress = g.IsHiding ? 1f : 0f;
            if (g.IsTransitioning)
            {
                hideProgress = g.IsHiding ? g.HideTransitionProgress : (1f - g.HideTransitionProgress);
            }

            // If completely hidden, don't draw
            if (hideProgress >= 1f)
                return;

            Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, g.Position);
            screenPos.Y += g.FlightHeight;

            // Calculate vertical offset and sprite height based on hide progress
            const int maxSpriteHeight = 7;  // Full grouse sprite height
            const float popUpDistance = 8f;  // How far up the grouse pops when appearing

            // Interpolate sprite height and position
            float currentSpriteHeight = MathHelper.Lerp(maxSpriteHeight, 0f, hideProgress);
            float verticalOffset = MathHelper.Lerp(-popUpDistance, 0f, hideProgress);

            // Apply vertical offset to screen position
            screenPos.Y += verticalOffset;

            SpriteEffects effects = g.FacingLeft ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            int frameX = g.AnimationFrame % 2;
            Rectangle sourceRect = new Rectangle(
                frameX * GrouseSpriteWidth,
                0,
                GrouseSpriteWidth,
                (int)Math.Max(1, currentSpriteHeight)
            );
            Vector2 origin = new Vector2(GrouseSpriteWidth / 2f, GrouseSpriteHeight);
            spriteBatch.Draw(
                grouseTexture,
                position: screenPos,
                sourceRectangle: sourceRect,
                color: Color.White * g.Alpha,
                rotation: 0f,
                origin: origin,
                scale: g.Scale,
                effects: effects,
                layerDepth: 0.85f
            );
        }

        private void DrawMovingGrouse(SpriteBatch spriteBatch, NetGrouse g)
        {
            if (grouseTexture == null || g.Alpha <= 0f)
                return;

            Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, g.Position);
            screenPos.Y += g.FlightHeight;

            int frameX = 0;
            int frameY = 0;
            SpriteEffects effects = g.FacingLeft ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            float scale = g.Scale;

            switch (g.State)
            {
                case GrouseState.Surprised:
                    frameX = g.AnimationFrame;
                    frameY = 0;
                    break;
                case GrouseState.Flushing:
                case GrouseState.Flying:
                case GrouseState.Landing:
                    frameX = NetGrouse.wingPattern[g.AnimationFrame % NetGrouse.wingPattern.Length];
                    frameY = 1;
                    scale *= 1.2f;
                    break;
                case GrouseState.KnockedDown:
                    frameX = g.AnimationFrame;
                    frameY = 1;
                    scale *= 1.2f;
                    break;
            }
            Rectangle sourceRect = new Rectangle(
                frameX * GrouseSpriteWidth,
                frameY * GrouseSpriteHeight,
                GrouseSpriteWidth,
                GrouseSpriteHeight
            );
            Vector2 origin = new Vector2(GrouseSpriteWidth / 2f, GrouseSpriteHeight);
            spriteBatch.Draw(
                grouseTexture,
                position: screenPos,
                sourceRectangle: sourceRect,
                color: Color.White * g.Alpha,
                rotation: 0f,
                origin: origin,
                scale: scale,
                effects: effects,
                layerDepth: 0.85f
            );

            if (g.DamageFlashTimer is float damageFlashTimer && damageFlashTimer > 0f && g.Smoke is Vector2 smoke)
            {
                float ratio = damageFlashTimer / GrouseDamageFlashDuration;
                int damageFrameY = 0;
                int damageFrameX = 0;
                if (ratio < 0.33)
                    damageFrameX = 2;
                else if (ratio > 0.33 && ratio < 0.66)
                    damageFrameX = 1;
                Rectangle damageRect = new Rectangle(
                    damageFrameX * DamageSpriteWidth,
                    damageFrameY * DamageSpriteHeight,
                    DamageSpriteWidth,
                    DamageSpriteHeight
                );
                spriteBatch.Draw(
                    damageTexture,
                    position: smoke,
                    sourceRectangle: damageRect,
                    color: Color.White * ratio,
                    rotation: (float)Math.Sin(ratio * 2.0f * Math.PI),
                    origin: new Vector2(DamageSpriteWidth / 2f, DamageSpriteHeight / 2f),
                    scale: 1.5f,
                    effects: SpriteEffects.None,
                    layerDepth: 0.86f
                );
            }
            if (surprisedTexture != null && g.State == GrouseState.Surprised && g.AnimationFrame == 4)
            {
                Vector2 surprisedPos = screenPos;
                surprisedPos.Y -= GrouseSpriteHeight * scale * 1.02f;
                spriteBatch.Draw(
                    surprisedTexture,
                    position: surprisedPos,
                    sourceRectangle: null,
                    color: Color.White,
                    rotation: 0f,
                    origin: new Vector2(surprisedTexture.Width / 2f, surprisedTexture.Height),
                    scale: SurprisedSpriteScale,
                    effects: SpriteEffects.None,
                    layerDepth: 0.86f
                );
            }
        }
    }
}
