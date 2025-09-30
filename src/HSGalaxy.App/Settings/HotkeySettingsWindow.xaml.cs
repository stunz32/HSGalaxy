using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using HSGalaxy.Core.Config;

namespace HSGalaxy.App.Settings
{
    public partial class HotkeySettingsWindow : Window
    {
        private readonly string _defTheme = "Ctrl+Alt+T";
        private readonly string _defCapture = "Ctrl+Alt+P";
        private readonly string _defWizard = "Ctrl+Alt+C";

        public HotkeySettingsWindow(AppSettings settings)
        {
            InitializeComponent();
            TxtTheme.Text = settings.ThemeHotkey ?? _defTheme;
            TxtCapture.Text = settings.CaptureHotkey ?? _defCapture;
            TxtWizard.Text = settings.WizardHotkey ?? _defWizard;
        }

        public event EventHandler<HotkeysSavedEventArgs>? Saved;

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var theme = TxtTheme.Text?.Trim() ?? _defTheme;
            var capture = TxtCapture.Text?.Trim() ?? _defCapture;
            var wizard = TxtWizard.Text?.Trim() ?? _defWizard;
            Saved?.Invoke(this, new HotkeysSavedEventArgs(theme, capture, wizard));
            Close();
        }

        private void BtnResetTheme_Click(object sender, RoutedEventArgs e) => TxtTheme.Text = _defTheme;
        private void BtnResetCapture_Click(object sender, RoutedEventArgs e) => TxtCapture.Text = _defCapture;
        private void BtnResetWizard_Click(object sender, RoutedEventArgs e) => TxtWizard.Text = _defWizard;

        private void Txt_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Build a chord string from pressed modifiers + key
            var isCtrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            var isAlt = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
            var isShift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            var isWin = Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin);

            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (IsModifierKey(key)) { e.Handled = true; return; }

            string chord = string.Join("+", new[]
            {
                isCtrl ? "Ctrl" : null,
                isAlt ? "Alt" : null,
                isShift ? "Shift" : null,
                isWin ? "Win" : null,
                KeyToToken(key)
            }.Where(s => !string.IsNullOrEmpty(s)));

            if (sender is System.Windows.Controls.TextBox tb)
            {
                tb.Text = chord;
            }
            e.Handled = true;
        }

        private static bool IsModifierKey(Key key)
        {
            return key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;
        }

        private static string KeyToToken(Key key)
        {
            if (key >= Key.A && key <= Key.Z) return key.ToString().ToUpperInvariant();
            if (key >= Key.D0 && key <= Key.D9) return ((char)('0' + (key - Key.D0))).ToString();
            if (key >= Key.NumPad0 && key <= Key.NumPad9) return ((char)('0' + (key - Key.NumPad0))).ToString();
            if (key >= Key.F1 && key <= Key.F24) return $"F{(int)(key - Key.F1 + 1)}";
            // Fallback to key name
            return key.ToString();
        }
    }

    public sealed class HotkeysSavedEventArgs : EventArgs
    {
        public HotkeysSavedEventArgs(string theme, string capture, string wizard)
        {
            Theme = theme; Capture = capture; Wizard = wizard;
        }
        public string Theme { get; }
        public string Capture { get; }
        public string Wizard { get; }
    }
}
