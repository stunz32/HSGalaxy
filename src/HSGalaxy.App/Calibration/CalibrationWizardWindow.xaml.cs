using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    private ObservableCollection<RoiRow> _roiRows = new();

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
            _overlay.RoisChanged += (_, __) => RefreshRoiList();
        }
        else
        {
            _overlay.Focus();
        }
        RefreshRoiList();
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_overlay == null)
        {
            SetStatus("Open overlay and draw ROIs first.");
            return;
        }
        var name = string.IsNullOrWhiteSpace(TxtProfile.Text) ? "Default" : TxtProfile.Text.Trim();
        var profile = new CalibrationProfile { Name = name, TargetTitle = _selected?.Title, TargetClass = _selected?.Class, TargetLeft = _selected?.Left, TargetTop = _selected?.Top };
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
        // If profile has target identity, attempt to re-attach by offset
        if (!string.IsNullOrWhiteSpace(profile.TargetTitle) || !string.IsNullOrWhiteSpace(profile.TargetClass))
        {
            var picker = new WindowPicker();
            var match = picker.Enumerate().FirstOrDefault(w =>
                (!string.IsNullOrWhiteSpace(profile.TargetTitle) && w.Title.IndexOf(profile.TargetTitle, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (!string.IsNullOrWhiteSpace(profile.TargetClass) && w.Class.IndexOf(profile.TargetClass, StringComparison.OrdinalIgnoreCase) >= 0));
            if (match != null)
            {
                int dx = 0, dy = 0;
                if (profile.TargetLeft.HasValue && profile.TargetTop.HasValue)
                {
                    dx = match.Left - profile.TargetLeft.Value;
                    dy = match.Top - profile.TargetTop.Value;
                }
                var adjusted = profile.Regions.Select(r => new Roi { Id = r.Id, X = r.X + dx, Y = r.Y + dy, Width = r.Width, Height = r.Height }).ToList();
                _overlay = new RoiEditorOverlayWindow(match);
                _overlay.Show();
                _overlay.SetRois(adjusted);
                SetStatus($"Loaded and attached to '{match.Title}'. Offset ({dx},{dy}).");
                return;
            }
        }
        _overlay!.SetRois(profile.Regions);
        await SaveCurrentProfileNameAsync(name);
        SetStatus($"Loaded profile '{name}'.");
        RefreshRoiList();
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        if (_overlay == null) { SetStatus("Overlay not open."); return; }
        _overlay.ClearRois();
        SetStatus("ROIs cleared.");
        RefreshRoiList();
    }

    private async void BtnReattach_Click(object sender, RoutedEventArgs e)
    {
        var name = string.IsNullOrWhiteSpace(TxtProfile.Text) ? "Default" : TxtProfile.Text.Trim();
        string folder = GetCalibrationFolder();
        var profile = await CalibrationManager.LoadAsync(folder, name);
        if (profile == null)
        {
            SetStatus($"Profile '{name}' not found in {folder}");
            return;
        }
        if (string.IsNullOrWhiteSpace(profile.TargetTitle) && string.IsNullOrWhiteSpace(profile.TargetClass))
        {
            SetStatus("Profile has no TargetTitle/Class saved.");
            return;
        }
        var picker = new WindowPicker();
        var match = picker.Enumerate().FirstOrDefault(w =>
            (!string.IsNullOrWhiteSpace(profile.TargetTitle) && w.Title.IndexOf(profile.TargetTitle, StringComparison.OrdinalIgnoreCase) >= 0) ||
            (!string.IsNullOrWhiteSpace(profile.TargetClass) && w.Class.IndexOf(profile.TargetClass, StringComparison.OrdinalIgnoreCase) >= 0));
        if (match == null)
        {
            SetStatus("No matching window found to reattach.");
            return;
        }
        EnsureOverlay();
        _overlay?.Close();
        _overlay = new RoiEditorOverlayWindow(match);
        _overlay.Show();
        int dx = 0, dy = 0;
        if (profile.TargetLeft.HasValue && profile.TargetTop.HasValue)
        {
            dx = match.Left - profile.TargetLeft.Value;
            dy = match.Top - profile.TargetTop.Value;
        }
        var adjusted = profile.Regions.Select(r => new Roi { Id = r.Id, X = r.X + dx, Y = r.Y + dy, Width = r.Width, Height = r.Height }).ToList();
        _overlay.SetRois(adjusted);
        _selected = match;
        SetStatus($"Reattached to '{match.Title}'. Offset ({dx},{dy}).");
        RefreshRoiList();
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

    // ROI list integration
    private void RefreshRoiList()
    {
        if (_overlay == null)
        {
            _roiRows = new();
            LstRois.ItemsSource = _roiRows;
            return;
        }
        var list = _overlay.GetRois()
            .Select(r => new RoiRow(r.Id, r.X, r.Y, r.Width, r.Height))
            .ToList();
        _roiRows = new ObservableCollection<RoiRow>(list);
        LstRois.ItemsSource = _roiRows;
    }

    private void LstRois_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_overlay == null) return;
        if (LstRois.SelectedItem is RoiRow row)
        {
            _overlay.SelectRoi(row.Id);
        }
    }

    private void BtnDeleteRoi_Click(object sender, RoutedEventArgs e)
    {
        if (_overlay == null) return;
        if (LstRois.SelectedItem is RoiRow row)
        {
            _overlay.DeleteRoi(row.Id);
            RefreshRoiList();
        }
    }

    private void TxtRoiId_LostFocus(object sender, RoutedEventArgs e)
    {
        TryRenameFromEditor(sender);
    }

    private void TxtRoiId_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            TryRenameFromEditor(sender);
            e.Handled = true;
        }
    }

    private void TryRenameFromEditor(object sender)
    {
        if (_overlay == null) return;
        if (sender is System.Windows.Controls.TextBox tb && tb.DataContext is RoiRow row)
        {
            var newId = tb.Text?.Trim() ?? string.Empty;
            if (!string.Equals(newId, row.Id, StringComparison.Ordinal))
            {
                // Two-way binding already updated row.Id; row.OriginalId is the previous value
            }
            if (!string.IsNullOrWhiteSpace(newId) && !string.Equals(row.OriginalId, newId, StringComparison.Ordinal))
            {
                _overlay.RenameRoi(row.OriginalId, newId);
                row.OriginalId = newId;
                RefreshRoiList();
            }
        }
    }

    private sealed class RoiRow
    {
        public RoiRow(string id, int x, int y, int w, int h)
        {
            Id = id; OriginalId = id; X = x; Y = y; Width = w; Height = h;
        }
        public string Id { get; set; }
        public string OriginalId { get; set; }
        public string Pos => $"{X},{Y}";
        public string Size => $"{Width}x{Height}";
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
    }
}
