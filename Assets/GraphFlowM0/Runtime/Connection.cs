using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Scaffold.GraphFlow.M0
{
    /// <summary>Runtime wiring handle — built at hydration, never serialized.</summary>
    public abstract class Connection
    {
        public abstract object SourceNodeBoxed { get; }
        public abstract int SourcePortId { get; }

        /// <summary>
        /// Hydration-time adapter: lazily maps the upstream read through <paramref name="map"/>.
        /// Keeps port wiring typed; graph/asset-level converter tables can supply the delegate later.
        /// </summary>
        public static Connection<TTo> Map<TFrom, TTo>(Connection<TFrom> source, Func<TFrom, TTo> map)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var src = source;
            var m = map;
            return new Connection<TTo>(src.SourceNode, src.SourcePortId, () => m(src.Read()));
        }
    }

    public sealed class Connection<T> : Connection
    {
        readonly Func<T> _read;

        public RuntimeNode SourceNode { get; }
        public override object SourceNodeBoxed => SourceNode;
        public override int SourcePortId { get; }

        internal Connection(RuntimeNode source, int sourcePortId, Func<T> read)
        {
            SourceNode = source;
            SourcePortId = sourcePortId;
            _read = read;
        }

        public T Read() => _read();
    }

    [Serializable]
    public abstract class RuntimeNode
    {
        public int nodeId;
        public string editorGuid;

        public abstract Connection GetOutputConnection(int portId);
        public abstract void BindInput(int portId, Connection connection);
    }

    [Serializable]
    public abstract class RuntimeNode<TRunner> : RuntimeNode where TRunner : GraphRunner
    {
        public abstract Task<FlowContinuation> Execute(TRunner runner);
    }
}
