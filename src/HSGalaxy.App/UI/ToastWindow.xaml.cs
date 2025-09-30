using System;
using System.Windows;

namespace HSGalaxy.App.UI
{
    public partial class ToastWindow : Window
    {
        public ToastWindow(string message)
        {
            InitializeComponent();
            Txt.Text = message;
            Loaded += (_, __) => PositionAtTopRight();
        }

        private void PositionAtTopRight()
        {
            try
            {
                // Measure content
            this.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                this.Width = this.DesiredSize.Width;
                this.Height = this.DesiredSize.Height;
                var wa = SystemParameters.WorkArea;
                Left = wa.Right - Width - 16;
                Top = wa.Top + 16;
            }
            catch { }
        }
    }
}
