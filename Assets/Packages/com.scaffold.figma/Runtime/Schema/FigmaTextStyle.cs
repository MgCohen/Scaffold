using System;

namespace Scaffold.Figma.Schema
{
    [Serializable]
    public class FigmaTextStyle
    {
        public float fontSize;
        public string fontFamily;
        public FigmaColor color;
        public string alignH;
        public string alignV;
    }

    [Serializable]
    public class FigmaColor
    {
        public float r;
        public float g;
        public float b;
        public float a;
    }
}
