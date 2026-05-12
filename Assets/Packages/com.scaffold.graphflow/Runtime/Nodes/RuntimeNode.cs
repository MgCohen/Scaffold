using System;
using System.Collections.Generic;

namespace Scaffold.GraphFlow
{
    [Serializable]
    public abstract class RuntimeNode
    {
        public int nodeId;
        public string editorGuid = string.Empty;

        [NonSerialized] public readonly Dictionary<string, Port> Ports = new();

        internal virtual void Build(in NodeBuildSlice slice)
        {
            for (int i = 0; i < slice.Data.Count; i++)
            {
                var d = slice.Data[i];
                d.Destination.ConnectFrom(d.Source);
            }
            for (int i = 0; i < slice.Flow.Count; i++)
            {
                var f = slice.Flow[i];
                f.Source.Connection = f.Connection;
                f.Destination.Connection = f.Connection;
            }
        }

        public virtual void Initialize(GraphRunner runner) { }
    }

    [Serializable]
    public abstract class RuntimeNode<TRunner> : RuntimeNode where TRunner : GraphRunner
    {
        protected static TRunner Runner(Flow flow) => (TRunner)flow.Runner;

        public sealed override void Initialize(GraphRunner runner) => Initialize((TRunner)runner);
        public virtual void Initialize(TRunner runner) { }
    }
}
