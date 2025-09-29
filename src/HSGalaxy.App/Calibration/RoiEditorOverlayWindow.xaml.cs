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
    private bool _draggingResize = false;
    private System.Windows.Point _start;
    private System.Windows.Shapes.Rectangle? _currentRect;
    private System.Windows.Shapes.Rectangle? _hitRect;
    private System.Windows.Shapes.Rectangle? _selectedRect;
    private ResizeEdges _resizeMode = ResizeEdges.None;
    private const double Grip = 6.0; // DIP tolerance for edge hit tests

    [Flags]
    private enum ResizeEdges { None=0, Left=1, Top=2, Right=4, Bottom=8 }

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
        var keep = CanvasRoot.Children.OfType<UIElement>().Where(c => c is not System.Windows.Shapes.Rectangle).ToList();
        CanvasRoot.Children.Clear();
        foreach (var k in keep) CanvasRoot.Children.Add(k);
        _selectedRect = null;
    }

    public List<Roi> GetRois()
    {
        double sx = _dpi.DpiScaleX;
        double sy = _dpi.DpiScaleY;
        var rois = new List<Roi>();
        foreach (var r in CanvasRoot.Children.OfType<System.Windows.Shapes.Rectangle>())
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

    private System.Windows.Shapes.Rectangle CreateRect()
    {
        return new System.Windows.Shapes.Rectangle
        {
            Stroke = System.Windows.Media.Brushes.Lime,
            Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 0, 255, 0)),
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
            _selectedRect = _hitRect;
            _resizeMode = GetResizeMode(_hitRect, _start);
            if (_resizeMode != ResizeEdges.None)
            {
                _draggingResize = true;
            }
            else
            {
                _draggingMove = true;
            }
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

    private void CanvasRoot_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
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
            MoveRect(_hitRect, dx, dy);
            _start = p;
        }
        else if (_draggingResize && _hitRect != null)
        {
            ResizeRect(_hitRect, p);
            _start = p;
        }
    }

    private void CanvasRoot_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _draggingNew = false;
        _draggingMove = false;
        _currentRect = null;
        _hitRect = null;
        _draggingResize = false;
        _resizeMode = ResizeEdges.None;
        Mouse.Capture(null);
    }

    private void CanvasRoot_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            // Remove last rectangle
            var last = _selectedRect ?? CanvasRoot.Children.OfType<System.Windows.Shapes.Rectangle>().LastOrDefault();
            if (last != null)
            {
                CanvasRoot.Children.Remove(last);
                if (_selectedRect == last) _selectedRect = null;
            }
            return;
        }

        // Keyboard nudging and resizing
        if (_selectedRect != null)
        {
            int step = (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) ? 10 : 1;
            if (e.Key == Key.Left) { MoveRect(_selectedRect, -step, 0); e.Handled = true; }
            else if (e.Key == Key.Right) { MoveRect(_selectedRect, step, 0); e.Handled = true; }
            else if (e.Key == Key.Up) { MoveRect(_selectedRect, 0, -step); e.Handled = true; }
            else if (e.Key == Key.Down) { MoveRect(_selectedRect, 0, step); e.Handled = true; }
            else if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                // Ctrl + arrows resize from bottom/right edges
                if (e.Key == Key.Left) { _selectedRect.Width = Math.Max(1, _selectedRect.Width - step); e.Handled = true; }
                else if (e.Key == Key.Right) { _selectedRect.Width = Math.Max(1, _selectedRect.Width + step); e.Handled = true; }
                else if (e.Key == Key.Up) { _selectedRect.Height = Math.Max(1, _selectedRect.Height - step); e.Handled = true; }
                else if (e.Key == Key.Down) { _selectedRect.Height = Math.Max(1, _selectedRect.Height + step); e.Handled = true; }
            }
        }
    }

    private System.Windows.Shapes.Rectangle? HitTest(System.Windows.Point p)
    {
        foreach (var r in CanvasRoot.Children.OfType<System.Windows.Shapes.Rectangle>().Reverse())
        {
            double x = Canvas.GetLeft(r);
            double y = Canvas.GetTop(r);
            if (p.X >= x && p.X <= x + r.Width && p.Y >= y && p.Y <= y + r.Height)
                return r;
        }
        return null;
    }

    private ResizeEdges GetResizeMode(System.Windows.Shapes.Rectangle r, System.Windows.Point p)
    {
        double x = Canvas.GetLeft(r);
        double y = Canvas.GetTop(r);
        double l = x, t = y, rt = x + r.Width, bt = y + r.Height;
        ResizeEdges mode = ResizeEdges.None;
        if (Math.Abs(p.X - l) <= Grip) mode |= ResizeEdges.Left;
        if (Math.Abs(p.X - rt) <= Grip) mode |= ResizeEdges.Right;
        if (Math.Abs(p.Y - t) <= Grip) mode |= ResizeEdges.Top;
        if (Math.Abs(p.Y - bt) <= Grip) mode |= ResizeEdges.Bottom;
        return mode;
    }

    private void MoveRect(System.Windows.Shapes.Rectangle rect, double dx, double dy)
    {
        double nx = Math.Max(0, Canvas.GetLeft(rect) + dx);
        double ny = Math.Max(0, Canvas.GetTop(rect) + dy);
        nx = Math.Min(nx, Math.Max(0, CanvasRoot.Width - rect.Width));
        ny = Math.Min(ny, Math.Max(0, CanvasRoot.Height - rect.Height));
        Canvas.SetLeft(rect, nx);
        Canvas.SetTop(rect, ny);
    }

    private void ResizeRect(System.Windows.Shapes.Rectangle rect, System.Windows.Point cursor)
    {
        double x = Canvas.GetLeft(rect);
        double y = Canvas.GetTop(rect);
        double w = rect.Width;
        double h = rect.Height;
        double nx = x, ny = y, nw = w, nh = h;

        if (_resizeMode.HasFlag(ResizeEdges.Left))
        {
            nx = Math.Min(cursor.X, x + w - 1);
            nw = (x + w) - nx;
        }
        if (_resizeMode.HasFlag(ResizeEdges.Right))
        {
            nw = Math.Max(1, cursor.X - x);
        }
        if (_resizeMode.HasFlag(ResizeEdges.Top))
        {
            ny = Math.Min(cursor.Y, y + h - 1);
            nh = (y + h) - ny;
        }
        if (_resizeMode.HasFlag(ResizeEdges.Bottom))
        {
            nh = Math.Max(1, cursor.Y - y);
        }

        // Clamp to canvas
        nx = Math.Max(0, nx);
        ny = Math.Max(0, ny);
        nw = Math.Min(nw, CanvasRoot.Width - nx);
        nh = Math.Min(nh, CanvasRoot.Height - ny);

        Canvas.SetLeft(rect, nx);
        Canvas.SetTop(rect, ny);
        rect.Width = Math.Max(1, nw);
        rect.Height = Math.Max(1, nh);
    }
}
