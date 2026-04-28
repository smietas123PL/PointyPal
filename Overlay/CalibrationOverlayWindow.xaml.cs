using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using PointyPal.Capture;
using PointyPal.Infrastructure;
using PointyPal.Core;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace PointyPal.Overlay;

public partial class CalibrationOverlayWindow : Window
{
    private readonly ConfigService _configService;
    private readonly CoordinateMapper _mapper;
    private readonly DispatcherTimer _refreshTimer;
    private bool _isInteractive = false;

    public event Action<PointerTarget>? TestPointRequested;

    public CalibrationOverlayWindow(ConfigService configService)
    {
        InitializeComponent();
        _configService = configService;
        _mapper = new CoordinateMapper();

        _refreshTimer = new DispatcherTimer();
        _refreshTimer.Interval = TimeSpan.FromMilliseconds(50);
        _refreshTimer.Tick += (s, e) => UpdateCursorInfo();
    }

    public void ShowCalibration(bool interactive = false)
    {
        _isInteractive = interactive;
        UpdateMonitorPosition();
        DrawGrid();
        _refreshTimer.Start();
        
        if (_isInteractive && _configService.Config.DeveloperModeEnabled)
        {
            DevControlPanel.Visibility = Visibility.Visible;
            InfoPanel.Visibility = Visibility.Collapsed;
            MakeInteractive(true);
        }
        else
        {
            DevControlPanel.Visibility = Visibility.Collapsed;
            InfoPanel.Visibility = Visibility.Visible;
            MakeInteractive(false);
        }

        Show();
    }

    public void HideCalibration()
    {
        _refreshTimer.Stop();
        Hide();
    }

    private void MakeInteractive(bool interactive)
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        int extendedStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        
        if (interactive)
        {
            // Remove WS_EX_TRANSPARENT to receive clicks
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, extendedStyle & ~NativeMethods.WS_EX_TRANSPARENT);
            this.Focusable = true;
        }
        else
        {
            // Add WS_EX_TRANSPARENT for click-through
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, extendedStyle | NativeMethods.WS_EX_TRANSPARENT);
            this.Focusable = false;
        }
    }

    private void UpdateMonitorPosition()
    {
        NativeMethods.GetCursorPos(out var pt);
        var info = ScreenUtilities.GetMonitorInfo(new Point(pt.X, pt.Y));
        
        this.Left = info.BoundsPhysical.Left;
        this.Top = info.BoundsPhysical.Top;
        this.Width = info.BoundsPhysical.Width;
        this.Height = info.BoundsPhysical.Height;

        MonitorInfoText.Text = $"Monitor: {info.BoundsPhysical.Width}x{info.BoundsPhysical.Height} @ {info.BoundsPhysical.Left},{info.BoundsPhysical.Top}";
        DpiInfoText.Text = $"DPI Scale: {info.DpiScaleX:F2} ({info.DpiScaleX * 96:F0} DPI)";
    }

    private void DrawGrid()
    {
        GridCanvas.Children.Clear();

        double w = this.Width;
        double h = this.Height;

        // 1. Draw Screen Grid (Blue, every 100px)
        for (double x = 0; x < w; x += 100)
        {
            AddGridLine(x, 0, x, h, Brushes.RoyalBlue, 1, x % 500 == 0 ? 0.8 : 0.3);
            if (x % 500 == 0) AddLabel(x + 2, 2, x.ToString(), Brushes.RoyalBlue, 10);
        }
        for (double y = 0; y < h; y += 100)
        {
            AddGridLine(0, y, w, y, Brushes.RoyalBlue, 1, y % 500 == 0 ? 0.8 : 0.3);
            if (y % 500 == 0) AddLabel(2, y + 2, y.ToString(), Brushes.RoyalBlue, 10);
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

        // Estimate image coords if we had a capture
        var config = _configService.Config;
        double downscale = (double)config.MaxImageWidth / this.Width;
        if (downscale > 1.0) downscale = 1.0;
        
        double imgX = relX * downscale;
        double imgY = relY * downscale;

        ImageCoordsText.Text = $"Image Est: {imgX:F0}, {imgY:F0} (Downscale: {downscale:F4})";
        
        // If monitor changed, refresh grid
        var info = ScreenUtilities.GetMonitorInfo(screenPoint);
        if (Math.Abs(info.BoundsPhysical.Left - this.Left) > 1 || Math.Abs(info.BoundsPhysical.Width - this.Width) > 1)
        {
            UpdateMonitorPosition();
            DrawGrid();
        }
    }

    private void OnTestPointClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            double x = 0, y = 0;
            switch (tag)
            {
                case "TopLeft": x = 50; y = 50; break;
                case "TopRight": x = Width - 50; y = 50; break;
                case "BottomLeft": x = 50; y = Height - 50; break;
                case "BottomRight": x = Width - 50; y = Height - 50; break;
                case "Center": x = Width / 2; y = Height / 2; break;
            }

            TriggerTestPoint(x, y, tag);
        }
    }

    private void OnRunCalibrationTest(object sender, RoutedEventArgs e)
    {
        TriggerTestPoint(Width / 2, Height / 2, "Calibration Test");
    }

    private void TriggerTestPoint(double relX, double relY, string label)
    {
        var physicalPoint = new Point(this.Left + relX, this.Top + relY);
        
        // We need a fake capture geometry to map this point
        var info = ScreenUtilities.GetMonitorInfo(physicalPoint);
        var geometry = new CaptureGeometry
        {
            MonitorBoundsPhysical = info.BoundsPhysical,
            MonitorBoundsDip = info.BoundsDip,
            DpiScaleX = info.DpiScaleX,
            DpiScaleY = info.DpiScaleY,
            DownscaleFactorX = 1.0,
            DownscaleFactorY = 1.0,
            CaptureImageWidth = (int)info.BoundsPhysical.Width,
            CaptureImageHeight = (int)info.BoundsPhysical.Height
        };

        var target = new PointerTarget
        {
            FinalScreenPhysicalPoint = physicalPoint,
            FinalOverlayDipPoint = new Point(relX / info.DpiScaleX, relY / info.DpiScaleY),
            Label = label,
            Source = PointSource.Calibration,
            Confidence = PointConfidence.High
        };

        TestPointRequested?.Invoke(target);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        HideCalibration();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Default to transparent
        MakeInteractive(false);
    }
}
