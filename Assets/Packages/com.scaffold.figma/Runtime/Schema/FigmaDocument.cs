using System;

namespace Scaffold.Figma.Schema
{
    [Serializable]
    public class FigmaDocument
    {
        public string documentName;
        public FigmaNode[] nodes;
    }
}
