using System;
using System.Windows;
using System.Windows.Threading;

namespace HSGalaxy.App.UI
{
    internal static class ToastService
    {
        public static void Show(string message, int durationMs = 2200)
        {
            try
            {
                if (System.Windows.Application.Current == null)
                {
                    return;
                }
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var toast = new ToastWindow(message)
                    {
                        Opacity = 0.0
                    };
                    toast.Show();
                    // Simple fade-in
                    var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(120)));
                    toast.BeginAnimation(Window.OpacityProperty, fadeIn);
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
                    timer.Tick += (_, __) =>
                    {
                        timer.Stop();
                        try
                        {
                            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.0, new Duration(TimeSpan.FromMilliseconds(180)));
                            fadeOut.Completed += (_, __2) => { try { toast.Close(); } catch { } };
                            toast.BeginAnimation(Window.OpacityProperty, fadeOut);
                        }
                        catch { toast.Close(); }
                    };
                    timer.Start();
                });
            }
            catch { }
        }
    }
}
