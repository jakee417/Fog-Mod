#nullable enable
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;
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
    private readonly List<string> testResults = new();
    private int testFailures;
    private string testSaveName = "Foggy_418804507";
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
    private const int TestFinishPhase = 12;

    private void InitTestHarness()
    {
        if (Environment.GetEnvironmentVariable("FOGMOD_TEST") != "1")
            return;
        testActive = true;
        testSaveName = Environment.GetEnvironmentVariable("FOGMOD_TEST_SAVE") ?? testSaveName;
        testResultsPath = Environment.GetEnvironmentVariable("FOGMOD_TEST_RESULTS")
            ?? Path.Combine(Path.GetTempPath(), "fogmod-test-results.txt");
        Helper.Events.GameLoop.UpdateTicked += OnTestTick;
        Monitor.Log($"Test harness active (save={testSaveName}, results={testResultsPath})", LogLevel.Info);
    }

    private void Check(bool cond, string name, string detail = "")
    {
        testResults.Add(cond ? $"PASS {name}" : $"FAIL {name}: {detail}");
        if (cond)
            Monitor.Log($"TEST PASS {name}", LogLevel.Info);
        else
        {
            testFailures++;
            Monitor.Log($"TEST FAIL {name}: {detail}", LogLevel.Error);
        }
    }

    private void NextPhase()
    {
        testPhase++;
        testPhaseTicks = 0;
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
                case 6: TestKill(); break;
                case 7: TestGrouseSpawn(); break;
                case 8: TestGrouseTrigger(); break;
                case 9: TestGrouseFlush(); break;
                case 10: TestGrouseLand(); break;
                case 11: TestGrouseKill(); break;
                case TestFinishPhase: TestFinish(); break;
            }
        }
        catch (Exception ex)
        {
            Check(false, "harness", $"phase {testPhase}: {ex.GetType().Name}: {ex.Message}");
            testPhase = TestFinishPhase;
            testPhaseTicks = 0;
        }
    }

    // Phase 0: wait for the title screen, then load the test save exactly
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

    // Phase 1: wait for the world.
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

    // Phase 2: warp to the Forest (bushes everywhere, no farm in the way).
    private void TestWarp()
    {
        if (testPhaseTicks == 1)
            Game1.warpFarmer("Forest", 50, 30, false);
        if (Game1.currentLocation?.NameOrUniqueName == "Forest")
            NextPhase();
        else if (testPhaseTicks > 600)
            GiveUp("warp", "never arrived in Forest");
    }

    // Phase 3: spawn a critter in the nearest usable bush.
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
        Check(testRunner.State == CritterzState.Sitting, "spawn-sitting", $"state={testRunner.State}");
        Check(testRunner.Skin >= 0 && testRunner.Skin <= 3, "spawn-skin", $"skin={testRunner.Skin}");
        Check(testRunner.HomeBush == bush.Tile, "spawn-homebush", $"home={testRunner.HomeBush} bush={bush.Tile}");
        NextPhase();
    }

    // Phase 4: shake the home bush through the real engine path (exercises
    // the Harmony hook) and wait for the burst.
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
            Bush? bush = BushHelper.GetAvailableBushes(location).FirstOrDefault(b => b.Tile == r.HomeBush);
            if (bush == null)
            {
                GiveUp("trigger", "home bush missing");
                return;
            }
            testLeavesBefore = TreeHelper.leaves.Count;
            bush.shake(bush.Tile, true);
        }
        if (r.State == CritterzState.Running && r.Path != null)
        {
            Check(r.DestBush != null, "trigger-dest", "no destination selected");
            Check(TreeHelper.leaves.Count > testLeavesBefore, "trigger-leaves", "no burst leaves spawned");
            if (r.DestBush is Vector2 dest)
                testDestBush = dest;
            NextPhase();
        }
        else if (r.State == CritterzState.Sitting && testPhaseTicks > 120)
            GiveUp("trigger", "still sitting (hook or path failed)");
        else if (testPhaseTicks > 600)
            GiveUp("trigger", $"stuck (state={r.State}, path={(r.Path == null ? "null" : r.Path.Count.ToString())})");
    }

    // Phase 5: wait for arrival at the destination bush.
    private void TestRun()
    {
        if (testRunner is not Critterz r)
        {
            GiveUp("arrive", "no test runner");
            return;
        }
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

    // Phase 6: loot shape + real engine kill (damageMonster, bomb hitbox).
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
            TriggerCritterz(testVictim);
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
        // Real kill through the engine (retry: 10% miss chance).
        int debrisBefore = location.debris.Count;
        for (int i = 0; i < 25 && v.Health > 0; i++)
            location.damageMonster(v.GetBoundingBox(), 0, 0, true, Game1.player, false);
        Check(v.Health <= 0, "kill-dead", $"health={v.Health}");
        Check(location.debris.Count >= debrisBefore + loot.Count, "kill-debris",
            $"debris grew by {location.debris.Count - debrisBefore}, loot had {loot.Count}");
        NextPhase();
    }

    // Phase 7: perch a grouse in a free tree.
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
        Check(testFlyer.State == GrouseState.Perched, "g-spawn-perched", $"state={testFlyer.State}");
        Check(testFlyer.TreePosition == testGrouseStartTree, "g-spawn-tree", $"tree={testFlyer.TreePosition}");
        Check(testFlyer.Texture != null, "g-spawn-texture", $"texture={testFlyer.TextureName}");
        string[] eggs = { "174", "176", "305", "928" };
        testGrouseEggs = testFlyer.InitialObjectToDrop.Count;
        Check(testFlyer.InitialObjectToDrop.All(id => eggs.Contains(id)), "g-spawn-eggs",
            $"eggs=[{string.Join(",", testFlyer.InitialObjectToDrop)}]");
        testGrouseDebrisBefore = location.debris.Count;
        NextPhase();
    }

    // Phase 8: shake the tree through the real engine path (exercises the
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
            if (TreeHelper.GetTreeFromId(location, g.TreePosition) is not Tree tree)
            {
                GiveUp("g-trigger", "home tree missing");
                return;
            }
            tree.shake(tree.Tile, true);
        }
        if (g.State == GrouseState.Surprised || g.State == GrouseState.Flushing || g.State == GrouseState.Flying)
            NextPhase();
        else if (testPhaseTicks > 600)
            GiveUp("g-trigger", $"still perched (state={g.State})");
    }

    // Phase 9: wait for the flush and its egg drop.
    private void TestGrouseFlush()
    {
        GameLocation location = Game1.currentLocation;
        if (testFlyer is not Grouse g)
        {
            GiveUp("g-flush", "no test grouse");
            return;
        }
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

    // Phase 10: wait for landing at a new tree.
    private void TestGrouseLand()
    {
        if (testFlyer is not Grouse g)
        {
            GiveUp("g-land", "no test grouse");
            return;
        }
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

    // Phase 11: snack shape + real engine kill (damageMonster, bomb hitbox).
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
            testGrouseVictim.State = GrouseState.Flying;
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
            location.damageMonster(v.GetBoundingBox(), 0, 0, true, Game1.player, false);
        Check(v.Health <= 0, "g-kill-dead", $"health={v.Health}");
        Check(location.debris.Count >= debrisBefore + loot.Count, "g-kill-debris",
            $"debris grew by {location.debris.Count - debrisBefore}, loot had {loot.Count}");
        NextPhase();
    }

    // Phase 12: report and exit without saving.
    private void TestFinish()
    {
        testPhase = 99; // run once
        testResults.Add($"RESULT: PASS {testResults.Count(r => r.StartsWith("PASS"))} FAIL {testFailures}");
        try
        {
            File.WriteAllLines(testResultsPath, testResults);
            Monitor.Log($"TEST wrote {testResultsPath}", LogLevel.Info);
        }
        catch (Exception ex)
        {
            Monitor.Log($"TEST could not write results: {ex.Message}", LogLevel.Error);
            testFailures++;
        }
        foreach (string line in testResults)
            Monitor.Log($"TEST {line}", testFailures > 0 ? LogLevel.Error : LogLevel.Info);
        Environment.Exit(testFailures > 0 ? 1 : 0);
    }
}
