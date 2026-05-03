using Scaffold.GraphFlow;
namespace Scaffold.GraphFlow.M0.Smoke
{
    // Hand-written by design: Unity binds ScriptableObject types to MonoScripts via on-disk .cs files,
    // which generator-emitted virtual sources cannot satisfy. Keep this file's name matching the type.
    public sealed class MySmokeGraphAsset : GraphAsset<MySmokeRunner> { }
}
