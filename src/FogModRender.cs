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
                Texture2D tex = p.Texture;
                if (tex == null) continue;
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
                    layerDepth: 0.95f
                );
            }
        }

        private void DrawExplosionSmokeParticles(SpriteBatch spriteBatch, Color fogColor)
        {
            Vector2 playerWorldCenter = GetPlayerWorldCenter();
            foreach (var p in explosionSmokeParticles)
            {
                Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, p.Position);
                Color color = ComposeSmokeColor(p, fogColor, playerWorldCenter);
                Texture2D tex = p.Texture;
                if (tex == null) continue;
                Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
                float lifeT = MathHelper.Clamp(p.AgeSeconds / Math.Max(0.001f, SmokeGrowthSeconds), 0f, 1f);
                float growth = 1f + 1.0f * lifeT;
                float scale = p.Scale * FogCloudScale * growth;

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
                // Only draw if not perched (perched grouse are hidden in trees)
                if (g.State == GrouseState.Perched)
                    continue;

                DrawSingleGrouse(spriteBatch, g);
            }
        }

        private void DrawSingleGrouse(SpriteBatch spriteBatch, Grouse g)
        {
            // Skip drawing if texture isn't loaded
            if (grouseTexture == null)
                return;

            Vector2 screenPos = Game1.GlobalToLocal(Game1.viewport, g.Position);
            screenPos.Y += g.FlightHeight; // Apply flight bobbing

            // Calculate source rectangle for current animation frame
            // 2x2 grid: [0,0]=standing, [1,0]=sitting, [0,1]=flying up, [1,1]=flying down
            int frameX = 0;
            int frameY = 0;
            switch (g.State)
            {
                case GrouseState.Perched:
                    frameX = g.AnimationFrame % 2; // 0=standing, 1=sitting
                    frameY = 0;
                    break;
                case GrouseState.Surprised:
                    // For now, use the same frames as perched (later you can add surprised-specific frames)
                    frameX = g.AnimationFrame % 2; // 0=standing, 1=sitting
                    frameY = 0;
                    break;
                case GrouseState.Flushing:
                case GrouseState.Flying:
                    // Alternate between flying up and down
                    frameX = g.AnimationFrame % 2; // 0=up, 1=down
                    frameY = 1;
                    break;
            }
            Rectangle sourceRect = new Rectangle(
                frameX * GrouseSpriteWidth,
                frameY * GrouseSpriteHeight,
                GrouseSpriteWidth,
                GrouseSpriteHeight
            );

            // Calculate destination rectangle with proper scaling
            Rectangle destRect = new Rectangle(
                (int)(screenPos.X - (GrouseSpriteWidth * g.Scale / 2)),
                (int)(screenPos.Y - (GrouseSpriteHeight * g.Scale / 2)),
                (int)(GrouseSpriteWidth * g.Scale),
                (int)(GrouseSpriteHeight * g.Scale)
            );

            // Color effects based on state
            Color grouseColor = Color.White;
            if (g.State == GrouseState.Flushing)
            {
                // Flash effect during flush
                float flushProgress = g.StateTimer / GrouseFlushDuration;
                float flashIntensity = (float)Math.Sin(g.StateTimer * 15f) * 0.5f + 0.5f;
                grouseColor = Color.Lerp(Color.White, Color.Yellow, flashIntensity * 0.6f);
            }

            // Determine sprite effects
            // Sprite in PNG faces left, so flip when grouse should face right
            SpriteEffects effects = g.FacingLeft ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            Vector2 origin = new Vector2(GrouseSpriteWidth / 2f, GrouseSpriteHeight / 2f);

            // Draw the grouse sprite
            spriteBatch.Draw(
                grouseTexture,
                destinationRectangle: destRect,
                sourceRectangle: sourceRect,
                color: grouseColor,
                rotation: 0f,
                origin: origin,
                effects: effects,
                layerDepth: 0.85f
            );
        }

        private void DrawDebugInfo(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch)
        {
            // Print things like time of day multiplier, weather multiplier,
            // cloud count, etc.
            string cloudCountText = $"Clouds: {floatingParticles?.Count ?? 0}";
            string smokeCountText = $"Smoke: {explosionSmokeParticles?.Count ?? 0}";

            string grouseInfo = "";
            if (config.EnableGrouseCritters)
            {
                string grouseCountText = $"Grouse: {grouse?.Count ?? 0}";
                int surprisedGrouse = grouse?.Where(g => g.State == GrouseState.Surprised).Count() ?? 0;
                int flyingGrouse = grouse?.Where(g => g.State == GrouseState.Flushing || g.State == GrouseState.Flying).Count() ?? 0;
                string stateText = $"Surprised: {surprisedGrouse}, Flying: {flyingGrouse}";
                grouseInfo = $"\n{grouseCountText}\n{stateText}";
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

        // Draw debug rings for each light source influence radius
        private void DrawDebugLightRings(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch)
        {
            if (lightSources == null || lightSources.Count == 0) return;
            Texture2D px = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1);
            px.SetData(new[] { Color.White });
            if (px == null) return;
            Color ringColor = Color.Yellow;
            int segments = 48;
            for (int i = 0; i < lightSources.Count; i++)
            {
                var li = lightSources[i];
                Vector2 centerScreen = Game1.GlobalToLocal(Game1.viewport, li.Position);
                float radius = li.RadiusPixels;
                // draw approximate circle with line segments
                Vector2 prev = centerScreen + new Vector2(radius, 0f);
                for (int s = 1; s <= segments; s++)
                {
                    float ang = MathHelper.TwoPi * (s / (float)segments);
                    Vector2 curr = centerScreen + new Vector2((float)Math.Cos(ang) * radius, (float)Math.Sin(ang) * radius);
                    DrawLine(spriteBatch, px, prev, curr, ringColor * 0.8f, DebugRingThickness);
                    prev = curr;
                }
            }
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
                    int fogCount = (col < countCols && row < countRows) ? fogCellOccupancy.Counts[col, row] : 0;
                    int smokeCount = (col < smokeCols && row < smokeRows) ? smokeCellOccupancy.Counts[col, row] : 0;
                    string text = $"{fogCount}/{smokeCount}";
                    var size = font.MeasureString(text);
                    Vector2 pos = new Vector2(screenRect.Right - size.X - margin, screenRect.Top + margin);
                    spriteBatch.DrawString(font, text, pos, Color.Black);
                }
            }
        }
    }
}
