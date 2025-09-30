using System.Collections.Generic;

namespace HSGalaxy.Core.Calibration
{
    public sealed class CalibrationProfile
    {
        public string Name { get; set; } = "Default";
        // Persist target window identity to simplify re-attachment
        public string? TargetTitle { get; set; }
        public string? TargetClass { get; set; }
        public List<Roi> Regions { get; set; } = new List<Roi>();
    }

    public sealed class Roi
    {
        public string Id { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
