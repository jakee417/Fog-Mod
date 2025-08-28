using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;
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
            screenPos.Y += g.FlightHeight;

            int frameX = 0;
            int frameY = 0;
            SpriteEffects effects = g.FacingLeft ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            switch (g.State)
            {
                case GrouseState.Perched:
                    // Simple default frame - this won't be visible anyway
                    frameX = 0; // sitting left
                    frameY = 0;
                    break;
                case GrouseState.Surprised:
                    // Cycle through top row: sitting left (0) → standing right (1) → standing left (2) → surprised forward (3)
                    frameX = g.AnimationFrame;
                    frameY = 0;
                    break;
                case GrouseState.Flushing:
                case GrouseState.Flying:
                    // Map animation frame to wing pattern: 0→1→2→3→2→1→0→1→2→3...
                    frameX = FogMod.wingPattern[g.AnimationFrame % FogMod.wingPattern.Length];
                    frameY = 1;
                    break;
            }
            Rectangle sourceRect = new Rectangle(
                frameX * GrouseSpriteWidth,
                frameY * GrouseSpriteHeight,
                GrouseSpriteWidth,
                GrouseSpriteHeight
            );

            // Use position-based drawing with origin - this is simpler and more reliable
            // Origin at bottom center means the sprite's bottom-center will be at screenPos
            Vector2 origin = new Vector2(GrouseSpriteWidth / 2f, GrouseSpriteHeight);

            // Draw the grouse sprite directly at the screen position
            // The origin will automatically position the sprite so its bottom-center is at screenPos
            spriteBatch.Draw(
                grouseTexture,
                position: screenPos,
                sourceRectangle: sourceRect,
                color: Color.White,
                rotation: 0f,
                origin: origin,
                scale: g.Scale,
                effects: effects,
                layerDepth: 0.85f
            );
            g.HasBeenSpotted = true;
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
