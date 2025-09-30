using Vortice.Mathematics;

namespace HSGalaxy.UI.Rendering
{
    /// <summary>
    /// Status strip model used by the renderer to draw an opaque bottom band with text metrics.
    /// </summary>
    public sealed class StatusStrip
    {
        public Color4 Background { get; set; } = ThemeColors.DarkBg;
        public Color4 Foreground { get; set; } = ThemeColors.DarkFg;
        public float HeightDip { get; set; } = 32f;

        // Metrics/fields
        public string Status { get; set; } = "Connected";
        public string Endpoint { get; set; } = "Auto";
        public string Region { get; set; } = "";
        public string Latency { get; set; } = "";
        public string P50 { get; set; } = "";
        public string P95 { get; set; } = "";
        public string Mode { get; set; } = "";
    }

    /// <summary>
    /// Theme colors for status strip backgrounds/foregrounds.
    /// </summary>
    public static class ThemeColors
    {
        public static readonly Color4 LightBg = new Color4(0xF9/255f, 0xFA/255f, 0xFB/255f, 1f);
        public static readonly Color4 LightFg = new Color4(0x11/255f, 0x18/255f, 0x27/255f, 1f);
        public static readonly Color4 DarkBg  = new Color4(0x11/255f, 0x18/255f, 0x27/255f, 1f);
        public static readonly Color4 DarkFg  = new Color4(0xF9/255f, 0xFA/255f, 0xFB/255f, 1f);
        public static readonly Color4 SafeBg  = new Color4(0x0B/255f, 0x0F/255f, 0x17/255f, 1f);
        public static readonly Color4 SafeFg  = DarkFg;
    }
}
