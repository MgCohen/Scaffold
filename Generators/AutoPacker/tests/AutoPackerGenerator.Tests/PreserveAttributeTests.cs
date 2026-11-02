using System;
using System.Reflection;
using NUnit.Framework;
using Scaffold.AutoPacker;

namespace AutoPackerGenerator.Tests
{
    // The runtime finds Packed and the pack and unpack members by reflection.
    // Each one must carry PreserveAttribute, so that a stripped IL2CPP player keeps it.
    public class PreserveAttributeTests
    {
        private const string Source = @"
using Scaffold.AutoPacker;

namespace App
{
    [AutoPack]
    public partial class ClassEvent
    {
        [Packed] public int Slot;
    }

    [AutoPack]
    public partial struct StructEvent
    {
        [Packed] public int Slot;
    }

    [AutoPack]
    public abstract partial class Marker { }

    [AutoPack]
    public partial class MarkerLeaf : Marker
    {
        [Packed] public int Slot;
    }
}";

        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        private static Assembly Build() =>
            GeneratorHarness.Load(GeneratorHarness.Emit(GeneratorHarness.Generate("Preserve", Source)))[0];

        private static void AssertPreserved(MemberInfo member)
        {
            Assert.IsNotNull(member, "member is missing");
            Assert.IsNotNull(member.GetCustomAttribute<PreserveAttribute>(), $"{member.DeclaringType?.Name}.{member.Name} has no PreserveAttribute");
        }

        private static void AssertPackedPreserved(Type owner)
        {
            var packed = owner.GetNestedType("Packed");
            AssertPreserved(packed);
            AssertPreserved(packed.GetConstructor(new[] { owner, typeof(IPackingHandler) }));
            AssertPreserved(owner.GetConstructor(new[] { packed, typeof(IPackingHandler) }));
            AssertPreserved(owner.GetMethod("PackTyped", Declared));
            AssertPreserved(owner.GetMethod("Pack", Declared));
        }

        [Test]
        public void Class_PackedAndPackUnpackMembers_ArePreserved()
        {
            var owner = Build().GetType("App.ClassEvent", throwOnError: true);

            AssertPackedPreserved(owner);
            AssertPreserved(owner.GetMethod("Unpack", Declared));
            AssertPreserved(owner.GetMethod("UnpackTyped", Declared));
        }

        [Test]
        public void Struct_PackedAndPackMembers_ArePreserved()
        {
            var owner = Build().GetType("App.StructEvent", throwOnError: true);

            AssertPackedPreserved(owner);
        }

        [Test]
        public void AbstractMarker_PackAndUnpack_ArePreserved()
        {
            var owner = Build().GetType("App.Marker", throwOnError: true);

            AssertPreserved(owner.GetMethod("Pack", Declared));
            AssertPreserved(owner.GetMethod("Unpack", Declared));
        }

        [Test]
        public void EmittedSource_UsesGlobalQualifiedPreserveAttribute()
        {
            var compilation = GeneratorHarness.Generate("PreserveSource", Source);

            var found = false;
            foreach (var tree in compilation.SyntaxTrees)
            {
                if (tree.ToString().Contains("public struct Packed"))
                {
                    StringAssert.Contains("[global::Scaffold.AutoPacker.PreserveAttribute]", tree.ToString());
                    found = true;
                }
            }
            Assert.IsTrue(found, "no generated Packed struct");
        }
    }
}
