#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Linq;

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

        private void DrawGrouse(SpriteBatch spriteBatch)
        {
            if (grouse.Count == 0)
                return;

            foreach (var g in grouse)
            {
                if (g.Location != Game1.currentLocation?.NameOrUniqueName)
                    continue;

                DrawSingleGrouse(spriteBatch, g);
            }
        }

        private void DrawSingleGrouse(SpriteBatch spriteBatch, NetGrouse g)
        {
            if (grouseTexture == null || g.Alpha <= 0f)
                return;

            Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, g.Position);
            screenPos.Y += g.FlightHeight;

            int frameX = 0;
            int frameY = 0;
            SpriteEffects effects = g.FacingLeft ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            switch (g.State)
            {
                case GrouseState.Perched:
                case GrouseState.Surprised:
                case GrouseState.KnockedDown:
                    frameX = g.AnimationFrame;
                    frameY = 0;
                    break;
                case GrouseState.Flushing:
                case GrouseState.Flying:
                    frameX = NetGrouse.wingPattern[g.AnimationFrame % NetGrouse.wingPattern.Length];
                    frameY = 1;
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
                scale: g.Scale,
                effects: effects,
                layerDepth: 0.85f
            );

            if (g.DamageFlashTimer is float damageFlashTimer && damageFlashTimer > 0f && g.Smoke is CollisionSmoke smoke)
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
                    position: smoke.Position,
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
                surprisedPos.Y -= GrouseSpriteHeight * g.Scale * 1.02f;
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
            g.HasBeenSpotted = true;
        }

        private void DrawDebugInfo(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch)
        {
            // Print things like time of day multiplier, weather multiplier,
            // cloud count, etc.
            string cloudCountText = $"Clouds: {floatingParticles?.Count ?? 0}";
            string smokeCountText = $"Smoke: {explosionSmokeParticles?.Count ?? 0}";

            string grouseInfo = "";
            if (Config.EnableGrouseCritters)
            {
                string grouseCountText = $"Grouse: {grouse?.Count ?? 0} in {outdoorLocations.Count()} locations";
                string grouseInLocation = $"Grouse In {Game1.currentLocation?.NameOrUniqueName ?? "Unknown"}: {grouse?.Count(g => g.Location == Game1.currentLocation?.NameOrUniqueName) ?? 0}";
                int surprisedGrouse = grouse?.Where(g => g.State == GrouseState.Surprised).Count() ?? 0;
                int flyingGrouse = grouse?.Where(g => g.State == GrouseState.Flushing || g.State == GrouseState.Flying).Count() ?? 0;
                int knockedDownGrouse = grouse?.Where(g => g.State == GrouseState.KnockedDown).Count() ?? 0;
                string stateText = $"Surprised: {surprisedGrouse}, Flying: {flyingGrouse}, Knocked Down: {knockedDownGrouse}";
                grouseInfo = $"\n{grouseCountText}\n{grouseInLocation}\n{stateText}";
            }

            string timeOfDayMultiplierText = $"Time of day multiplier: {ComputeTimeOfDayOpacityMultiplier():F2}";
            string weatherMultiplierText = $"Weather multiplier: {lastWeatherFogIntensityFactor:F2}";
            string dailyFogMultiplierText = $"Daily fog multiplier: {dailyFogStrength:F2}";
            string locationText = $"Location: {Game1.currentLocation?.NameOrUniqueName ?? "None"}";
            string fogGridSizeText = $"Fog grid size: {grid.ExtCols}x{grid.ExtRows} = {grid.ExtCols * grid.ExtRows}";
            string fogDayText = $"Fog day: {isFogDay} w/ prob {probabilityOfFogRoll:F2} <? {probabilityOfFogForADay:F2}";
            string text = $"{fogDayText}\n{fogGridSizeText}\n{cloudCountText}\n{smokeCountText}{grouseInfo}\n{dailyFogMultiplierText}\n{timeOfDayMultiplierText}\n{weatherMultiplierText}\n{locationText}";
            var font = Game1.smallFont;
            int margin = 8;
            // Put text in upper-left corner
            Vector2 pos = new Vector2(margin, margin);
            spriteBatch.DrawString(font, text, pos, Color.Red);
        }

        private static void DrawLine(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch, Texture2D texture, Vector2 a, Vector2 b, Color color, float thickness)
        {
            Vector2 delta = b - a;
            float length = delta.Length();
            if (length <= 0.001f) return;
            float rotation = (float)Math.Atan2(delta.Y, delta.X);
            spriteBatch.Draw(texture, a, null, color, rotation, new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0.99f);
        }

        private void DrawDebugFogGrid(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch)
        {
            Texture2D squareFogTexture = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1);
            squareFogTexture.SetData(new[] { Color.White });
            if (squareFogTexture == null) return;
            Color lineColor = Color.Black * 0.85f;
            float thickness = DebugRingThickness;
            var font = Game1.smallFont;
            int margin = 2;
            int countCols = fogCellOccupancy.Counts != null ? fogCellOccupancy.Counts.GetLength(0) : 0;
            int countRows = fogCellOccupancy.Counts != null ? fogCellOccupancy.Counts.GetLength(1) : 0;
            int smokeCols = smokeCellOccupancy.Counts != null ? smokeCellOccupancy.Counts.GetLength(0) : 0;
            int smokeRows = smokeCellOccupancy.Counts != null ? smokeCellOccupancy.Counts.GetLength(1) : 0;
            for (int row = 0; row < grid.ExtRows; row++)
            {
                for (int col = 0; col < grid.ExtCols; col++)
                {
                    float x = grid.ExtOriginWorld.X + col * grid.CellSize;
                    float y = grid.ExtOriginWorld.Y + row * grid.CellSize;
                    var worldRect = new Rectangle((int)Math.Floor(x), (int)Math.Floor(y), (int)Math.Ceiling(grid.CellSize), (int)Math.Ceiling(grid.CellSize));
                    var screenRect = Game1.GlobalToLocal(Game1.viewport, worldRect);

                    // outline
                    Vector2 a = new Vector2(screenRect.Left, screenRect.Top);
                    Vector2 b = new Vector2(screenRect.Right, screenRect.Top);
                    Vector2 c = new Vector2(screenRect.Right, screenRect.Bottom);
                    Vector2 d = new Vector2(screenRect.Left, screenRect.Bottom);
                    DrawLine(spriteBatch, squareFogTexture, a, b, lineColor, thickness);
                    DrawLine(spriteBatch, squareFogTexture, b, c, lineColor, thickness);
                    DrawLine(spriteBatch, squareFogTexture, c, d, lineColor, thickness);
                    DrawLine(spriteBatch, squareFogTexture, d, a, lineColor, thickness);

                    // draw fog/smoke counts in top-right corner
                    int fogCount = (col < countCols && row < countRows) ? (fogCellOccupancy.Counts is int[,] fogCounts ? fogCounts[col, row] : 0) : 0;
                    int smokeCount = (col < smokeCols && row < smokeRows) ? (smokeCellOccupancy.Counts is int[,] smokeCounts ? smokeCounts[col, row] : 0) : 0;
                    string text = $"{fogCount}/{smokeCount}";
                    var size = font.MeasureString(text);
                    Vector2 pos = new Vector2(screenRect.Right - size.X - margin, screenRect.Top + margin);
                    spriteBatch.DrawString(font, text, pos, Color.Black);
                }
            }
        }
    }
}
