#nullable enable
using System;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace FogMod.Tests;

public class NativeCrittersTests
{
    private static Type TypeNamed(string fullName)
    {
        var name = new AssemblyName("TestCritters");
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
        ModuleBuilder module = assembly.DefineDynamicModule("m");
        TypeBuilder type = module.DefineType(fullName, TypeAttributes.Public | TypeAttributes.Class);
        return type.CreateType()!;
    }

    [Fact]
    public void ShouldSuppress_NullIsFalse()
    {
        Assert.False(Utils.NativeCritters.ShouldSuppress((Type?)null));
    }

    [Fact]
    public void ShouldSuppress_NativeRabbitAndSquirrel()
    {
        Assert.True(Utils.NativeCritters.ShouldSuppress(TypeNamed("StardewValley.BellsAndWhistles.Rabbit")));
        Assert.True(Utils.NativeCritters.ShouldSuppress(TypeNamed("StardewValley.BellsAndWhistles.Squirrel")));
    }

    [Fact]
    public void ShouldSuppress_EverythingElsePasses()
    {
        // Other native critters keep spawning.
        Assert.False(Utils.NativeCritters.ShouldSuppress(TypeNamed("StardewValley.BellsAndWhistles.Butterfly")));
        Assert.False(Utils.NativeCritters.ShouldSuppress(TypeNamed("StardewValley.BellsAndWhistles.Birdie")));
        // Another mod's own Rabbit/Squirrel types are kept.
        Assert.False(Utils.NativeCritters.ShouldSuppress(TypeNamed("SomeMod.Rabbit")));
        Assert.False(Utils.NativeCritters.ShouldSuppress(TypeNamed("SomeMod.Squirrel")));
        Assert.False(Utils.NativeCritters.ShouldSuppress(typeof(string)));
    }
}
