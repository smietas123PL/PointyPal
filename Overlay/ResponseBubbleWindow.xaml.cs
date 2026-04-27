using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using PointyPal.Infrastructure;

namespace PointyPal.Overlay;

public partial class ResponseBubbleWindow : Window
{
    private bool _isVisible;

    public ResponseBubbleWindow()
    {
        InitializeComponent();
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

    public async void ShowMessage(string message, double anchorX, double anchorY, int durationMs = 4000)
    {
        MessageText.Text = message;
        UpdateLayout();
        UpdatePosition(anchorX, anchorY);
        
        Show();
        _isVisible = true;

        await Task.Delay(durationMs);
        if (_isVisible && MessageText.Text == message)
        {
            Hide();
            _isVisible = false;
        }
    }

    public void UpdatePosition(double anchorX, double anchorY)
    {
        if (_isVisible)
        {
            Left = anchorX + 30;
            Top = anchorY - ActualHeight - 10;
        }
    }

    public void HideMessage()
    {
        Hide();
        _isVisible = false;
    }
}
