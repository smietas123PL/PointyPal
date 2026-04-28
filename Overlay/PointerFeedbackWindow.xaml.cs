using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using PointyPal.Infrastructure;

namespace PointyPal.Overlay;

public partial class PointerFeedbackWindow : Window
{
    public event Action<int>? FeedbackReceived;

    public PointerFeedbackWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        int extendedStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        
        // We want this one to be interactive but NOT take focus away if possible
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, extendedStyle | 
            NativeMethods.WS_EX_TOOLWINDOW | 
            NativeMethods.WS_EX_NOACTIVATE);
    }

    public void ShowAt(double x, double y)
    {
        Left = x - (Width / 2);
        Top = y + 40; // Offset below the target
        Show();
    }

    private void OnRatingClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && int.TryParse(tag, out int rating))
        {
            FeedbackReceived?.Invoke(rating);
            Hide();
        }
    }

    private void OnDismissClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }
}
