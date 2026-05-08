using System;
using UnityEngine;

namespace Scaffold.GraphFlow
{
    [Serializable]
    public struct RuntimeVariable
    {
        public string id;
        public string name;
        public string typeName;
        [SerializeReference] public VariableDefault defaultValue;
    }

    [Serializable]
    public struct VariableEdge
    {
        public string variableId;
        public int toNodeId;
        public string toPortName;
    }
}
