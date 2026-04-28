using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PointyPal.Infrastructure;

namespace PointyPal.Overlay;

public partial class PointerVisualControl : UserControl
{
    private Storyboard? _bobAnimation;
    private Storyboard? _pulseAnimation;
    private Storyboard? _spinnerAnimation;
    private Storyboard? _barsAnimation;
    private Storyboard? _speakAnimation;

    public PointerVisualControl()
    {
        InitializeComponent();
        
        _bobAnimation = Resources["BobAnimation"] as Storyboard;
        _pulseAnimation = Resources["PulseAnimation"] as Storyboard;
        _spinnerAnimation = Resources["SpinnerAnimation"] as Storyboard;
        _barsAnimation = Resources["BarsAnimation"] as Storyboard;
        _speakAnimation = Resources["SpeakAnimation"] as Storyboard;

        // Default state
        _bobAnimation?.Begin();
    }

    public void ApplyConfig(AppConfig config)
    {
        double size = Math.Clamp(config.PointerVisualSizeDip, config.PointerVisualMinSizeDip, config.PointerVisualMaxSizeDip);
        double baseSize = 48.0; // The base size of the triangle grid in XAML
        double scale = size / baseSize;
        
        GlobalScale.ScaleX = scale;
        GlobalScale.ScaleY = scale;
        
        StatusScale.ScaleX = config.PointerStatusSlotScale;
        StatusScale.ScaleY = config.PointerStatusSlotScale;

        // Update Pulse animation values based on config
        if (_pulseAnimation != null)
        {
            var scaleXAnim = _pulseAnimation.Children[1] as DoubleAnimation;
            var scaleYAnim = _pulseAnimation.Children[2] as DoubleAnimation;
            if (scaleXAnim != null) scaleXAnim.To = config.PointerVisualGlowScale;
            if (scaleYAnim != null) scaleYAnim.To = config.PointerVisualGlowScale;
        }
    }

    public void UpdateState(CompanionState state, bool isSafeMode = false, bool isDeveloperMode = false)
    {
        // Reset visibilities
        ListeningBars.Visibility = Visibility.Collapsed;
        ProcessingSpinner.Visibility = Visibility.Collapsed;
        SpeakingBars.Visibility = Visibility.Collapsed;
        ErrorBadge.Visibility = Visibility.Collapsed;
        SafeBadge.Visibility = Visibility.Collapsed;
        DevBadge.Visibility = Visibility.Collapsed;
        Aura.Visibility = Visibility.Collapsed;

        // Stop active status animations
        _pulseAnimation?.Stop();
        _spinnerAnimation?.Stop();
        _barsAnimation?.Stop();
        _speakAnimation?.Stop();

        // Update based on state
        switch (state)
        {
            case CompanionState.FollowingCursor:
                break;

            case CompanionState.Listening:
                ListeningBars.Visibility = Visibility.Visible;
                Aura.Visibility = Visibility.Visible;
                _pulseAnimation?.Begin();
                _barsAnimation?.Begin();
                break;

            case CompanionState.Processing:
                ProcessingSpinner.Visibility = Visibility.Visible;
                Aura.Visibility = Visibility.Visible;
                _pulseAnimation?.Begin();
                _spinnerAnimation?.Begin();
                break;

            case CompanionState.Speaking:
                SpeakingBars.Visibility = Visibility.Visible;
                Aura.Visibility = Visibility.Visible;
                _pulseAnimation?.Begin();
                _speakAnimation?.Begin();
                break;

            case CompanionState.Error:
                ErrorBadge.Visibility = Visibility.Visible;
                break;
                
            case CompanionState.FlyingToTarget:
            case CompanionState.PointingAtTarget:
            case CompanionState.ReturningToCursor:
                // Special tilt for pointing
                TriangleGrid.RenderTransform = new RotateTransform(-7, 24, 24);
                return; // Don't reset transform below
        }

        // Reset tilt if not in pointing state
        TriangleGrid.RenderTransform = new TranslateTransform(0, 0);

        // Handle badges (Safe Mode / Developer) if no active state badge
        if (state == CompanionState.FollowingCursor || state == CompanionState.Error)
        {
            if (state == CompanionState.Error)
            {
                ErrorBadge.Visibility = Visibility.Visible;
            }
            else if (isSafeMode)
            {
                SafeBadge.Visibility = Visibility.Visible;
            }
            else if (isDeveloperMode)
            {
                DevBadge.Visibility = Visibility.Visible;
            }
        }
    }
}
