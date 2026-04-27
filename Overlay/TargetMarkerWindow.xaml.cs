using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using PointyPal.Infrastructure;

namespace PointyPal.Overlay;

public partial class TargetMarkerWindow : Window
{
    private Storyboard? _pulseAnimation;

    public TargetMarkerWindow()
    {
        InitializeComponent();
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

    public void ShowAt(double x, double y)
    {
        Left = x - (Width / 2);
        Top = y - (Height / 2);
        Show();
        _pulseAnimation?.Begin();
    }

    public void HideMarker()
    {
        _pulseAnimation?.Stop();
        Hide();
    }
}
