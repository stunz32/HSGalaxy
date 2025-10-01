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
using HSGalaxy.Core.OCR;
using HSGalaxy.Diagnostics;

namespace HSGalaxy.App.Calibration;

public partial class CalibrationWizardWindow : Window
{
    private readonly WindowPicker _picker = new WindowPicker();
    private HSGalaxy.UI.Capture.WindowPicker.WindowInfo? _selected;
    private RoiEditorOverlayWindow? _overlay;
    private ObservableCollection<RoiRow> _roiRows = new();
    private ObservableCollection<OcrRow> _ocrAll = new();

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

    private async void BtnRunOcr_Click(object sender, RoutedEventArgs e)
    {
        if (_overlay == null) { SetStatus("Overlay not open."); return; }
        var rois = _overlay.GetRois();
        if (rois.Count == 0) { SetStatus("No ROIs to OCR."); return; }
        var profile = new CalibrationProfile { Name = string.IsNullOrWhiteSpace(TxtProfile.Text) ? "Default" : TxtProfile.Text.Trim() };
        profile.Regions.AddRange(rois);
        var client = OcrClientSelector.Create();
        var pipeline = new OcrPipeline(client);
        using var cap = new HSGalaxy.UI.Capture.CaptureManager();
        var result = await pipeline.RunOnceAsync(profile, r => cap.Capture(new System.Drawing.Rectangle(r.X, r.Y, r.Width, r.Height)));
        var list = new System.Collections.ObjectModel.ObservableCollection<OcrRow>();
        for (int i = 0; i < rois.Count; i++)
        {
            string id = rois[i].Id ?? i.ToString();
            var lines = result.Lines.FindAll(l => l.RoiIndex == i);
            if (lines.Count == 0)
            {
                list.Add(new OcrRow(id, 0.0, "0.00", ""));
            }
            else
            {
                float avg = 0f; foreach (var ln in lines) avg += ln.Confidence; avg /= lines.Count;
                string text = string.Join(" ", lines.ConvertAll(l => l.Text));
                list.Add(new OcrRow(id, avg, avg.ToString("F2"), text));
            }
        }
        _ocrAll = list;
        ApplyOcrFilters();
        TxtOcrMeta.Text = $"Source:{result.Source}  Elapsed:{result.ElapsedMs:F1}ms  Lines:{result.Lines.Count}";
        SetStatus("OCR run complete.");
        try { OverlayLogger.Log("Wizard.OCR.Run", $"Source={result.Source}; Lines={result.Lines.Count}; ElapsedMs={result.ElapsedMs:F1}"); } catch { }
    }

    private void BtnCopyAll_Click(object sender, RoutedEventArgs e)
    {
        if (LstOcr.Items.Count == 0) { SetStatus("No OCR results to copy."); return; }
        var sb = new System.Text.StringBuilder();
        foreach (var it in LstOcr.Items)
        {
            if (it is OcrRow row) sb.AppendLine($"{row.RoiId}\t{row.Confidence}\t{row.Text}");
        }
        try { System.Windows.Clipboard.SetText(sb.ToString()); SetStatus("OCR results copied to clipboard."); }
        catch { SetStatus("Failed to access clipboard."); }
    }

    private async void BtnSaveResults_Click(object sender, RoutedEventArgs e)
    {
        if (LstOcr.Items.Count == 0) { SetStatus("No OCR results to save."); return; }
        try
        {
            var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HSGalaxy");
            System.IO.Directory.CreateDirectory(folder);
            var name = string.IsNullOrWhiteSpace(TxtProfile.Text) ? "Default" : TxtProfile.Text.Trim();
            var path = System.IO.Path.Combine(folder, $"ocr_{Sanitize(name)}_{DateTime.Now:yyyyMMdd_HHmmss}.tsv");
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("ROI\tConf\tText");
            foreach (var it in LstOcr.Items) if (it is OcrRow row) sb.AppendLine($"{row.RoiId}\t{row.Confidence}\t{row.Text}");
            await System.IO.File.WriteAllTextAsync(path, sb.ToString(), System.Text.Encoding.UTF8);
            SetStatus($"Saved OCR results to: {path}");
        }
        catch (Exception ex)
        {
            SetStatus($"Save failed: {ex.Message}");
        }
    }

    private void TxtOcrFilter_Changed(object sender, TextChangedEventArgs e)
    {
        ApplyOcrFilters();
    }

    private void ApplyOcrFilters()
    {
        if (_ocrAll == null) { LstOcr.ItemsSource = null; TxtOcrCounters.Text = string.Empty; return; }
        string roi = TxtOcrFilterRoi?.Text?.Trim() ?? string.Empty;
        double min = 0.0;
        if (!string.IsNullOrWhiteSpace(TxtOcrMinConf?.Text))
        {
            double.TryParse(TxtOcrMinConf.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out min);
        }
        var filtered = _ocrAll.Where(r =>
            (string.IsNullOrWhiteSpace(roi) || (r.RoiId?.IndexOf(roi, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
            && r.ConfVal >= min).ToList();
        var view = new ObservableCollection<OcrRow>(filtered);
        LstOcr.ItemsSource = view;
        int withText = filtered.Count(r => !string.IsNullOrWhiteSpace(r.Text));
        int empty = filtered.Count - withText;
        TxtOcrCounters.Text = $"WithText:{withText}  Empty:{empty}";
    }

    private void BtnCopyTextOnly_Click(object sender, RoutedEventArgs e)
    {
        if (LstOcr.Items.Count == 0) { SetStatus("No OCR results to copy."); return; }
        var sb = new System.Text.StringBuilder();
        foreach (var it in LstOcr.Items)
        {
            if (it is OcrRow row)
            {
                sb.AppendLine($"[{row.RoiId}]");
                if (!string.IsNullOrWhiteSpace(row.Text)) sb.AppendLine(row.Text);
                sb.AppendLine();
            }
        }
        try { System.Windows.Clipboard.SetText(sb.ToString()); SetStatus("OCR text copied to clipboard."); try { OverlayLogger.Log("Wizard.OCR.CopyTextOnly", $"Items={LstOcr.Items.Count}"); } catch { } }
        catch { SetStatus("Failed to access clipboard."); }
    }

    private sealed class OcrRow
    {
        public OcrRow(string roiId, double confVal, string conf, string text) { RoiId = roiId; ConfVal = confVal; Confidence = conf; Text = text; }
        public string RoiId { get; }
        public double ConfVal { get; }
        public string Confidence { get; }
        public string Text { get; }
    }

    private async void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var name = string.IsNullOrWhiteSpace(TxtProfile.Text) ? "Default" : TxtProfile.Text.Trim();
        string folder = GetCalibrationFolder();
        var profile = await CalibrationManager.LoadAsync(folder, name);
        if (profile == null) { SetStatus($"Profile '{name}' not found in {folder}"); return; }
        try
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = Sanitize(name) + ".json",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                InitialDirectory = folder
            };
            if (dlg.ShowDialog(this) == true)
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(profile, Newtonsoft.Json.Formatting.Indented);
                await System.IO.File.WriteAllTextAsync(dlg.FileName, json, System.Text.Encoding.UTF8);
                SetStatus($"Exported '{name}' to: {dlg.FileName}");
                try { OverlayLogger.Log("Wizard.Export", dlg.FileName); } catch { }
            }
        }
        catch (Exception ex) { SetStatus($"Export failed: {ex.Message}"); }
    }

    private async void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                InitialDirectory = GetCalibrationFolder()
            };
            if (dlg.ShowDialog(this) != true) return;
            var json = await System.IO.File.ReadAllTextAsync(dlg.FileName, System.Text.Encoding.UTF8);
            var prof = Newtonsoft.Json.JsonConvert.DeserializeObject<CalibrationProfile>(json) ?? new CalibrationProfile();
            var desired = TxtProfile?.Text?.Trim();
            string name = !string.IsNullOrWhiteSpace(desired) ? desired! : (!string.IsNullOrWhiteSpace(prof.Name) ? prof.Name : System.IO.Path.GetFileNameWithoutExtension(dlg.FileName));
            string folder = GetCalibrationFolder();
            // If name exists and user didn't explicitly type a different name, auto-suffix
            string outPath = System.IO.Path.Combine(folder, name + ".json");
            if (System.IO.File.Exists(outPath) && (string.IsNullOrWhiteSpace(desired) || string.Equals(desired, prof.Name, StringComparison.OrdinalIgnoreCase)))
            {
                string baseName = name;
                string candidate = baseName + "_copy";
                int n = 2;
                while (System.IO.File.Exists(System.IO.Path.Combine(folder, candidate + ".json")))
                {
                    candidate = baseName + "_copy" + n.ToString();
                    n++;
                }
                name = candidate;
                SetStatus($"Name exists. Imported as '{name}'.");
            }
            prof.Name = name;
            await CalibrationManager.SaveAsync(prof, folder);
            await SaveCurrentProfileNameAsync(name);
            TxtProfile.Text = name;
            SetStatus($"Imported profile '{name}' from {dlg.FileName}");
            try { OverlayLogger.Log("Wizard.Import", $"{dlg.FileName} -> {System.IO.Path.Combine(folder, name + ".json")}"); } catch { }
            if (_overlay != null)
            {
                _overlay.SetRois(prof.Regions);
                RefreshRoiList();
            }
        }
        catch (Exception ex) { SetStatus($"Import failed: {ex.Message}"); }
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
            // Notify the running App (tray/status strip) to reflect the change immediately
            HSGalaxy.App.App.NotifyCurrentProfileChanged(name);
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
                if (_overlay.ContainsId(newId))
                {
                    SetStatus($"ROI Id '{newId}' already exists.");
                    tb.Text = row.OriginalId; // revert visual
                }
                else
                {
                    _overlay.RenameRoi(row.OriginalId, newId);
                    row.OriginalId = newId;
                    RefreshRoiList();
                }
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
