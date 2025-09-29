using System.Collections.Generic;

namespace HSGalaxy.Core.OCR
{
    public sealed class OcrResult
    {
        public List<OcrLine> Lines { get; } = new List<OcrLine>();
        public double ElapsedMs { get; set; }
        public string Source { get; set; } = string.Empty;
    }

    public sealed class OcrLine
    {
        public int RoiIndex { get; set; }
        public string Text { get; set; } = string.Empty;
        public float Confidence { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}

