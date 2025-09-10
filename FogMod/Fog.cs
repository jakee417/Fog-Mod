#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using System;
using System.Collections.Generic;
using StardewValley;
using FogMod.Models;

namespace FogMod;

public partial class FogMod : Mod
{
    private bool isFogDay = false;
    private float probabilityOfFogForADay = 0.05f;
    private float probabilityOfFogRoll = 0.0f;
    private float breathBasePhase;
    private float dailyFogStrength = 0f;
    public List<Texture2D>? cloudTextures { get; set; }
    private List<FogParticle> floatingParticles = new List<FogParticle>();
    private CellOccupancy fogCellOccupancy;

    internal void InitializeDailyFogStrength()
    {
        if (!Context.IsWorldReady)
            return;

        if (!Config.EnableDailyRandomFog)
        {
            isFogDay = true;
            dailyFogStrength = 1f;
            probabilityOfFogForADay = 1f;
            probabilityOfFogRoll = 0f;
            return;
        }

        int daysPlayed = (int)Game1.stats.DaysPlayed;
        FogForecast forecast = ComputeFogForecast(daysPlayed);
        isFogDay = forecast.IsFogDay;
        dailyFogStrength = forecast.DailyFogStrength;
        probabilityOfFogForADay = forecast.ProbabilityOfFogForADay;
        probabilityOfFogRoll = forecast.ProbabilityOfFogRoll;
    }

    private static FogForecast ComputeFogForecast(int daysPlayed)
    {
        int seed = daysPlayed ^ (int)(Game1.uniqueIDForThisGame & 0x7FFFFFFF);
        var rng = new Random(seed);
        float probabilityOfFogRoll = (float)rng.NextDouble();
        float probabilityOfFogForADay = ComputeProbabilityOfFogForADay();
        bool isFogDay = probabilityOfFogRoll <= probabilityOfFogForADay;
        float strengthRoll = (float)rng.NextDouble();
        float dailyFogStrength = MathHelper.Lerp(Constants.DailyRandomFogMin, Constants.DailyRandomFogMax, strengthRoll);
        return new FogForecast(
            isFogDay: isFogDay,
            dailyFogStrength: dailyFogStrength,
            probabilityOfFogForADay: probabilityOfFogForADay,
            probabilityOfFogRoll: probabilityOfFogRoll
        );
    }

    private static float ComputeProbabilityOfFogForADay()
    {
        string season = Game1.currentSeason;
        float seasonalProbability;
        switch (season)
        {
            case "spring":
                seasonalProbability = 0.05f;
                break;
            case "summer":
                seasonalProbability = 0.1f;
                break;
            case "fall":
                seasonalProbability = 0.2f;
                break;
            case "winter":
                seasonalProbability = 0.18f;
                break;
            default:
                seasonalProbability = 0.08f;
                break;
        }
        return seasonalProbability;
    }

    private void InitializeFloatingFogParticles()
    {
        floatingParticles = new List<FogParticle>();
        grid = new Grid(
            cellSize: Constants.FogTileSize,
            bufferCells: Constants.DefaultFogGridBufferCells
        );
        for (int row = 0; row < grid.ExtRows; row++)
        {
            for (int col = 0; col < grid.ExtCols; col++)
            {
                for (int n = 0; n < Constants.MinimumFogParticlesPerCell; n++)
                {
                    if (CreateParticleInCell(col, row, grid.ExtOriginWorld) is FogParticle p)
                        floatingParticles.Add(p);
                }
            }
        }
    }

    public void ResetFogParticles()
    {
        floatingParticles.Clear();
    }

    private FogParticle? CreateParticleInCell(int col, int row, Vector2 viewportTopLeftWorld)
    {
        float speed = Constants.FloatingParticleSpeed * (0.9f + (float)Random.NextDouble() * 0.2f);
        Vector2 baseDir = globalWindDirection;
        Vector2 velocity;
        float angleJitter = ((float)Random.NextDouble() - 0.5f) * MathHelper.ToRadians(12f);
        float cos = (float)Math.Cos(angleJitter);
        float sin = (float)Math.Sin(angleJitter);
        Vector2 jitteredDir = new Vector2(
            baseDir.X * cos - baseDir.Y * sin,
            baseDir.X * sin + baseDir.Y * cos
        );
        if (jitteredDir.LengthSquared() > 0.0000001f)
            jitteredDir.Normalize();
        else if (baseDir.LengthSquared() > 0.0000001f)
            jitteredDir = Vector2.Normalize(baseDir);
        else
            jitteredDir = Vector2.Zero;
        velocity = jitteredDir * speed;
        float cellX = viewportTopLeftWorld.X + col * Constants.FogTileSize;
        float cellY = viewportTopLeftWorld.Y + row * Constants.FogTileSize;
        float x = cellX + (float)Random.NextDouble() * Constants.FogTileSize;
        float y = cellY + (float)Random.NextDouble() * Constants.FogTileSize;
        float scale = Constants.DefaultFloatingScaleMin + (float)Random.NextDouble() * Math.Max(0.01f, Constants.DefaultFloatingScaleMax - Constants.DefaultFloatingScaleMin);
        float alpha = 0.35f + (float)Random.NextDouble() * 0.35f;
        if (cloudTextures != null && cloudTextures.Count > 0)
        {
            int idx = Random.Next(cloudTextures.Count);
            Texture2D chosenTex = cloudTextures[idx];
            return new FogParticle(
                position: new Vector2(x, y),
                velocity: velocity,
                scale: scale,
                rotation: 0f,
                alpha: Math.Clamp(alpha, 0.05f, 0.6f),
                ageSeconds: 0f,
                texture: chosenTex,
                isFadingOut: false,
                fadeOutSecondsLeft: Constants.ParticleFadeOutSeconds
            );
        }
        return null;
    }

    private void UpdateFloatingFogParticles(float deltaSeconds)
    {
        if (floatingParticles.Count == 0)
            InitializeFloatingFogParticles();
        floatingParticles = FogParticle.RemoveUnusedParticles(floatingParticles, grid, deltaSeconds, true);
        var occupancy = CellOccupancy.ComputeCellOccupancy(floatingParticles, grid);
        occupancy.Counts = PopulateCellsUnderTarget(occupancy.Counts);
        RemoveFogOverTarget(occupancy);
        fogCellOccupancy = occupancy;
    }

    private int[,] PopulateCellsUnderTarget(int[,] counts)
    {
        for (int row = 0; row < grid.ExtRows; row++)
        {
            for (int col = 0; col < grid.ExtCols; col++)
            {
                int target = Constants.MinimumFogParticlesPerCell;
                int need = target - counts[col, row];
                for (int k = 0; k < need; k++)
                {
                    if (CreateParticleInCell(col, row, grid.ExtOriginWorld) is FogParticle p)
                    {
                        floatingParticles.Add(p);
                        counts[col, row]++;
                    }
                }
            }
        }
        return counts;
    }

    private void RemoveFogOverTarget(CellOccupancy occupancy)
    {
        for (int row = 0; row < grid.ExtRows; row++)
        {
            for (int col = 0; col < grid.ExtCols; col++)
            {
                int target = Constants.MaximumFogParticlesPerCell;
                if (occupancy.Indices is List<int>[,])
                {
                    int eligible = occupancy.Indices[col, row] != null ? occupancy.Indices[col, row].Count : 0;
                    int extra = eligible - target;
                    if (extra <= 0)
                        continue;
                    var list = occupancy.Indices[col, row];
                    if (list == null || list.Count == 0)
                        continue;

                    list.Sort((a, b) => floatingParticles[b].AgeSeconds.CompareTo(floatingParticles[a].AgeSeconds));
                    int toFade = Math.Min(extra, list.Count);
                    for (int k = 0; k < toFade; k++)
                    {
                        int idx = list[k];
                        var p = floatingParticles[idx];
                        if (!p.IsFadingOut)
                        {
                            p.IsFadingOut = true;
                            floatingParticles[idx] = p;
                        }
                    }
                }
            }
        }
    }
}