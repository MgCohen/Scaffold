using System;

namespace Scaffold.AutoPacker
{
    // Tells the Unity linker to keep the marked code in a stripped IL2CPP player.
    // The Unity linker matches an attribute named PreserveAttribute by its class name only,
    // so this type has the same effect as UnityEngine.Scripting.PreserveAttribute.
    // The generator puts it on the Packed struct and on the pack and unpack members,
    // because the runtime finds them by reflection.
    [AttributeUsage(
        AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum
        | AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field
        | AttributeTargets.Event | AttributeTargets.Interface | AttributeTargets.Delegate,
        Inherited = false)]
    public sealed class PreserveAttribute : Attribute { }
}
