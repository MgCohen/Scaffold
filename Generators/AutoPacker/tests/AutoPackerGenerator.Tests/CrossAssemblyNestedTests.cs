using System;
using System.Reflection;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Scaffold.AutoPacker;

namespace AutoPackerGenerator.Tests
{
    // A [Packed] field whose type derives from an [AutoPack] record in another assembly.
    // Compilation A has the base record. Compilation B has the derived record and the holder.
    public class CrossAssemblyNestedTests
    {
        private const string IdSource = @"
using Scaffold.AutoPacker;

namespace Lib
{
    [AutoPack]
    public abstract partial record Id
    {
        [Packed] public int Value { get; init; } = -1;
    }

    [AutoPack]
    public abstract partial record Pair
    {
        [Packed] public int Left { get; init; }
        [Packed] public int Right { get; init; }
    }
}";

        private const string HolderSource = @"
using Scaffold.AutoPacker;

namespace App
{
    public sealed record AId : Lib.Id;
    public sealed record APair : Lib.Pair;

    [AutoPack]
    public partial class IdHolder
    {
        [Packed] public AId Id;
        [Packed] public APair Pair;
        [Packed] public int Slot;
    }
}";

        private static (Assembly Lib, Assembly App) BuildBoth()
        {
            var lib = GeneratorHarness.Emit(GeneratorHarness.Generate("Lib", IdSource));
            var app = GeneratorHarness.Emit(GeneratorHarness.Generate("App", HolderSource, MetadataReference.CreateFromImage(lib)));
            var loaded = GeneratorHarness.Load(lib, app);
            return (loaded[0], loaded[1]);
        }

        [Test]
        public void HolderInOtherAssembly_Compiles_AndStoresOwnerPacked()
        {
            var (lib, app) = BuildBoth();

            var holderPacked = app.GetType("App.IdHolder+Packed", throwOnError: true);
            Assert.AreEqual(lib.GetType("Lib.Id+Packed"), holderPacked.GetField("Id").FieldType);
            Assert.AreEqual(lib.GetType("Lib.Pair+Packed"), holderPacked.GetField("Pair").FieldType);
        }

        [Test]
        public void HolderInOtherAssembly_RoundTrips()
        {
            var (_, app) = BuildBoth();
            var holderType = app.GetType("App.IdHolder", throwOnError: true);
            var idType = app.GetType("App.AId", throwOnError: true);
            var pairType = app.GetType("App.APair", throwOnError: true);

            var id = Activator.CreateInstance(idType);
            idType.GetProperty("Value").SetValue(id, 7);
            var pair = Activator.CreateInstance(pairType);
            pairType.GetProperty("Left").SetValue(pair, 2);
            pairType.GetProperty("Right").SetValue(pair, 5);

            var holder = Activator.CreateInstance(holderType);
            holderType.GetField("Id").SetValue(holder, id);
            holderType.GetField("Pair").SetValue(holder, pair);
            holderType.GetField("Slot").SetValue(holder, 3);

            var packed = ((IPackable)holder).Pack();
            var back = Activator.CreateInstance(holderType);
            ((IUnpackable)back).Unpack(packed);

            var backId = holderType.GetField("Id").GetValue(back);
            Assert.AreEqual(idType, backId.GetType());
            Assert.AreEqual(7, idType.GetProperty("Value").GetValue(backId));
            var backPair = holderType.GetField("Pair").GetValue(back);
            Assert.AreEqual(2, pairType.GetProperty("Left").GetValue(backPair));
            Assert.AreEqual(5, pairType.GetProperty("Right").GetValue(backPair));
            Assert.AreEqual(3, holderType.GetField("Slot").GetValue(back));
        }

        [Test]
        public void HolderInOtherAssembly_NullId_RoundTripsNull()
        {
            var (_, app) = BuildBoth();
            var holderType = app.GetType("App.IdHolder", throwOnError: true);

            var packed = ((IPackable)Activator.CreateInstance(holderType)).Pack();
            var idPacked = packed.GetType().GetField("Id").GetValue(packed);
            Assert.AreEqual(-1, idPacked.GetType().GetField("Value").GetValue(idPacked));

            var back = Activator.CreateInstance(holderType);
            ((IUnpackable)back).Unpack(packed);
            Assert.IsNull(holderType.GetField("Id").GetValue(back));
        }

        [Test]
        public void SameAssembly_StillPacksInline()
        {
            var app = GeneratorHarness.Load(GeneratorHarness.Emit(GeneratorHarness.Generate("Same", IdSource + HolderSource.Replace("using Scaffold.AutoPacker;", ""))))[0];

            var holderPacked = app.GetType("App.IdHolder+Packed", throwOnError: true);
            Assert.AreEqual(app.GetType("Lib.Id+Packed"), holderPacked.GetField("Id").FieldType);
        }
    }
}
