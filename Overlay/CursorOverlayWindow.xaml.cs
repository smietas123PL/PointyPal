using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using PointyPal.AI;
using PointyPal.Capture;
using PointyPal.Infrastructure;
using PointyPal.Input;
using PointyPal.Core;
using Point = System.Windows.Point;

namespace PointyPal.Overlay;

public partial class CursorOverlayWindow : Window
{
    private readonly AppStateManager _stateManager;
    private readonly PushToTalkService _pttService;
    private readonly InteractionCoordinator _coordinator;
    private readonly ScreenCaptureService _captureService = new();
    private readonly CoordinateMapper _mapper = new();
    private readonly PointTagParser _parser = new();
    private readonly ResilienceMonitorService? _resilienceMonitor;
    private readonly ConfigService _configService;
    private readonly ProviderHealthCheckService _healthService;

    private double _currentX;
    private double _currentY;

    // Easing factor (0 to 1, higher is faster)
    private const double EasingFactor = 0.3;
    // Offset from actual cursor so we don't cover it
    private const double OffsetX = 20;
    private const double OffsetY = 20;

    private readonly PointerFlightAnimator _animator = new();
    private readonly TargetMarkerWindow _targetMarker;
    private readonly PointerFeedbackWindow _feedbackWindow = new();
    private readonly PointerQualityService _qualityService;
    private readonly DiagnosticsWindow _diagnostics = new();
    private readonly ResponseBubbleWindow _responseBubble = new();
    private readonly CalibrationOverlayWindow _calibrationOverlay;
    private DateTime _pointStartTime;

    private CaptureResult? _latestCapture;
    private PointTag? _latestParsedTag;
    private PointerTarget? _latestTarget;
    private Point? _latestMappedScreenTarget;
    private string _latestStateReason = string.Empty;

    public CursorOverlayWindow(
        AppStateManager stateManager, 
        PushToTalkService pttService, 
        InteractionCoordinator coordinator,
        ConfigService configService,
        UsageTracker usageTracker,
        DebugArtifactCleanupService cleanupService,
        ProviderHealthCheckService healthService,
        SelfTestReportService selfTestReportService,
        InteractionTimelineService timelineService,
        PerformanceSummaryService performanceSummaryService,
        PointerQualityService qualityService,
        AppLifecycleService? lifecycleService = null,
        StartupRegistrationService? startupRegistrationService = null,
        AppLogService? appLogService = null,
        CrashLogger? crashLogger = null,
        SingleInstanceService? singleInstanceService = null,
        ResilienceMonitorService? resilienceMonitor = null)
    {
        InitializeComponent();
        _stateManager = stateManager;
        _pttService = pttService;
        _coordinator = coordinator;
        _configService = configService;
        _qualityService = qualityService;
        _healthService = healthService;
        _targetMarker = new TargetMarkerWindow(configService);
        
        Pointer.ApplyConfig(configService.Config);
        _configService.ConfigChanged += OnConfigChanged;

        _diagnostics.SetServices(
            configService,
            usageTracker,
            cleanupService,
            healthService,
            selfTestReportService,
            timelineService,
            performanceSummaryService,
            lifecycleService,
            startupRegistrationService,
            appLogService,
            crashLogger,
            singleInstanceService,
            resilienceMonitor);
        _resilienceMonitor = resilienceMonitor;
        _calibrationOverlay = new CalibrationOverlayWindow(configService);
        _calibrationOverlay.TestPointRequested += OnCalibrationTestPointRequested;
        _feedbackWindow.FeedbackReceived += OnFeedbackReceived;
        
        _stateManager.StateChanged += OnStateChanged;
        _pttService.DiagnosticsToggled += OnDiagnosticsToggled;
        _pttService.CalibrationToggled += OnCalibrationToggled;
        _pttService.TestF10Requested += OnTestF10Requested;
        _pttService.TestF11Requested += OnTestF11Requested;

        _coordinator.FlightRequested += OnFlightRequested;
        _coordinator.ResponseBubbleRequested += OnResponseBubbleRequested;
        _coordinator.DiagnosticsUpdated += OnInteractionDiagnosticsUpdated;

        if (_resilienceMonitor != null)
        {
            _resilienceMonitor.DisplayTopologyChanged += OnDisplayTopologyChanged;
        }

        Pointer.UpdateState(_stateManager.CurrentState, _configService.Config.ForceSafeMode, _configService.Config.DeveloperModeEnabled);

        // Initialize position
        if (NativeMethods.GetCursorPos(out var pt))
        {
            _currentX = pt.X + OffsetX;
            _currentY = pt.Y + OffsetY;
            Left = _currentX;
            Top = _currentY;
        }

        CompositionTarget.Rendering += OnRendering;
    }

    private void OnFlightRequested(PointerTarget target, TimeSpan duration)
    {
        Dispatcher.InvokeAsync(() =>
        {
            var dipPoint = target.FinalOverlayDipPoint;
            _latestTarget = target;
            _latestMappedScreenTarget = target.FinalScreenPhysicalPoint;
            _animator.Start(new Point(_currentX, _currentY), dipPoint, duration);
            _targetMarker.ShowAt(dipPoint.X, dipPoint.Y, target.Label);
            _feedbackWindow.Hide(); // Hide previous if still showing
        });
    }

    private void OnFeedbackReceived(int rating)
    {
        if (_latestTarget != null)
        {
            _qualityService.RecordFeedback(rating, _latestTarget);
            _diagnostics.UpdatePointerQuality(_qualityService.GetStats());
        }
    }

    private void OnResponseBubbleRequested(string text)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _responseBubble.ShowMessage(text, _currentX, _currentY);
        });
    }

    private void OnInteractionDiagnosticsUpdated(InteractionDiagnostics diag)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _diagnostics.UpdateInteractionData(diag);
        });
    }

    private void OnCalibrationTestPointRequested(PointerTarget target)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _latestMappedScreenTarget = target.FinalScreenPhysicalPoint;
            _animator.Start(new Point(_currentX, _currentY), target.FinalOverlayDipPoint, TimeSpan.FromMilliseconds(400));
            _targetMarker.ShowAt(target.FinalOverlayDipPoint.X, target.FinalOverlayDipPoint.Y, target.Label);
            _stateManager.SetState(CompanionState.FlyingToTarget, "Calibration Test");
        });
    }

    private void OnTestF10Requested(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            try
            {
                _latestCapture?.Dispose();
                _latestCapture = _captureService.CaptureCurrentCursorMonitor();
                
                string fakeResponse = "Kliknij tutaj. [POINT:900,250:test mapped target]";
                _latestParsedTag = _parser.Parse(fakeResponse);

                if (_latestParsedTag.HasPoint)
                {
                    var imagePoint = new Point(_latestParsedTag.X, _latestParsedTag.Y);
                    _latestMappedScreenTarget = _mapper.MapImagePointToScreenPoint(imagePoint, _latestCapture);
                    
                    _stateManager.SetState(CompanionState.FlyingToTarget, "F10 Fake Tag Mapped");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"F10 Test failed: {ex.Message}");
            }
        });
    }

    private void OnTestF11Requested(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            try
            {
                _latestCapture?.Dispose();
                _latestCapture = _captureService.CaptureCurrentCursorMonitor();

                var imageCenter = new Point(_latestCapture.Image.Width / 2.0, _latestCapture.Image.Height / 2.0);
                _latestMappedScreenTarget = _mapper.MapImagePointToScreenPoint(imageCenter, _latestCapture);

                _latestParsedTag = new PointTag { HasPoint = true, X = imageCenter.X, Y = imageCenter.Y, Label = "Center of Image" };
                
                _stateManager.SetState(CompanionState.FlyingToTarget, "F11 Center Mapped");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"F11 Test failed: {ex.Message}");
            }
        });
    }

    private void OnDisplayTopologyChanged()
    {
        Dispatcher.InvokeAsync(() =>
        {
            // Reset window position or refresh layout if needed
            System.Diagnostics.Debug.WriteLine("Display topology changed, refreshing overlay bounds.");
            // WPF windows often need a kick to recognize new virtual screen bounds
        });
    }

    private void OnConfigChanged(object? sender, AppConfig config)
    {
        Dispatcher.InvokeAsync(() => 
        {
            Pointer.ApplyConfig(config);
        });
    }

    private void OnStateChanged(object? sender, (CompanionState State, string Reason) e)
    {
        Dispatcher.InvokeAsync(() => 
        {
            _latestStateReason = e.Reason;
            Pointer.UpdateState(e.State, _configService.Config.ForceSafeMode, _configService.Config.DeveloperModeEnabled);
            
            if (e.State == CompanionState.FlyingToTarget)
            {
                if (_latestMappedScreenTarget.HasValue && 
                    (e.Reason == "F10 Fake Tag Mapped" || e.Reason == "F11 Center Mapped"))
                {
                    var target = _latestMappedScreenTarget.Value;
                    _animator.Start(new Point(_currentX, _currentY), target, TimeSpan.FromSeconds(0.6));
                    _targetMarker.ShowAt(target.X, target.Y, "Test Point");
                }
                else if (NativeMethods.GetCursorPos(out var pt))
                {
                    // Fallback to F8 old fake logic
                    var bounds = ScreenUtilities.GetMonitorBounds(new Point(pt.X, pt.Y));
                    double targetX = bounds.X + (bounds.Width * 0.70);
                    double targetY = bounds.Y + (bounds.Height * 0.35);

                    _animator.Start(new Point(_currentX, _currentY), new Point(targetX, targetY), TimeSpan.FromSeconds(0.6));
                    _targetMarker.ShowAt(targetX, targetY, "Fallback Target");
                }
                else
                {
                    _stateManager.SetState(CompanionState.FollowingCursor, "Lost cursor");
                }
            }
            else if (e.State == CompanionState.PointingAtTarget)
            {
                _pointStartTime = DateTime.Now;
            }
            else if (e.State == CompanionState.ReturningToCursor)
            {
                _targetMarker.HideMarker();
                // We don't hide feedback window here because it might be what triggered the return or we want it to stay for a bit
            }
            else if (e.State == CompanionState.FollowingCursor || e.State == CompanionState.Listening)
            {
                _targetMarker.HideMarker();
                _feedbackWindow.Hide();
            }
        });
    }

    private void OnDiagnosticsToggled(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (_configService.Config.DeveloperModeEnabled || _configService.Config.ShowAdvancedDiagnostics)
            {
                _diagnostics.Toggle();
                return;
            }

            var policy = new ProviderPolicyService(_configService);
            var status = policy.GetProviderStatusForUi();
            string message = $"Status: {status.ModeLabel}. Worker: {_healthService.WorkerStatus}. Auth: {(status.WorkerAuthConfigured ? "configured" : "missing")}.";
            _responseBubble.ShowMessage(message, _currentX, _currentY);
        });
    }

    private void OnCalibrationToggled(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (_calibrationOverlay.Visibility == Visibility.Visible)
            {
                _calibrationOverlay.HideCalibration();
            }
            else
            {
                bool interactive = _configService.Config.DeveloperModeEnabled;
                _calibrationOverlay.ShowCalibration(interactive);
            }
        });
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        int extendedStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        
        // WS_EX_TRANSPARENT: Click-through
        // WS_EX_TOOLWINDOW: Don't show in Alt-Tab
        // WS_EX_NOACTIVATE: Don't take focus
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, extendedStyle | 
            NativeMethods.WS_EX_TRANSPARENT | 
            NativeMethods.WS_EX_TOOLWINDOW | 
            NativeMethods.WS_EX_NOACTIVATE);
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var state = _stateManager.CurrentState;
        
        NativeMethods.POINT pt = new();
        bool hasCursor = NativeMethods.GetCursorPos(out pt);

        var source = PresentationSource.FromVisual(this);
        double dpiScaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiScaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        double offset = _configService.Config.PointerTargetOffsetPx;
        double cursorTargetX = (pt.X / dpiScaleX) + offset;
        double cursorTargetY = (pt.Y / dpiScaleY) + offset;

        if (state == CompanionState.FlyingToTarget)
        {
            var now = DateTime.Now;
            var pos = _animator.GetPosition(now);
            _currentX = pos.X;
            _currentY = pos.Y;

            if (_animator.IsFinished(now))
            {
                _stateManager.SetState(CompanionState.PointingAtTarget, "Flight finished");
            }
        }
        else if (state == CompanionState.PointingAtTarget)
        {
            if ((DateTime.Now - _pointStartTime).TotalMilliseconds > 500)
            {
                var config = _configService.Config;
                bool shouldShowFeedback = config.PointerFeedbackPromptEnabled;
                if (config.PointerFeedbackPromptDeveloperOnly && !config.DeveloperModeEnabled)
                    shouldShowFeedback = false;

                if (shouldShowFeedback && _latestTarget != null && _latestTarget.Source != PointSource.Calibration)
                {
                    _feedbackWindow.ShowAt(_currentX, _currentY);
                }

                _stateManager.SetState(CompanionState.ReturningToCursor, "Point finished");
            }
        }
        else if (state == CompanionState.ReturningToCursor)
        {
            // Easing to the dynamic cursor
            _currentX += (cursorTargetX - _currentX) * (EasingFactor * 0.5);
            _currentY += (cursorTargetY - _currentY) * (EasingFactor * 0.5);

            if (Math.Abs(cursorTargetX - _currentX) < 2 && Math.Abs(cursorTargetY - _currentY) < 2)
            {
                _stateManager.SetState(CompanionState.FollowingCursor, "Returned to cursor");
            }
        }
        else
        {
            // Follow cursor normally
            if (hasCursor)
            {
                _currentX += (cursorTargetX - _currentX) * EasingFactor;
                _currentY += (cursorTargetY - _currentY) * EasingFactor;
            }
        }

        // Update window position
        Left = _currentX;
        Top = _currentY;

        _responseBubble.UpdatePosition(_currentX, _currentY);

        if (_diagnostics.Visibility == Visibility.Visible && hasCursor)
        {
            var bounds = ScreenUtilities.GetMonitorBounds(new Point(pt.X, pt.Y));
            _diagnostics.UpdateData(
                pt.X, pt.Y, 
                _currentX, _currentY, 
                bounds, 
                state.ToString(), 
                _latestCapture, 
                _latestParsedTag, 
                _latestMappedScreenTarget, 
                _latestStateReason);

            _diagnostics.UpdateVoiceData(
                _pttService.MicService.IsMicrophoneAvailable(),
                _pttService.MicService.IsRecording,
                _pttService.MicService.LastFilePath ?? "-",
                _pttService.MicService.LastDurationMs
            );
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
        _stateManager.StateChanged -= OnStateChanged;
        _pttService.DiagnosticsToggled -= OnDiagnosticsToggled;
        _pttService.TestF10Requested -= OnTestF10Requested;
        _pttService.TestF11Requested -= OnTestF11Requested;
        _configService.ConfigChanged -= OnConfigChanged;
        
        _coordinator.FlightRequested -= OnFlightRequested;
        _coordinator.ResponseBubbleRequested -= OnResponseBubbleRequested;
        _coordinator.DiagnosticsUpdated -= OnInteractionDiagnosticsUpdated;

        if (_resilienceMonitor != null)
        {
            _resilienceMonitor.DisplayTopologyChanged -= OnDisplayTopologyChanged;
        }
        
        _targetMarker.Close();
        _diagnostics.Close();
        _responseBubble.Close();
        _calibrationOverlay.Close();
        _latestCapture?.Dispose();
        
        base.OnClosed(e);
    }
}
