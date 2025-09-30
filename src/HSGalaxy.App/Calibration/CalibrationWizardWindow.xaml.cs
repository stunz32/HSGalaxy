using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using HSGalaxy.Core.Calibration;
using HSGalaxy.Core.Config;
using HSGalaxy.UI.Capture;

namespace HSGalaxy.App.Calibration;

public partial class CalibrationWizardWindow : Window
{
    private readonly WindowPicker _picker = new WindowPicker();
    private HSGalaxy.UI.Capture.WindowPicker.WindowInfo? _selected;
    private RoiEditorOverlayWindow? _overlay;

    public CalibrationWizardWindow()
    {
        InitializeComponent();
        LoadWindows();
    }

    private void LoadWindows(string? filter = null)
    {
        var list = string.IsNullOrWhiteSpace(filter)
            ? _picker.Enumerate().ToList()
            : _picker.Enumerate().Where(w => (w.Title?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 || (w.Class?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0).ToList();
        LstWindows.ItemsSource = list;
    }

    private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        LoadWindows(TxtFilter.Text);
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => LoadWindows(TxtFilter.Text);

    private void LstWindows_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstWindows.SelectedItem is WindowPicker.WindowInfo wi)
        {
            _selected = wi;
            SetStatus($"Selected: {_selected.Title} [Class={_selected.Class}]  ({_selected.Left},{_selected.Top})-({_selected.Right},{_selected.Bottom})");
        }
        else
        {
            _selected = null;
            SetStatus("No target window selected.");
        }
    }

    private void BtnSelectWindow_Click(object sender, RoutedEventArgs e)
    {
        if (LstWindows.SelectedItem is HSGalaxy.UI.Capture.WindowPicker.WindowInfo wi)
            _selected = wi;
        else
            _selected = null;
        if (_selected == null)
        {
            SetStatus("Select a window from the list.");
            return;
        }
        SetStatus($"Selected: {_selected.Title} [Class={_selected.Class}]  ({_selected.Left},{_selected.Top})-({_selected.Right},{_selected.Bottom})");
        EnsureOverlay();
    }

    private void BtnOpenOverlay_Click(object sender, RoutedEventArgs e) => EnsureOverlay();
    private void BtnCloseOverlay_Click(object sender, RoutedEventArgs e) { _overlay?.Close(); _overlay = null; }

    private void EnsureOverlay()
    {
        if (_selected == null)
        {
            SetStatus("No target window selected.");
            return;
        }
        if (_overlay == null)
        {
            _overlay = new RoiEditorOverlayWindow(_selected);
            _overlay.Show();
        }
        else
        {
            _overlay.Focus();
        }
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_overlay == null)
        {
            SetStatus("Open overlay and draw ROIs first.");
            return;
        }
        var name = string.IsNullOrWhiteSpace(TxtProfile.Text) ? "Default" : TxtProfile.Text.Trim();
        var profile = new CalibrationProfile { Name = name };
        profile.Regions.AddRange(_overlay.GetRois());
        string folder = GetCalibrationFolder();
        await CalibrationManager.SaveAsync(profile, folder);
        await SaveCurrentProfileNameAsync(name);
        SetStatus($"Saved profile '{name}' to {folder}");
    }

    private async void BtnLoad_Click(object sender, RoutedEventArgs e)
    {
        var name = string.IsNullOrWhiteSpace(TxtProfile.Text) ? "Default" : TxtProfile.Text.Trim();
        string folder = GetCalibrationFolder();
        var profile = await CalibrationManager.LoadAsync(folder, name);
        if (profile == null)
        {
            SetStatus($"Profile '{name}' not found in {folder}");
            return;
        }
        EnsureOverlay();
        _overlay!.SetRois(profile.Regions);
        await SaveCurrentProfileNameAsync(name);
        SetStatus($"Loaded profile '{name}'.");
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        if (_overlay == null) { SetStatus("Overlay not open."); return; }
        _overlay.ClearRois();
        SetStatus("ROIs cleared.");
    }

    private async void BtnCaptureProof_Click(object sender, RoutedEventArgs e)
    {
        if (_overlay == null) { SetStatus("Overlay not open."); return; }
        var rois = _overlay.GetRois();
        if (rois.Count == 0) { SetStatus("No ROIs to capture."); return; }
        using var cap = new HSGalaxy.UI.Capture.CaptureManager();
        int width = 0, height = 0;
        foreach (var r in rois) { width = Math.Max(width, r.Width); height += r.Height; }
        using var composite = new System.Drawing.Bitmap(Math.Max(width, 1), Math.Max(height, 1), System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (var g = System.Drawing.Graphics.FromImage(composite))
        {
            g.Clear(System.Drawing.Color.Transparent);
            int y = 0;
            foreach (var r in rois)
            {
                using var bmp = cap.Capture(new System.Drawing.Rectangle(r.X, r.Y, r.Width, r.Height));
                g.DrawImageUnscaled(bmp, 0, y);
                y += r.Height;
            }
        }
        var folder = Path.Combine(Path.GetTempPath(), "HSGalaxy");
        Directory.CreateDirectory(folder);
        var name = string.IsNullOrWhiteSpace(TxtProfile.Text) ? "Default" : TxtProfile.Text.Trim();
        var path = Path.Combine(folder, $"calib_proof_{Sanitize(name)}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        composite.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        SetStatus($"Captured proof to: {path}");
    }

    private static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }

    private static string GetCalibrationFolder()
    {
        var env = Environment.GetEnvironmentVariable("HSGALAXY_CALIB_DIR");
        if (!string.IsNullOrWhiteSpace(env)) return env!;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "calibration");
    }

    private static async Task SaveCurrentProfileNameAsync(string name)
    {
        try
        {
            var cfgFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "config");
            Directory.CreateDirectory(cfgFolder);
            var cfgPath = Path.Combine(cfgFolder, "appsettings.json");
            var store = new JsonConfigStore<AppSettings>(cfgPath);
            var settings = await store.LoadAsync();
            settings.CurrentProfile = name;
            await store.SaveAsync(settings);
        }
        catch { }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        _overlay?.Close();
        _overlay = null;
        Close();
    }

    private void SetStatus(string text) => TxtStatus.Text = text;
}
