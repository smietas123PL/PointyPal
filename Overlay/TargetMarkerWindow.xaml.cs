using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using PointyPal.Infrastructure;

namespace PointyPal.Overlay;

public partial class TargetMarkerWindow : Window
{
    private readonly ConfigService? _configService;
    private Storyboard? _pulseAnimation;

    public TargetMarkerWindow(ConfigService? configService = null)
    {
        InitializeComponent();
        _configService = configService;
        _pulseAnimation = (Storyboard)Resources["PulseAnimation"];
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        int extendedStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, extendedStyle | 
            NativeMethods.WS_EX_TRANSPARENT | 
            NativeMethods.WS_EX_TOOLWINDOW | 
            NativeMethods.WS_EX_NOACTIVATE);
    }

    public void ShowAt(double x, double y, string label)
    {
        // Center the 200x150 window on the target
        Left = x - 100;
        Top = y - 75;

        if (string.IsNullOrEmpty(label))
        {
            LabelBubble.Visibility = Visibility.Collapsed;
        }
        else
        {
            LabelBubble.Visibility = Visibility.Visible;
            int maxLength = _configService?.Config.PointerLabelMaxLength ?? 40;
            LabelText.Text = label.Length > maxLength ? label.Substring(0, maxLength - 3) + "..." : label;
        }

        Show();
        _pulseAnimation?.Begin();
    }

    public void HideMarker()
    {
        _pulseAnimation?.Stop();
        Hide();
    }
}
