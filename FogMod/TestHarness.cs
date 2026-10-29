#nullable enable
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using Netcode;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FogMod.Models;
using FogMod.Utils;

namespace FogMod;

// Headless integration harness, inert unless FOGMOD_TEST=1. Auto-loads
// FOGMOD_TEST_SAVE, runs a scripted critterz scenario (spawn, trigger,
// run, arrival, kill, loot) with assertions, writes FOGMOD_TEST_RESULTS,
// and exits nonzero on failure. The process exits without saving, so the
// save file is never mutated.
public partial class FogMod : Mod
{
    private bool testActive;
    private int testPhase;
    private int testPhaseTicks;
    private readonly List<(int Phase, string Line)> testResults = new();
    private int testFailures;
    private string testSaveName = "Tims_452916884";
    private string testResultsPath = "";
    private Critterz? testRunner;
    private Critterz? testVictim;
    private Vector2 testDestBush;
    private int testLeavesBefore;
    private Grouse? testFlyer;
    private Grouse? testGrouseVictim;
    private Vector2 testGrouseStartTree;
    private int testGrouseEggs;
    private int testGrouseLeavesBefore;
    private int testGrouseDebrisBefore;
    private readonly List<Critterz> testCritters = new();
    private readonly List<Grouse> testGrouse = new();
    private Critterz? testSitterA;
    private Critterz? testRunnerB;
    private Grouse? testGrouseDmg;
    private Critterz? testMissCritterz;
    private Grouse? testMissGrouse;
    private Grouse? testHider;
    private bool testHideSeenHidden;
    private bool testHideSeenExposed;
    private readonly List<Critterz> testSkinCritters = new();
    private Critterz? testNowhere;
    private bool testFogResetChecked;
    private Vector2 testCycleHome;
    private bool testCycleSawRun;
    private Vector2 testFlightTree;
    private bool testFlightSawAir;
    private const int TestFinishPhase = 24;

    private record TestScenario(string Name, int FirstPhase, int LastPhase);
    private static readonly TestScenario[] TestScenarios = new TestScenario[]
    {
        new("Setup", 0, 2),
        new("Critter run", 3, 6),
        new("Critter combat", 7, 9),
        new("Grouse perch & flight", 10, 15),
        new("Grouse combat", 16, 16),
        new("Spawning", 17, 20),
        new("Systems", 21, 23),
    };
    private int testAnnouncedScenario = -1;

    private static int ScenarioForPhase(int phase)
    {
        for (int i = 0; i < TestScenarios.Length; i++)
        {
            if (phase >= TestScenarios[i].FirstPhase && phase <= TestScenarios[i].LastPhase)
                return i;
        }
        return -1;
    }

    private void InitTestHarness()
    {
        if (Environment.GetEnvironmentVariable("FOGMOD_TEST") != "1")
            return;
        testActive = true;
        testSaveName = Environment.GetEnvironmentVariable("FOGMOD_TEST_SAVE") ?? testSaveName;
        testResultsPath = Environment.GetEnvironmentVariable("FOGMOD_TEST_RESULTS")
            ?? Path.Combine(Path.GetTempPath(), "fogmod-test-results.txt");
        Helper.Events.GameLoop.UpdateTicked += OnTestTick;
        Helper.Events.Display.Rendering += OnTestRendering;
        testHideFarmer = true;
        try
        {
            var testHarmony = new HarmonyLib.Harmony(ModManifest.UniqueID + ".Test");
            testHarmony.Patch(
                original: HarmonyLib.AccessTools.Method(typeof(Farmer), nameof(Farmer.draw),
                    new[] { typeof(Microsoft.Xna.Framework.Graphics.SpriteBatch) }),
                prefix: new HarmonyLib.HarmonyMethod(typeof(FogMod), nameof(OnTestFarmerDrawPrefix))
            );
        }
        catch (Exception ex)
        {
            Monitor.Log($"Test farmer-hide patch failed (cosmetic): {ex.Message}", LogLevel.Warn);
        }
        Monitor.Log($"Test harness active (save={testSaveName}, results={testResultsPath})", LogLevel.Info);
    }

    private void Check(bool cond, string name, string detail = "")
    {
        string line = cond ? $"PASS {name}" : $"FAIL {name}: {detail}";
        testResults.Add((testPhase, line));
        int scenario = ScenarioForPhase(testPhase);
        string tag = scenario >= 0 ? $"[S{scenario + 1}] " : "";
        if (cond)
            Monitor.Log($"TEST {tag}{line}", LogLevel.Info);
        else
        {
            testFailures++;
            Monitor.Log($"TEST {tag}{line}", LogLevel.Error);
        }
    }

    private void NextPhase()
    {
        testPhase++;
        testPhaseTicks = 0;
    }

    // Watch-mode camera: SnapCam jumps, TrackCam retargets the smooth
    // follow (applied every frame in OnTestRendering). The farmer stays
    // parked (and undrawn), so the frame holds only the subject.
    private Vector2 testCamPos;
    private Vector2 testCamTarget;
    private bool testCamActive;
    private bool testHideFarmer;

    private void SnapCam(Vector2 pixelPos)
    {
        testCamPos = pixelPos;
        testCamTarget = pixelPos;
        testCamActive = true;
    }

    private void TrackCam(Vector2 pixelPos)
    {
        testCamTarget = pixelPos;
        testCamActive = true;
    }

    private void OnTestRendering(object? sender, RenderingEventArgs e)
    {
        if (!testActive || !testCamActive || Game1.currentLocation is not GameLocation location)
            return;
        var map = location.Map;
        if (map == null || map.Layers == null || map.Layers.Count == 0)
            return;
        float dt = (float)Game1.currentGameTime.ElapsedGameTime.TotalSeconds;
        float t = Math.Min(1f, dt * 5f);
        testCamPos += (testCamTarget - testCamPos) * t;
        int mapW = map.Layers[0].LayerWidth * Game1.tileSize;
        int mapH = map.Layers[0].LayerHeight * Game1.tileSize;
        int x = (int)(testCamPos.X - Game1.viewport.Width / 2);
        int y = (int)(testCamPos.Y - Game1.viewport.Height / 2);
        Game1.viewport.X = Math.Clamp(x, 0, Math.Max(0, mapW - Game1.viewport.Width));
        Game1.viewport.Y = Math.Clamp(y, 0, Math.Max(0, mapH - Game1.viewport.Height));
    }

    public static bool OnTestFarmerDrawPrefix()
    {
        return !(Instance?.testHideFarmer ?? false);
    }

    private void GiveUp(string name, string detail)
    {
        Check(false, name, detail);
        testPhase = TestFinishPhase;
        testPhaseTicks = 0;
    }

    private void OnTestTick(object? sender, UpdateTickedEventArgs e)
    {
        if (!testActive || testPhase > TestFinishPhase)
            return;
        testPhaseTicks++;
        int scenario = ScenarioForPhase(testPhase);
        if (scenario != testAnnouncedScenario)
        {
            testAnnouncedScenario = scenario;
            if (scenario >= 0)
            {
                var s = TestScenarios[scenario];
                Monitor.Log($"TEST === Scenario {scenario + 1}/{TestScenarios.Length}: {s.Name} ===", LogLevel.Info);
                try { Game1.addHUDMessage(new HUDMessage($"Test: {s.Name}", 2)); } catch { }
            }
        }
        try
        {
            switch (testPhase)
            {
                case 0: TestWaitTitle(); break;
                case 1: TestWaitLoaded(); break;
                case 2: TestWarp(); break;
                case 3: TestSpawn(); break;
                case 4: TestTrigger(); break;
                case 5: TestRun(); break;
                case 6: TestSecondCycle(); break;
                case 7: TestKill(); break;
                case 8: TestDamageRules(); break;
                case 9: TestMissRate(); break;
                case 10: TestGrouseSpawn(); break;
                case 11: TestGrouseTrigger(); break;
                case 12: TestGrouseFlush(); break;
                case 13: TestGrouseLand(); break;
                case 14: TestHiding(); break;
                case 15: TestSecondFlight(); break;
                case 16: TestGrouseKill(); break;
                case 17: TestAuditSpawns(); break;
                case 18: TestSkinSeason(); break;
                case 19: TestNowhereToRun(); break;
                case 20: TestLaunchedAndCommands(); break;
                case 21: TestFxAndSnacks(); break;
                case 22: TestFog(); break;
                case 23: TestScattergun(); break;
                case TestFinishPhase: TestFinish(); break;
            }
        }
        catch (Exception ex)
        {
            Check(false, "harness", $"phase {testPhase}: {ex.GetType().Name}: {ex.Message}");
            Monitor.Log($"TEST harness stack: {ex}", LogLevel.Error);
            testPhase = TestFinishPhase;
            testPhaseTicks = 0;
        }
    }

    // wait for the title screen, then load the test save exactly
    // like clicking its slot (SaveFileSlot.Activate).
    private void TestWaitTitle()
    {
        if (testPhaseTicks > 10800)
        {
            GiveUp("autoload", "never reached title screen");
            return;
        }
        if (testPhaseTicks > 120 && Game1.activeClickableMenu is TitleMenu)
        {
            SaveGame.Load(testSaveName);
            Game1.exitActiveMenu();
            Monitor.Log($"TEST loading save {testSaveName}", LogLevel.Info);
            NextPhase();
        }
    }

    // wait for the world.
    private void TestWaitLoaded()
    {
        if (testPhaseTicks > 10800)
        {
            GiveUp("load", $"save {testSaveName} never became ready");
            return;
        }
        if (Context.IsWorldReady)
            NextPhase();
    }

    // warp to the Forest (bushes everywhere, no farm in the way).
    private void TestWarp()
    {
        if (testPhaseTicks == 1)
            Game1.warpFarmer("Forest", 50, 30, false);
        if (Game1.currentLocation?.NameOrUniqueName == "Forest")
            NextPhase();
        else if (testPhaseTicks > 600)
            GiveUp("warp", "never arrived in Forest");
    }

    // spawn a critter in the nearest usable bush.
    private void TestSpawn()
    {
        GameLocation location = Game1.currentLocation;
        Bush? bush = NearestReachableBush(location, Game1.player.Tile);
        if (bush == null)
        {
            GiveUp("spawn", "no reachable bush in Forest");
            return;
        }
        testRunner = SpawnCritterz(location, bush.Tile, false, bush.Tile);
        testCritters.Add(testRunner);
        Check(testRunner.State == CritterzState.Sitting, "spawn-sitting", $"state={testRunner.State}");
        Check(testRunner.Skin >= 0 && testRunner.Skin <= 3, "spawn-skin", $"skin={testRunner.Skin}");
        Check(testRunner.HomeBush == bush.Tile, "spawn-homebush", $"home={testRunner.HomeBush} bush={bush.Tile}");
        SnapCam(testRunner.Position);
        NextPhase();
    }

    // wrong bush first (must not startle), then the home bush
    // through the real engine path (exercises the Harmony hook).
    private void TestTrigger()
    {
        GameLocation location = Game1.currentLocation;
        if (testRunner is not Critterz r)
        {
            GiveUp("trigger", "no test runner");
            return;
        }
        if (testPhaseTicks == 1)
        {
            Bush? other = BushHelper.GetAvailableBushes(location).FirstOrDefault(b => b.Tile != r.HomeBush);
            if (other != null)
                other.shake(other.Tile, true);
        }
        if (testPhaseTicks == 60)
        {
            if (r.State != CritterzState.Sitting)
            {
                GiveUp("trigger-homebush-only", $"state={r.State} after wrong-bush shake");
                return;
            }
            Check(true, "trigger-homebush-only");
            Bush? bush = BushHelper.GetAvailableBushes(location).FirstOrDefault(b => b.Tile == r.HomeBush);
            if (bush == null)
            {
                GiveUp("trigger", "home bush missing");
                return;
            }
            testLeavesBefore = TreeHelper.leaves.Count;
            bush.shake(bush.Tile, true);
            SnapCam(r.Position);
            return;
        }
        if (testPhaseTicks < 60)
            return;
        if (r.State == CritterzState.Running && r.Path != null)
        {
            Check(r.DestBush != null, "trigger-dest", "no destination selected");
            Check(TreeHelper.leaves.Count > testLeavesBefore, "trigger-leaves", "no burst leaves spawned");
            if (r.DestBush is Vector2 dest)
                testDestBush = dest;
            CheckPath(r, location);
            NextPhase();
        }
        else if (r.State == CritterzState.Sitting && testPhaseTicks > 240)
            GiveUp("trigger", "still sitting (hook or path failed)");
        else if (testPhaseTicks > 720)
            GiveUp("trigger", $"stuck (state={r.State}, path={(r.Path == null ? "null" : r.Path.Count.ToString())})");
    }

    // Pathseeking audit: tile-center waypoints stay on reachable tiles
    // (endpoints exempt), every leg stays in the 60-degree cone around the
    // x-axis (no straight up/down), and the path ends at the dest bush.
    private void CheckPath(Critterz r, GameLocation location)
    {
        List<Vector2> points = new List<Vector2> { r.HomeBush * Game1.tileSize + new Vector2(32f, 32f) };
        if (r.Path != null)
            points.AddRange(r.Path);
        if (points.Count < 2)
        {
            Check(false, "path-legal-tiles", "empty path");
            return;
        }
        HashSet<Vector2> reachable = GetReachableTiles(location);
        int badTiles = 0, badCone = 0;
        for (int i = 1; i < points.Count; i++)
        {
            Vector2 a = points[i - 1], b = points[i];
            float dx = b.X - a.X, dy = b.Y - a.Y;
            Vector2 btile = new Vector2((float)Math.Floor(b.X / Game1.tileSize), (float)Math.Floor(b.Y / Game1.tileSize));
            bool endpoint = btile == r.HomeBush || (r.DestBush is Vector2 dest && btile == dest);
            if (!endpoint)
            {
                // Zigzag midpoints hug the corridor (X on a tile boundary);
                // tile centers must be reachable outright.
                float xm = Math.Abs(b.X % 64f);
                if (xm < 0.01f || xm > 63.99f)
                {
                    bool near = false;
                    for (int ox = -1; ox <= 1 && !near; ox++)
                        for (int oy = -1; oy <= 1 && !near; oy++)
                            near = reachable.Contains(btile + new Vector2(ox, oy));
                    if (!near)
                        badTiles++;
                }
                else if (!reachable.Contains(btile))
                    badTiles++;
            }
            float run = Math.Abs(dx), rise = Math.Abs(dy);
            if (Math.Sqrt(dx * dx + dy * dy) < 0.5f || run < 0.5f || rise / run > 1.74f)
                badCone++;
        }
        Check(badTiles == 0, "path-legal-tiles", $"{badTiles} bad waypoints of {points.Count - 1}");
        Check(badCone == 0, "path-cone", $"{badCone} legs outside the 60-degree cone");
        Vector2 end = points[points.Count - 1];
        float endDist = r.DestBush is Vector2 d
            ? Vector2.Distance(end, d * Game1.tileSize + new Vector2(32f, 32f)) : float.MaxValue;
        Check(endDist < 2f, "path-endpoint", $"ends {endDist}px from dest bush");
    }

    // wait for arrival at the destination bush.
    private void TestRun()
    {
        if (testRunner is not Critterz r)
        {
            GiveUp("arrive", "no test runner");
            return;
        }
        TrackCam(r.Position);
        if (r.State == CritterzState.Sitting)
        {
            Check(r.HomeBush == testDestBush, "arrive-homebush", $"home={r.HomeBush} dest={testDestBush}");
            float dist = Vector2.Distance(r.Position, testDestBush * Game1.tileSize + new Vector2(32f, 32f));
            Check(dist < 2f, "arrive-position", $"off by {dist}px");
            NextPhase();
        }
        else if (testPhaseTicks > 3600)
            GiveUp("arrive", $"still running after 60s (waypoints={r.Path?.Count})");
    }

    // loot shape + real engine kill (damageMonster, bomb hitbox).
    private void TestKill()
    {
        GameLocation location = Game1.currentLocation;
        if (testPhaseTicks == 1)
        {
            Bush? bush = BushHelper.GetAvailableBushes(location)
                .Where(b => testRunner == null || b.Tile != testRunner.HomeBush)
                .FirstOrDefault(b => HasReachableExit(b.Tile, GetReachableTiles(location)));
            if (bush == null)
            {
                GiveUp("kill", "no second bush");
                return;
            }
            testVictim = SpawnCritterz(location, bush.Tile, false, bush.Tile);
            testCritters.Add(testVictim);
            TriggerCritterz(testVictim);
            SnapCam(testVictim.Position);
        }
        if (testVictim is not Critterz v)
        {
            GiveUp("kill", "no victim");
            return;
        }
        if (v.State != CritterzState.Running || v.Path == null)
        {
            if (testPhaseTicks > 600)
                GiveUp("kill", "victim never ran");
            return;
        }
        // Loot shape first (pre-rolled at spawn).
        string[] pool = v.Skin switch
        {
            Constants.GreyBunnyRow or Constants.WhiteBunnyRow => new[] { "78", "440", "446" },
            Constants.SquirrelRow => new[] { "408", "309", "311" },
            _ => new[] { "168", "169", "170", "171", "172" },
        };
        List<string> loot = v.ObjectToDrop;
        Check(loot.All(id => pool.Contains(id)), "kill-loot-shape", $"skin={v.Skin} loot=[{string.Join(",", loot)}]");
        SnapCam(v.Position);
        // Real kill through the engine (retry: 10% miss chance).
        int debrisBefore = location.debris.Count;
        for (int i = 0; i < 25 && v.Health > 0; i++)
            location.damageMonster(v.GetBoundingBox(), 1000, 1000, true, Game1.player, false);
        Check(v.Health <= 0, "kill-dead", $"health={v.Health}");
        Check(location.debris.Count >= debrisBefore + loot.Count, "kill-debris",
            $"debris grew by {location.debris.Count - debrisBefore}, loot had {loot.Count}");
        NextPhase();
    }

    // perch a grouse in a free tree.
    private void TestGrouseSpawn()
    {
        GameLocation location = Game1.currentLocation;
        if (GetNPCsAtCurrentLocation() is not NetCollection<NPC> npc)
        {
            GiveUp("g-spawn", "no NPC list");
            return;
        }
        var occupied = location.characters.OfType<Grouse>().Select(g => g.TreePosition).ToHashSet();
        Tree? tree = TreeHelper.GetAvailableTreePositions(location)
            .FirstOrDefault(t => !occupied.Contains(TreeHelper.GetTreePosition(t)));
        if (tree == null)
        {
            GiveUp("g-spawn", "no free tree in Forest");
            return;
        }
        testGrouseStartTree = TreeHelper.GetTreePosition(tree);
        testFlyer = SpawnGrouse(npc, testGrouseStartTree, TreeHelper.GetGrouseSpawnPosition(tree), location, null, false);
        testGrouse.Add(testFlyer);
        Check(testFlyer.State == GrouseState.Perched, "g-spawn-perched", $"state={testFlyer.State}");
        Check(testFlyer.TreePosition == testGrouseStartTree, "g-spawn-tree", $"tree={testFlyer.TreePosition}");
        Check(testFlyer.Texture != null, "g-spawn-texture", $"texture={testFlyer.TextureName}");
        string[] eggs = { "174", "176", "305", "928" };
        testGrouseEggs = testFlyer.InitialObjectToDrop.Count;
        Check(testFlyer.InitialObjectToDrop.All(id => eggs.Contains(id)), "g-spawn-eggs",
            $"eggs=[{string.Join(",", testFlyer.InitialObjectToDrop)}]");
        testGrouseDebrisBefore = location.debris.Count;
        SnapCam(testFlyer.Position);
        NextPhase();
    }

    // shake the tree through the real engine path (exercises the
    // Harmony hook) and wait for the surprise.
    private void TestGrouseTrigger()
    {
        GameLocation location = Game1.currentLocation;
        if (testFlyer is not Grouse g)
        {
            GiveUp("g-trigger", "no test grouse");
            return;
        }
        if (testPhaseTicks == 1)
        {
            Tree? other = TreeHelper.GetAvailableTreePositions(location)
                .FirstOrDefault(t => TreeHelper.GetTreePosition(t) != g.TreePosition);
            if (other != null)
                other.shake(other.Tile, true);
        }
        if (testPhaseTicks == 60)
        {
            if (g.State != GrouseState.Perched)
            {
                GiveUp("g-trigger-hometree-only", $"state={g.State} after wrong-tree shake");
                return;
            }
            Check(true, "g-trigger-hometree-only");
            if (TreeHelper.GetTreeFromId(location, g.TreePosition) is not Tree tree)
            {
                GiveUp("g-trigger", "home tree missing");
                return;
            }
            tree.shake(tree.Tile, true);
            SnapCam(g.Position);
            return;
        }
        if (testPhaseTicks < 60)
            return;
        if (g.State == GrouseState.Surprised || g.State == GrouseState.Flushing || g.State == GrouseState.Flying)
            NextPhase();
        else if (testPhaseTicks > 660)
            GiveUp("g-trigger", $"still perched (state={g.State})");
    }

    // wait for the flush and its egg drop.
    private void TestGrouseFlush()
    {
        GameLocation location = Game1.currentLocation;
        if (testFlyer is not Grouse g)
        {
            GiveUp("g-flush", "no test grouse");
            return;
        }
        TrackCam(g.Position);
        if (g.State == GrouseState.Flushing || g.State == GrouseState.Flying || g.State == GrouseState.Landing
            || (g.State == GrouseState.Perched && g.TreePosition != testGrouseStartTree))
        {
            Check(g.InitialObjectToDrop.Count == 0, "g-flush-eggs-drained",
                $"eggs left=[{string.Join(",", g.InitialObjectToDrop)}]");
            Check(location.debris.Count >= testGrouseDebrisBefore + testGrouseEggs, "g-flush-debris",
                $"debris grew by {location.debris.Count - testGrouseDebrisBefore}, eggs had {testGrouseEggs}");
            testGrouseLeavesBefore = TreeHelper.leaves.Count;
            NextPhase();
        }
        else if (testPhaseTicks > 600)
            GiveUp("g-flush", $"never flushed (state={g.State})");
        else
        {
            // Rolling baseline: old debris expires or gets vacuumed up while
            // we wait out the surprise, so compare against last tick.
            testGrouseDebrisBefore = location.debris.Count;
        }
    }

    // wait for landing at a new tree.
    private void TestGrouseLand()
    {
        if (testFlyer is not Grouse g)
        {
            GiveUp("g-land", "no test grouse");
            return;
        }
        TrackCam(g.Position);
        if (g.State == GrouseState.Perched)
        {
            Check(g.TreePosition != testGrouseStartTree, "g-land-newtree",
                $"tree={g.TreePosition} start={testGrouseStartTree}");
            Check(TreeHelper.GetTreeFromId(Game1.currentLocation, g.TreePosition) is Tree, "g-land-tree-exists",
                $"tree={g.TreePosition}");
            Check(TreeHelper.leaves.Count > testGrouseLeavesBefore, "g-land-leaves", "no landing leaves spawned");
            NextPhase();
        }
        else if (testPhaseTicks > 3600)
            GiveUp("g-land", $"never landed (state={g.State})");
    }

    // snack shape + real engine kill (damageMonster, bomb hitbox).
    private void TestGrouseKill()
    {
        GameLocation location = Game1.currentLocation;
        if (testPhaseTicks == 1)
        {
            if (GetNPCsAtCurrentLocation() is not NetCollection<NPC> npc)
            {
                GiveUp("g-kill", "no NPC list");
                return;
            }
            var occupied = location.characters.OfType<Grouse>().Select(g => g.TreePosition).ToHashSet();
            Tree? tree = TreeHelper.GetAvailableTreePositions(location)
                .FirstOrDefault(t => !occupied.Contains(TreeHelper.GetTreePosition(t)));
            if (tree == null)
            {
                GiveUp("g-kill", "no free tree");
                return;
            }
            testGrouseVictim = SpawnGrouse(npc, TreeHelper.GetTreePosition(tree),
                TreeHelper.GetGrouseSpawnPosition(tree), location, null, false);
            testGrouse.Add(testGrouseVictim);
            testGrouseVictim.State = GrouseState.Flying;
            SnapCam(testGrouseVictim.Position);
        }
        if (testGrouseVictim is not Grouse v)
        {
            GiveUp("g-kill", "no victim");
            return;
        }
        List<string> loot = v.ObjectToDrop;
        Check(loot.All(id => !string.IsNullOrEmpty(id)), "g-kill-loot-shape", $"loot=[{string.Join(",", loot)}]");
        int debrisBefore = location.debris.Count;
        for (int i = 0; i < 25 && v.Health > 0; i++)
            location.damageMonster(v.GetBoundingBox(), 1000, 1000, true, Game1.player, false);
        Check(v.Health <= 0, "g-kill-dead", $"health={v.Health}");
        Check(location.debris.Count >= debrisBefore + loot.Count, "g-kill-debris",
            $"debris grew by {location.debris.Count - debrisBefore}, loot had {loot.Count}");
        NextPhase();
    }

    // audit every auto-spawned animal in every outdoor location:
    // valid homes, reachable exits, caps, banned locations.
    private void TestAuditSpawns()
    {
        int badBush = 0, badReach = 0, critterzOver = 0, bannedCritterz = 0, badTree = 0, badTreeLegal = 0, grouseOver = 0;
        foreach (GameLocation loc in Game1.locations.Where(l => l.IsOutdoors))
        {
            List<Critterz> critters = loc.characters.OfType<Critterz>().Where(r => !testCritters.Contains(r)).ToList();
            if (critters.Count > Constants.CritterzMaxPerLocation)
                critterzOver++;
            if (loc is Farm || loc is StardewValley.Locations.Desert)
                bannedCritterz += critters.Count;
            HashSet<Vector2> reachable = GetReachableTiles(loc);
            var bushTiles = BushHelper.GetAvailableBushes(loc).Select(b => b.Tile).ToHashSet();
            foreach (Critterz r in critters)
            {
                if (!bushTiles.Contains(r.HomeBush))
                    badBush++;
                else if (!HasReachableExit(r.HomeBush, reachable))
                    badReach++;
            }
            List<Grouse> grouse = loc.characters.OfType<Grouse>().Where(g => !testGrouse.Contains(g)).ToList();
            if (grouse.Count > Constants.GrouseMaxPerLocation)
                grouseOver++;
            foreach (Grouse g in grouse)
            {
                if (TreeHelper.GetTreeFromId(loc, g.TreePosition) is not Tree tree)
                    badTree++;
                else if (!TreeHelper.GetLegalTree(tree))
                    badTreeLegal++;
            }
        }
        Check(badBush == 0, "audit-critterz-bushes", $"{badBush} homed at non-bushes");
        Check(badReach == 0, "audit-critterz-reachable", $"{badReach} without reachable exits");
        Check(critterzOver == 0, "audit-critterz-cap", $"{critterzOver} locations over cap");
        Check(bannedCritterz == 0, "audit-critterz-banned", $"{bannedCritterz} on farm/desert");
        Check(badTree == 0, "audit-grouse-trees", $"{badTree} perched at non-trees");
        Check(badTreeLegal == 0, "audit-grouse-legal", $"{badTreeLegal} on illegal trees");
        Check(grouseOver == 0, "audit-grouse-cap", $"{grouseOver} locations over cap");
        NextPhase();
    }

    // damage rules per state and weapon.
    private void TestDamageRules()
    {
        GameLocation location = Game1.currentLocation;
        if (testPhaseTicks == 1)
        {
            Bush? bush = NearestReachableBush(location, Game1.player.Tile);
            if (bush == null)
            {
                GiveUp("dmg", "no bush");
                return;
            }
            testSitterA = SpawnCritterz(location, bush.Tile, false, bush.Tile);
            testRunnerB = SpawnCritterz(location, bush.Tile, false, bush.Tile);
            TriggerCritterz(testRunnerB);
            testCritters.Add(testSitterA);
            testCritters.Add(testRunnerB);
            Tree? tree = TreeHelper.GetAvailableTreePositions(location).FirstOrDefault();
            if (tree == null || GetNPCsAtCurrentLocation() is not NetCollection<NPC> npc)
            {
                GiveUp("dmg", "no tree");
                return;
            }
            testGrouseDmg = SpawnGrouse(npc, TreeHelper.GetTreePosition(tree),
                TreeHelper.GetGrouseSpawnPosition(tree), location, null, false);
            testGrouse.Add(testGrouseDmg);
            // Hidden critter: bombs do nothing.
            for (int i = 0; i < 3; i++)
                testSitterA.takeDamage(1000, 0, 0, true, 1.0, Game1.player);
            Check(testSitterA.Health == Constants.CritterzMaxHealth, "dmg-hidden-immune",
                $"health={testSitterA.Health}");
            // Perched, then surprised, grouse: bombs do nothing.
            for (int i = 0; i < 3; i++)
                testGrouseDmg.takeDamage(1000, 0, 0, true, 1.0, Game1.player);
            Check(testGrouseDmg.Health == Constants.GrouseMaxHealth, "dmg-perched-immune",
                $"health={testGrouseDmg.Health}");
            testGrouseDmg.State = GrouseState.Surprised;
            for (int i = 0; i < 3; i++)
                testGrouseDmg.takeDamage(1000, 0, 0, true, 1.0, Game1.player);
            Check(testGrouseDmg.Health == Constants.GrouseMaxHealth, "dmg-surprised-immune",
                $"health={testGrouseDmg.Health}");
        }
        if (testRunnerB is not Critterz b || testGrouseDmg is not Grouse gd)
        {
            GiveUp("dmg", "missing subjects");
            return;
        }
        if (b.State != CritterzState.Running || b.Path == null)
        {
            if (testPhaseTicks > 600)
                GiveUp("dmg", "runner never ran");
            return;
        }
        // Wrong weapon (empty hand) on a running critter: no damage.
        Game1.player.CurrentTool = null;
        for (int i = 0; i < 3; i++)
            b.takeDamage(1000, 0, 0, false, 1.0, Game1.player);
        Check(b.Health == Constants.CritterzMaxHealth, "dmg-wrong-weapon", $"health={b.Health}");
        // Legal bomb on a running critter: damage.
        for (int i = 0; i < 25 && b.Health >= Constants.CritterzMaxHealth; i++)
            b.takeDamage(1000, 0, 0, true, 1.0, Game1.player);
        Check(b.Health < Constants.CritterzMaxHealth, "dmg-legal-bomb", $"health={b.Health}");
        // Flying grouse: damage.
        gd.State = GrouseState.Flying;
        for (int i = 0; i < 25 && gd.Health >= Constants.GrouseMaxHealth; i++)
            gd.takeDamage(1000, 0, 0, true, 1.0, Game1.player);
        Check(gd.Health < Constants.GrouseMaxHealth, "dmg-flying-vulnerable", $"health={gd.Health}");
        NextPhase();
    }

    // bush fx helper, flush snacks, off-location predicate.
    private void TestFxAndSnacks()
    {
        GameLocation location = Game1.currentLocation;
        Bush? bush = BushHelper.GetAvailableBushes(location).FirstOrDefault();
        if (bush == null)
        {
            GiveUp("fx", "no bush");
            return;
        }
        int leavesBefore = TreeHelper.leaves.Count;
        PlayCritterzBushFx(location, bush);
        Check(TreeHelper.leaves.Count > leavesBefore, "fx-leaves", "no leaves spawned");
        try
        {
            float maxShake = Helper.Reflection.GetField<float>(bush, "maxShake").GetValue();
            Check(maxShake > 0f, "fx-wobble", $"maxShake={maxShake}");
        }
        catch (Exception ex)
        {
            Check(false, "fx-wobble", $"reflection failed: {ex.Message}");
        }
        if (testRunner is not Critterz r)
        {
            GiveUp("fx", "no runner");
            return;
        }
        int debrisBefore = location.debris.Count;
        for (int i = 0; i < 50; i++)
            DropCritterzSnacks(r, location);
        Check(location.debris.Count > debrisBefore, "flush-snacks", "50 bursts dropped nothing");
        Vector2 savedPos = r.Position;
        Check(!Critterz.IsCritterzOffLocation(r, location), "bounds-home", "home flagged off-location");
        r.Position = new Vector2(-1000f, -1000f);
        Check(Critterz.IsCritterzOffLocation(r, location), "bounds-away", "(-1000,-1000) not flagged");
        r.Position = savedPos;
        NextPhase();
    }

    // S1: second full cycle on the same critter.
    private void TestSecondCycle()
    {
        GameLocation location = Game1.currentLocation;
        if (testRunner is not Critterz r)
        {
            GiveUp("cycle2", "no test runner");
            return;
        }
        if (testPhaseTicks == 1)
        {
            if (r.State != CritterzState.Sitting)
            {
                GiveUp("cycle2", $"runner not sitting (state={r.State})");
                return;
            }
            testCycleHome = r.HomeBush;
            testCycleSawRun = false;
            Bush? bush = BushHelper.GetAvailableBushes(location).FirstOrDefault(b => b.Tile == r.HomeBush);
            if (bush == null)
            {
                GiveUp("cycle2", "home bush missing");
                return;
            }
            bush.shake(bush.Tile, true);
            SnapCam(r.Position);
            return;
        }
        if (r.State == CritterzState.Running && r.Path != null)
            testCycleSawRun = true;
        TrackCam(r.Position);
        if (testCycleSawRun && r.State == CritterzState.Sitting)
        {
            Check(r.HomeBush != testCycleHome, "cycle2-home", $"home={r.HomeBush} (unchanged)");
            Check(r.modData.ContainsKey("Junimatic.NotScary"), "notscary-critterz", "modData missing");
            NextPhase();
        }
        else if (testPhaseTicks > 4200)
            GiveUp("cycle2", $"stuck (state={r.State}, sawRun={testCycleSawRun})");
    }

    // S2: miss rate over 200 rolls per species (healed between hits).
    private void TestMissRate()
    {
        GameLocation location = Game1.currentLocation;
        if (testPhaseTicks == 1)
        {
            Bush? bush = NearestReachableBush(location, Game1.player.Tile);
            if (bush == null)
            {
                GiveUp("miss", "no bush");
                return;
            }
            testMissCritterz = SpawnCritterz(location, bush.Tile, false, bush.Tile);
            testCritters.Add(testMissCritterz);
            TriggerCritterz(testMissCritterz);
            SnapCam(testMissCritterz.Position);
        }
        if (testMissCritterz is not Critterz b)
        {
            GiveUp("miss", "missing critterz");
            return;
        }
        if (b.State != CritterzState.Running || b.Path == null)
        {
            if (testPhaseTicks > 600)
                GiveUp("miss", "runner never ran");
            return;
        }
        int hits = 0;
        for (int i = 0; i < 200; i++)
        {
            b.Health = Constants.CritterzMaxHealth;
            if (b.takeDamage(1, 0, 0, true, 0.0, Game1.player) != -1)
                hits++;
        }
        Check(hits >= 160 && hits <= 195, "miss-rate", $"{hits}/200 hit");
        Tree? tree = TreeHelper.GetAvailableTreePositions(location).FirstOrDefault();
        if (tree == null || GetNPCsAtCurrentLocation() is not NetCollection<NPC> npc)
        {
            GiveUp("miss", "no tree");
            return;
        }
        testMissGrouse = SpawnGrouse(npc, TreeHelper.GetTreePosition(tree),
            TreeHelper.GetGrouseSpawnPosition(tree), location, null, false);
        testGrouse.Add(testMissGrouse);
        testMissGrouse.State = GrouseState.Flying;
        int ghits = 0;
        for (int i = 0; i < 200; i++)
        {
            testMissGrouse.Health = Constants.GrouseMaxHealth;
            if (testMissGrouse.takeDamage(1, 0, 0, true, 0.0, Game1.player) != -1)
                ghits++;
        }
        Check(ghits >= 160 && ghits <= 195, "miss-rate-grouse", $"{ghits}/200 hit");
        NextPhase();
    }

    // S3: perched grouse cycles between hidden and exposed.
    private void TestHiding()
    {
        GameLocation location = Game1.currentLocation;
        if (testPhaseTicks == 1)
        {
            if (GetNPCsAtCurrentLocation() is not NetCollection<NPC> npc)
            {
                GiveUp("hiding", "no NPC list");
                return;
            }
            Tree? tree = TreeHelper.GetAvailableTreePositions(location).FirstOrDefault();
            if (tree == null)
            {
                GiveUp("hiding", "no tree");
                return;
            }
            testHider = SpawnGrouse(npc, TreeHelper.GetTreePosition(tree),
                TreeHelper.GetGrouseSpawnPosition(tree), location, null, false);
            testGrouse.Add(testHider);
            testHideSeenHidden = false;
            testHideSeenExposed = false;
            SnapCam(testHider.Position);
        }
        if (testHider is not Grouse g)
        {
            GiveUp("hiding", "no hider");
            return;
        }
        testHideSeenHidden |= g.IsHiding;
        testHideSeenExposed |= !g.IsHiding;
        if (testHideSeenHidden && testHideSeenExposed)
        {
            Check(true, "hiding-cycles");
            NextPhase();
        }
        else if (testPhaseTicks > 3600)
            GiveUp("hiding-cycles", $"hidden={testHideSeenHidden} exposed={testHideSeenExposed}");
    }

    // S3: second full flight on the same grouse.
    private void TestSecondFlight()
    {
        GameLocation location = Game1.currentLocation;
        if (testFlyer is not Grouse g)
        {
            GiveUp("flight2", "no test grouse");
            return;
        }
        if (testPhaseTicks == 1)
        {
            if (g.State != GrouseState.Perched)
            {
                GiveUp("flight2", $"grouse not perched (state={g.State})");
                return;
            }
            testFlightTree = g.TreePosition;
            testFlightSawAir = false;
            if (TreeHelper.GetTreeFromId(location, g.TreePosition) is not Tree tree)
            {
                GiveUp("flight2", "home tree missing");
                return;
            }
            tree.shake(tree.Tile, true);
            SnapCam(g.Position);
            return;
        }
        if (g.State != GrouseState.Perched)
            testFlightSawAir = true;
        TrackCam(g.Position);
        if (testFlightSawAir && g.State == GrouseState.Perched)
        {
            Check(g.TreePosition != testFlightTree, "flight2-tree", $"tree={g.TreePosition} (unchanged)");
            Check(g.modData.ContainsKey("Junimatic.NotScary"), "notscary-grouse", "modData missing");
            NextPhase();
        }
        else if (testPhaseTicks > 4200)
            GiveUp("flight2", $"stuck (state={g.State}, sawAir={testFlightSawAir})");
    }

    // S5: bunny skin follows the season; proximity alone never startles.
    private void TestSkinSeason()
    {
        GameLocation location = Game1.currentLocation;
        if (testPhaseTicks == 1)
        {
            Bush? bush = NearestReachableBush(location, Game1.player.Tile);
            if (bush == null)
            {
                GiveUp("skin", "no bush");
                return;
            }
            string savedSeason = Game1.currentSeason;
            try
            {
                Game1.currentSeason = "winter";
                if (!location.IsWinterHere())
                {
                    GiveUp("skin", "location not winter in winter");
                    return;
                }
                List<int> winterSkins = new();
                for (int i = 0; i < 30; i++)
                {
                    Critterz c = SpawnCritterz(location, bush.Tile, false, bush.Tile);
                    winterSkins.Add(c.Skin);
                    testSkinCritters.Add(c);
                }
                Check(!winterSkins.Contains(Constants.GreyBunnyRow), "skin-winter-no-grey",
                    $"skins=[{string.Join(",", winterSkins)}]");
                Check(winterSkins.Contains(Constants.WhiteBunnyRow), "skin-winter-white", "no white bunny in 30");
                Game1.currentSeason = savedSeason;
                List<int> mildSkins = new();
                for (int i = 0; i < 30; i++)
                {
                    Critterz c = SpawnCritterz(location, bush.Tile, false, bush.Tile);
                    mildSkins.Add(c.Skin);
                    testSkinCritters.Add(c);
                }
                Check(!mildSkins.Contains(Constants.WhiteBunnyRow), "skin-mild-no-white",
                    $"skins=[{string.Join(",", mildSkins)}]");
                Check(mildSkins.Contains(Constants.GreyBunnyRow), "skin-mild-grey", "no grey bunny in 30");
                var all = winterSkins.Concat(mildSkins).ToList();
                Check(all.Contains(Constants.SquirrelRow) && all.Contains(Constants.RaccoonRow), "skin-variety",
                    "missing squirrel or raccoon in 60");
            }
            finally
            {
                Game1.currentSeason = savedSeason;
            }
            Game1.player.Position = bush.Tile * Game1.tileSize + new Vector2(32f, 32f);
            SnapCam(bush.Tile * Game1.tileSize + new Vector2(32f, 32f));
        }
        if (testPhaseTicks >= 300)
        {
            Check(testSkinCritters.All(c => c.State == CritterzState.Sitting), "no-proximity",
                $"{testSkinCritters.Count(c => c.State != CritterzState.Sitting)} emerged unprovoked");
            NextPhase();
        }
    }

    // S5: every bush occupied means ducking back silently.
    private void TestNowhereToRun()
    {
        GameLocation location = Game1.currentLocation;
        if (testPhaseTicks == 1)
        {
            List<Bush> bushes = BushHelper.GetAvailableBushes(location);
            if (bushes.Count == 0)
            {
                GiveUp("nowhere", "no bushes");
                return;
            }
            Bush home = NearestReachableBush(location, Game1.player.Tile) ?? bushes[0];
            foreach (Bush b in bushes)
                SpawnCritterz(location, b.Tile, false, b.Tile);
            testNowhere = SpawnCritterz(location, home.Tile, false, home.Tile);
            TriggerCritterz(testNowhere);
            SnapCam(home.Tile * Game1.tileSize + new Vector2(32f, 32f));
            return;
        }
        if (testNowhere is not Critterz v)
        {
            GiveUp("nowhere", "no victim");
            return;
        }
        if (v.State == CritterzState.Running && v.Path != null)
        {
            GiveUp("nowhere-ducked", "ran despite full bushes");
            return;
        }
        if (v.State == CritterzState.Sitting && v.Path == null && testPhaseTicks > 10)
        {
            Check(v.DestBush == null, "nowhere-no-dest", $"dest={v.DestBush}");
            Check(true, "nowhere-ducked");
            NextPhase();
        }
        else if (testPhaseTicks > 240)
            GiveUp("nowhere", "stuck");
    }

    // S5: launched grouse carry no drops; debug commands work.
    private void TestLaunchedAndCommands()
    {
        GameLocation location = Game1.currentLocation;
        if (GetNPCsAtCurrentLocation() is not NetCollection<NPC> npc)
        {
            GiveUp("launched", "no NPC list");
            return;
        }
        Tree? tree = TreeHelper.GetAvailableTreePositions(location).FirstOrDefault();
        if (tree == null)
        {
            GiveUp("launched", "no tree");
            return;
        }
        Grouse launched = SpawnGrouse(npc, TreeHelper.GetTreePosition(tree),
            TreeHelper.GetGrouseSpawnPosition(tree), location, null, true);
        Check(launched.InitialObjectToDrop.Count == 0 && launched.ObjectToDrop.Count == 0, "launched-no-drops",
            $"eggs={launched.InitialObjectToDrop.Count} snacks={launched.ObjectToDrop.Count}");
        int critBefore = location.characters.OfType<Critterz>().Count();
        SpawnDebugCritterz();
        Check(location.characters.OfType<Critterz>().Count() == critBefore + 1, "cmd-spawn-critterz", "no critterz spawned");
        int grouseBefore = location.characters.OfType<Grouse>().Count();
        SpawnDebugGrouse();
        Check(location.characters.OfType<Grouse>().Count() == grouseBefore + 1, "cmd-spawn-grouse", "no grouse spawned");
        NextPhase();
    }

    // S6: fog forecast, season table, config gates, warp reset.
    private void TestFog()
    {
        GameLocation location = Game1.currentLocation;
        if (testPhaseTicks == 1)
        {
            var f1 = ComputeFogForecast(100);
            var f2 = ComputeFogForecast(100);
            Check(f1.IsFogDay == f2.IsFogDay && f1.DailyFogStrength == f2.DailyFogStrength
                && f1.ProbabilityOfFogForADay == f2.ProbabilityOfFogForADay
                && f1.ProbabilityOfFogRoll == f2.ProbabilityOfFogRoll, "fog-forecast-deterministic", "same input diverged");
            Check(f1.ProbabilityOfFogRoll >= 0f && f1.ProbabilityOfFogRoll <= 1f
                && f1.ProbabilityOfFogForADay >= 0f && f1.ProbabilityOfFogForADay <= 1f
                && f1.DailyFogStrength >= Constants.DailyRandomFogMin
                && f1.DailyFogStrength <= Constants.DailyRandomFogMax, "fog-forecast-bounds", "out of range");
            string savedSeason = Game1.currentSeason;
            try
            {
                Game1.currentSeason = "spring";
                float spring = ComputeProbabilityOfFogForADay();
                Game1.currentSeason = "summer";
                float summer = ComputeProbabilityOfFogForADay();
                Game1.currentSeason = "fall";
                float fall = ComputeProbabilityOfFogForADay();
                Game1.currentSeason = "winter";
                float winter = ComputeProbabilityOfFogForADay();
                Game1.currentSeason = "void";
                float def = ComputeProbabilityOfFogForADay();
                Check(spring == 0.05f && summer == 0.1f && fall == 0.2f && winter == 0.18f && def == 0.08f,
                    "fog-season-table", $"s={spring} su={summer} f={fall} w={winter} d={def}");
            }
            finally
            {
                Game1.currentSeason = savedSeason;
            }
            bool enableFog = Config.EnableFog, enableBank = Config.EnableFogBank, enableDaily = Config.EnableDailyRandomFog;
            try
            {
                Config.EnableFog = false;
                Config.EnableFogBank = false;
                InitializeDailyFogStrength();
                Check(!isFogDay, "fog-config-off", "fog day with fog disabled");
                Config.EnableFog = true;
                Config.EnableFogBank = true;
                Config.EnableDailyRandomFog = false;
                InitializeDailyFogStrength();
                Check(isFogDay, "fog-config-always", "no fog day with randomness off");
            }
            finally
            {
                Config.EnableFog = enableFog;
                Config.EnableFogBank = enableBank;
                Config.EnableDailyRandomFog = enableDaily;
                InitializeDailyFogStrength();
            }
            Game1.warpFarmer("FarmHouse", 5, 5, false);
            return;
        }
        if (Game1.currentLocation?.NameOrUniqueName == "FarmHouse" && !testFogResetChecked)
        {
            testFogResetChecked = true;
            Check(floatingParticles.Count == 0, "fog-reset-particles", $"count={floatingParticles.Count}");
            Check(explosionSmokeParticles.Count == 0, "fog-reset-smoke", $"count={explosionSmokeParticles.Count}");
            Check(TreeHelper.leaves.Count == 0, "fog-reset-leaves", $"count={TreeHelper.leaves.Count}");
            Game1.warpFarmer("Forest", 50, 30, false);
            return;
        }
        if (testFogResetChecked && Game1.currentLocation?.NameOrUniqueName == "Forest")
            NextPhase();
        else if (testPhaseTicks > 1500)
            GiveUp("fog", "warp stuck");
    }

    // S6: scattergun fire through the real engine path.
    private void TestScattergun()
    {
        GameLocation location = Game1.currentLocation;
        SnapCam(Game1.player.Position);
        GiveScattergunToPlayer();
        Slingshot? gun = Game1.player.Items.OfType<Slingshot>().FirstOrDefault(s => IsScattergunId(s.ItemId));
        if (gun == null)
        {
            GiveUp("scatter-give", "not in inventory (full?)");
            return;
        }
        Check(true, "scatter-give");
        gun.attachments[0] = new StardewValley.Object("390", 10);
        gun.aimPos.Value = new Point(1000, 500);
        gun.pullStartTime = Game1.currentGameTime.TotalGameTime.TotalSeconds - 5.0;
        int projBefore = location.projectiles.Count;
        int flashBefore = explosionFlashInfos.Count;
        int smokeBefore = explosionSmokeParticles.Count;
        gun.PerformFire(location, Game1.player);
        int expectPellets = Config.MultiSlingShotPellets ? Constants.GrouseRewardItemMaxPellets : 1;
        Check(location.projectiles.Count == projBefore + expectPellets, "scatter-pellets",
            $"fired {location.projectiles.Count - projBefore}, expected {expectPellets}");
        Check(gun.attachments[0] != null && gun.attachments[0].Stack == 10 - expectPellets, "scatter-ammo",
            $"stack={gun.attachments[0]?.Stack}");
        Check(explosionFlashInfos.Count == flashBefore + 1, "scatter-flash",
            $"flashes grew by {explosionFlashInfos.Count - flashBefore}");
        Check(explosionSmokeParticles.Count == smokeBefore + ScattergunConfig.SmokeCount, "scatter-smoke",
            $"smoke grew by {explosionSmokeParticles.Count - smokeBefore}");
        gun.pullStartTime = Game1.currentGameTime.TotalGameTime.TotalSeconds;
        int proj2 = location.projectiles.Count, flash2 = explosionFlashInfos.Count, smoke2 = explosionSmokeParticles.Count;
        int ammoBefore = gun.attachments[0]?.Stack ?? -1;
        gun.PerformFire(location, Game1.player);
        Check(location.projectiles.Count == proj2 && explosionFlashInfos.Count == flash2
            && explosionSmokeParticles.Count == smoke2 && (gun.attachments[0]?.Stack ?? -2) == ammoBefore,
            "scatter-uncharged-noop", "uncharged shot had effects");
        NextPhase();
    }

    // report by scenario and exit without saving.
    private void TestFinish()
    {
        testPhase = 99; // run once
        var lines = new List<string>();
        for (int i = 0; i < TestScenarios.Length; i++)
        {
            var checks = testResults.Where(r => ScenarioForPhase(r.Phase) == i).ToList();
            int passed = checks.Count(r => r.Line.StartsWith("PASS"));
            lines.Add($"## Scenario {i + 1}/{TestScenarios.Length}: {TestScenarios[i].Name} ({passed}/{checks.Count})");
            lines.AddRange(checks.Select(r => r.Line));
        }
        int totalPass = testResults.Count(r => r.Line.StartsWith("PASS"));
        lines.Add($"RESULT: PASS {totalPass} FAIL {testFailures}");
        try
        {
            File.WriteAllLines(testResultsPath, lines);
            Monitor.Log($"TEST wrote {testResultsPath}", LogLevel.Info);
        }
        catch (Exception ex)
        {
            Monitor.Log($"TEST could not write results: {ex.Message}", LogLevel.Error);
            testFailures++;
        }
        foreach (string line in lines)
            Monitor.Log($"TEST {line}", testFailures > 0 ? LogLevel.Error : LogLevel.Info);
        Environment.Exit(testFailures > 0 ? 1 : 0);
    }
}
