using System;

namespace Scaffold.Figma.Schema
{
    [Serializable]
    public class FigmaNode
    {
        public string id;
        public string name;
        public string type;
        public FigmaRect rect;
        public FigmaConstraints constraints;
        public string layoutMode;
        public float layoutSpacing;
        public FigmaPadding layoutPadding;
        public FigmaFill[] fills;
        public string text;
        public FigmaTextStyle style;
        public FigmaNode[] children;
    }
}
