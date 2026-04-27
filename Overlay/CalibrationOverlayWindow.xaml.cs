using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using PointyPal.Capture;
using PointyPal.Infrastructure;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace PointyPal.Overlay;

public partial class CalibrationOverlayWindow : Window
{
    private readonly ConfigService _configService;
    private readonly CoordinateMapper _mapper;
    private readonly DispatcherTimer _refreshTimer;

    public CalibrationOverlayWindow(ConfigService configService)
    {
        InitializeComponent();
        _configService = configService;
        _mapper = new CoordinateMapper();

        _refreshTimer = new DispatcherTimer();
        _refreshTimer.Interval = TimeSpan.FromMilliseconds(50);
        _refreshTimer.Tick += (s, e) => UpdateCursorInfo();
    }

    public void ShowCalibration()
    {
        UpdateMonitorPosition();
        DrawGrid();
        _refreshTimer.Start();
        Show();
    }

    public void HideCalibration()
    {
        _refreshTimer.Stop();
        Hide();
    }

    private void UpdateMonitorPosition()
    {
        NativeMethods.GetCursorPos(out var pt);
        var bounds = ScreenUtilities.GetMonitorBounds(new Point(pt.X, pt.Y));
        
        this.Left = bounds.Left;
        this.Top = bounds.Top;
        this.Width = bounds.Width;
        this.Height = bounds.Height;
    }

    private void DrawGrid()
    {
        GridCanvas.Children.Clear();

        double w = this.Width;
        double h = this.Height;

        // 1. Draw Screen Grid (Blue, every 100px)
        for (double x = 0; x < w; x += 100)
        {
            AddGridLine(x, 0, x, h, Brushes.RoyalBlue, 1, x % 500 == 0 ? 2 : 0.5);
            if (x % 200 == 0) AddLabel(x + 2, 2, x.ToString(), Brushes.RoyalBlue, 10);
        }
        for (double y = 0; y < h; y += 100)
        {
            AddGridLine(0, y, w, y, Brushes.RoyalBlue, 1, y % 500 == 0 ? 2 : 0.5);
            if (y % 200 == 0) AddLabel(2, y + 2, y.ToString(), Brushes.RoyalBlue, 10);
        }

        // 2. Draw Image Grid (Green, based on MaxImageWidth)
        var config = _configService.Config;
        double maxW = config.MaxImageWidth;
        double scale = maxW / w;
        double imageH = h * scale;

        // Vertical lines every 100 image-pixels
        for (double ix = 0; ix <= maxW; ix += 100)
        {
            double sx = ix / scale;
            AddGridLine(sx, 0, sx, h, Brushes.LimeGreen, 1, 0.8, new DoubleCollection { 4, 4 });
            if (ix % 200 == 0) AddLabel(sx + 2, h - 20, $"i:{ix}", Brushes.LimeGreen, 10);
        }
        // Horizontal lines every 100 image-pixels
        for (double iy = 0; iy <= imageH; iy += 100)
        {
            double sy = iy / scale;
            AddGridLine(0, sy, w, sy, Brushes.LimeGreen, 1, 0.8, new DoubleCollection { 4, 4 });
            if (iy % 200 == 0) AddLabel(w - 50, sy + 2, $"i:{iy}", Brushes.LimeGreen, 10);
        }
    }

    private void AddGridLine(double x1, double y1, double x2, double y2, System.Windows.Media.Brush brush, double thickness, double opacity, DoubleCollection? dash = null)
    {
        var line = new Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = brush,
            StrokeThickness = thickness,
            Opacity = opacity,
            StrokeDashArray = dash
        };
        GridCanvas.Children.Add(line);
    }

    private void AddLabel(double x, double y, string text, System.Windows.Media.Brush brush, int size)
    {
        var tb = new TextBlock
        {
            Text = text,
            Foreground = brush,
            FontSize = size,
            FontFamily = new System.Windows.Media.FontFamily("Consolas")
        };
        Canvas.SetLeft(tb, x);
        Canvas.SetTop(tb, y);
        GridCanvas.Children.Add(tb);
    }

    private void UpdateCursorInfo()
    {
        NativeMethods.GetCursorPos(out var pt);
        var screenPoint = new Point(pt.X, pt.Y);
        
        // Relative to current monitor
        double relX = screenPoint.X - this.Left;
        double relY = screenPoint.Y - this.Top;

        ScreenCoordsText.Text = $"Screen: {pt.X}, {pt.Y} (Rel: {relX:F0}, {relY:F0})";

        // Estimate image coords
        var config = _configService.Config;
        double scale = (double)config.MaxImageWidth / this.Width;
        double imgX = relX * scale;
        double imgY = relY * scale;

        ImageCoordsText.Text = $"Image: {imgX:F0}, {imgY:F0} (Scale: {scale:F4})";
        
        // If monitor changed, refresh grid
        var currentBounds = ScreenUtilities.GetMonitorBounds(screenPoint);
        if (Math.Abs(currentBounds.Left - this.Left) > 1 || Math.Abs(currentBounds.Width - this.Width) > 1)
        {
            UpdateMonitorPosition();
            DrawGrid();
        }
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
}
