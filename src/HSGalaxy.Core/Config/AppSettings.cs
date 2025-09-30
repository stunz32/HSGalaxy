namespace HSGalaxy.Core.Config
{
    /// <summary>
    /// Minimal app settings persisted to config store.
    /// </summary>
    public sealed class AppSettings
    {
        public string CurrentProfile { get; set; } = "";
        // Global hotkey chords in 'Ctrl+Alt+K' style
        public string ThemeHotkey { get; set; } = "Ctrl+Alt+T";
        public string CaptureHotkey { get; set; } = "Ctrl+Alt+P";
        public string WizardHotkey { get; set; } = "Ctrl+Alt+C";
    }
}
