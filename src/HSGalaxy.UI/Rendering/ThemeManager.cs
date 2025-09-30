using System;
using Vortice.Mathematics;

namespace HSGalaxy.UI.Rendering
{
    public enum ThemeKind
    {
        Dark,
        Light,
        Safe
    }

    public static class ThemeManager
    {
        private static ThemeKind _current = ThemeKind.Dark;
        public static ThemeKind Current
        {
            get => _current;
            set
            {
                if (_current == value) return;
                _current = value;
                Changed?.Invoke(null, EventArgs.Empty);
            }
        }

        public static event EventHandler? Changed;

        public static (Color4 bg, Color4 fg) GetColors(ThemeKind kind)
        {
            return kind switch
            {
                ThemeKind.Light => (ThemeColors.LightBg, ThemeColors.LightFg),
                ThemeKind.Safe  => (ThemeColors.SafeBg, ThemeColors.SafeFg),
                _               => (ThemeColors.DarkBg, ThemeColors.DarkFg)
            };
        }

        public static void Cycle()
        {
            Current = Current switch
            {
                ThemeKind.Dark => ThemeKind.Light,
                ThemeKind.Light => ThemeKind.Safe,
                _ => ThemeKind.Dark
            };
        }

        public static void InitializeFromEnv()
        {
            var env = Environment.GetEnvironmentVariable("HSGALAXY_THEME");
            if (string.IsNullOrEmpty(env)) { Current = ThemeKind.Dark; return; }
            if (env.Equals("light", StringComparison.OrdinalIgnoreCase)) { Current = ThemeKind.Light; return; }
            if (env.Equals("safe", StringComparison.OrdinalIgnoreCase))  { Current = ThemeKind.Safe;  return; }
            Current = ThemeKind.Dark;
        }
    }
}

