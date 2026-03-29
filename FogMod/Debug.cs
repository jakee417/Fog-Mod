#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using FogMod.Models;
using FogMod.Utils;

namespace FogMod
{
    public partial class FogMod : Mod
    {
        public void PrintDebugInfoToConsole()
        {
            // Print all MonsterSlayerQuests keys and their status
            try
            {
                var quests = Helper.GameContent.Load<Dictionary<string, StardewValley.GameData.MonsterSlayerQuestData>>("Data/MonsterSlayerQuests");
                foreach (var kvp in quests)
                {
                    string key = kvp.Key;
                    var quest = kvp.Value;

                    if (key != Constants.GrouseQuestName)
                        continue;

                    // Print the raw JSON of the quest object
                    try
                    {
                        string questJson = System.Text.Json.JsonSerializer.Serialize(quest, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        Monitor.Log($"[MonsterSlayerQuest][JSON] Key: {key}\n{questJson}", LogLevel.Info);
                    }
                    catch (Exception ex)
                    {
                        Monitor.Log($"Error serializing MonsterSlayerQuestData for key {key}: {ex.Message}", LogLevel.Warn);
                    }

                    int kills = 0;
                    foreach (var target in quest.Targets)
                        kills += Game1.stats.getMonstersKilled(target);
                    bool complete = kills >= quest.Count;
                    bool gilQuestFlag = Game1.player.mailReceived.Contains($"Gil_{key}");
                    Monitor.Log($"[MonsterSlayerQuest] Key: {key}, Kills: {kills}/{quest.Count}, Complete: {complete}, Gil flag: {(gilQuestFlag ? "YES" : "NO")}", LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Error printing MonsterSlayerQuests debug info: {ex.Message}", LogLevel.Warn);
            }

            // Print things like time of day multiplier, weather multiplier,
            // cloud count, etc.
            string cloudCountText = $"Clouds: {floatingParticles?.Count ?? 0}";
            string smokeCountText = $"Smoke: {explosionSmokeParticles?.Count ?? 0}";
            string fogCountText = $"Banks: {numFogBankChunks}";

            string grouseInfo = "";
            List<Grouse>? allGrouse = GetAllGrouse();
            string grouseCountText = $"Grouse: {allGrouse?.Count ?? 0} in {outdoorLocations.Count()} locations w/ {TreeHelper.GetAvailableTrees(outdoorLocations).Count} trees";
            string grouseInLocation = $"Grouse In {Game1.currentLocation?.NameOrUniqueName ?? "Unknown"}: {GetNPCsAtCurrentLocation()?.Count(p => p is Grouse)}";
            int surprisedGrouse = allGrouse?.Count(g => g.State == GrouseState.Surprised) ?? 0;
            int flyingGrouse = allGrouse?.Count(g => g.State == GrouseState.Flying || g.State == GrouseState.Flushing) ?? 0;
            int landingGrouse = allGrouse?.Count(g => g.State == GrouseState.Landing) ?? 0;
            string stateText = $"Surprised: {surprisedGrouse}, Flying: {flyingGrouse}, Landing: {landingGrouse}";
            int grouseKilled = Game1.stats.getMonstersKilled(Constants.GrouseName);
            int grouseQuestGoal = Constants.GrouseQuestGoal;
            bool grouseRewardReceived = Game1.player.mailReceived.Contains(Constants.GrouseSlayerCompleteFlag);
            string gilFlag = $"Gil_{Constants.GrouseQuestName}";
            bool gilRewardReceived = Game1.player.mailReceived.Contains(gilFlag);
            string grouseKilledText = $"Grouse Killed: {grouseKilled}/{grouseQuestGoal} (Reward mail: {(grouseRewardReceived ? "YES" : "NO")}, Gil flag: {(gilRewardReceived ? "YES" : "NO")})";
            grouseInfo = $"\n{grouseCountText}\n{grouseInLocation}\n{stateText}\n{grouseKilledText}";
            string timeOfDayMultiplierText = $"Time of day multiplier: {ComputeTimeOfDayOpacityMultiplier():F2}";
            string weatherMultiplierText = $"Weather multiplier: {GetWeatherIntensityFactor():F2}";
            string dailyFogMultiplierText = $"Daily fog multiplier: {dailyFogStrength:F2}";
            string locationText = $"Location: {Game1.currentLocation?.NameOrUniqueName ?? "None"}";
            string fogGridSizeText = $"Fog grid size: {grid.ExtCols}x{grid.ExtRows} = {grid.ExtCols * grid.ExtRows}";
            string fogDayText = $"Fog day: {isFogDay} w/ prob {probabilityOfFogRoll:F2} <? {probabilityOfFogForADay:F2}";
            string needsSync = $"Needs Sync: {!Utils.Multiplayer.IsAbleToUpdateOwnWorld()}";
            string debugInfo = $"{fogDayText}\n{fogGridSizeText}\n{cloudCountText}\n{fogCountText}\n{smokeCountText}{grouseInfo}\n{dailyFogMultiplierText}\n{timeOfDayMultiplierText}\n{weatherMultiplierText}\n{locationText}\n{needsSync}";
            Monitor.Log(debugInfo, LogLevel.Info);
        }

        public static void DrawLine(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch, Texture2D texture, Vector2 a, Vector2 b, Color color, float thickness)
        {
            Vector2 delta = b - a;
            float length = delta.Length();
            if (length <= 0.001f) return;
            float rotation = (float)Math.Atan2(delta.Y, delta.X);
            spriteBatch.Draw(texture, a, null, color, rotation, new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0.99f);
        }

        public void DrawDebugFogGrid(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch)
        {
            Texture2D squareFogTexture = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1);
            squareFogTexture.SetData(new[] { Color.White });
            if (squareFogTexture == null) return;
            Color lineColor = Color.Black * 0.85f;
            float thickness = Constants.DebugRingThickness;
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
