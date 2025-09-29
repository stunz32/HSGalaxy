using System;
using System.Windows;
using System.Windows.Input;

namespace HSGalaxy.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SetupHotkeys();
    }

    private void SetupHotkeys()
    {
        var gesture = new KeyGesture(Key.C, ModifierKeys.Control | ModifierKeys.Alt);
        var cmd = new RoutedCommand();
        cmd.InputGestures.Add(gesture);
        CommandBindings.Add(new CommandBinding(cmd, (_, __) => OpenCalibrationWizard()));
    }

    private void BtnOpenCalib_Click(object sender, RoutedEventArgs e) => OpenCalibrationWizard();

    private void OpenCalibrationWizard()
    {
        var wiz = new Calibration.CalibrationWizardWindow();
        wiz.Owner = this;
        wiz.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        wiz.Show();
    }
}

