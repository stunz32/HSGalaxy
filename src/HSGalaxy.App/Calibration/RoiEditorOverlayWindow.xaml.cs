using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using HSGalaxy.Core.Calibration;
using HSGalaxy.UI.Capture;

namespace HSGalaxy.App.Calibration;

public partial class RoiEditorOverlayWindow : Window
{
    private readonly HSGalaxy.UI.Capture.WindowPicker.WindowInfo _target;
    private DpiScale _dpi;
    private bool _draggingNew = false;
    private bool _draggingMove = false;
    private Point _start;
    private Rectangle? _currentRect;
    private Rectangle? _hitRect;

    public RoiEditorOverlayWindow(HSGalaxy.UI.Capture.WindowPicker.WindowInfo target)
    {
        _target = target;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _dpi = VisualTreeHelper.GetDpi(this);
        // Position over target window using PMv2 DPI
        double scaleX = _dpi.DpiScaleX;
        double scaleY = _dpi.DpiScaleY;
        Left = _target.Left / scaleX;
        Top = _target.Top / scaleY;
        Width = Math.Max(1, (_target.Right - _target.Left) / scaleX);
        Height = Math.Max(1, (_target.Bottom - _target.Top) / scaleY);
        CanvasRoot.Width = Width;
        CanvasRoot.Height = Height;
        CanvasRoot.Focus();
    }

    public void ClearRois()
    {
        var keep = CanvasRoot.Children.OfType<UIElement>().Where(c => c is not Rectangle).ToList();
        CanvasRoot.Children.Clear();
        foreach (var k in keep) CanvasRoot.Children.Add(k);
    }

    public List<Roi> GetRois()
    {
        double sx = _dpi.DpiScaleX;
        double sy = _dpi.DpiScaleY;
        var rois = new List<Roi>();
        foreach (var r in CanvasRoot.Children.OfType<Rectangle>())
        {
            double x = Canvas.GetLeft(r);
            double y = Canvas.GetTop(r);
            double w = r.Width;
            double h = r.Height;
            int px = _target.Left + (int)Math.Round(x * sx);
            int py = _target.Top + (int)Math.Round(y * sy);
            int pw = (int)Math.Round(w * sx);
            int ph = (int)Math.Round(h * sy);
            rois.Add(new Roi { Id = Guid.NewGuid().ToString("N"), X = px, Y = py, Width = Math.Max(1, pw), Height = Math.Max(1, ph) });
        }
        return rois;
    }

    public void SetRois(IEnumerable<Roi> rois)
    {
        ClearRois();
        double sx = _dpi.DpiScaleX;
        double sy = _dpi.DpiScaleY;
        foreach (var roi in rois)
        {
            int rx = roi.X - _target.Left;
            int ry = roi.Y - _target.Top;
            if (roi.Width <= 0 || roi.Height <= 0) continue;
            var rect = CreateRect();
            rect.Width = Math.Max(1, roi.Width / sx);
            rect.Height = Math.Max(1, roi.Height / sy);
            Canvas.SetLeft(rect, Math.Max(0, rx / sx));
            Canvas.SetTop(rect, Math.Max(0, ry / sy));
            CanvasRoot.Children.Add(rect);
        }
    }

    private Rectangle CreateRect()
    {
        return new Rectangle
        {
            Stroke = Brushes.Lime,
            Fill = new SolidColorBrush(Color.FromArgb(30, 0, 255, 0)),
            StrokeThickness = 2.0,
            RadiusX = 2,
            RadiusY = 2
        };
    }

    private void CanvasRoot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(CanvasRoot);
        _hitRect = HitTest(_start);
        if (_hitRect != null)
        {
            _draggingMove = true;
            CanvasRoot.CaptureMouse();
            e.Handled = true;
            return;
        }

        _draggingNew = true;
        _currentRect = CreateRect();
        _currentRect.Width = 1;
        _currentRect.Height = 1;
        Canvas.SetLeft(_currentRect, _start.X);
        Canvas.SetTop(_currentRect, _start.Y);
        CanvasRoot.Children.Add(_currentRect);
        CanvasRoot.CaptureMouse();
        e.Handled = true;
    }

    private void CanvasRoot_MouseMove(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(CanvasRoot);
        if (_draggingNew && _currentRect != null)
        {
            double x = Math.Min(p.X, _start.X);
            double y = Math.Min(p.Y, _start.Y);
            double w = Math.Abs(p.X - _start.X);
            double h = Math.Abs(p.Y - _start.Y);
            Canvas.SetLeft(_currentRect, x);
            Canvas.SetTop(_currentRect, y);
            _currentRect.Width = Math.Max(1, w);
            _currentRect.Height = Math.Max(1, h);
        }
        else if (_draggingMove && _hitRect != null)
        {
            double dx = p.X - _start.X;
            double dy = p.Y - _start.Y;
            Canvas.SetLeft(_hitRect, Math.Max(0, Canvas.GetLeft(_hitRect) + dx));
            Canvas.SetTop(_hitRect, Math.Max(0, Canvas.GetTop(_hitRect) + dy));
            _start = p;
        }
    }

    private void CanvasRoot_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _draggingNew = false;
        _draggingMove = false;
        _currentRect = null;
        _hitRect = null;
        Mouse.Capture(null);
    }

    private void CanvasRoot_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            // Remove last rectangle
            var last = CanvasRoot.Children.OfType<Rectangle>().LastOrDefault();
            if (last != null) CanvasRoot.Children.Remove(last);
        }
    }

    private Rectangle? HitTest(Point p)
    {
        foreach (var r in CanvasRoot.Children.OfType<Rectangle>().Reverse())
        {
            double x = Canvas.GetLeft(r);
            double y = Canvas.GetTop(r);
            if (p.X >= x && p.X <= x + r.Width && p.Y >= y && p.Y <= y + r.Height)
                return r;
        }
        return null;
    }
}
