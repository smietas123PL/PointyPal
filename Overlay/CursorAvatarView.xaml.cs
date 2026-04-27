using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PointyPal.Infrastructure;

namespace PointyPal.Overlay;

public partial class CursorAvatarView : System.Windows.Controls.UserControl
{
    private Storyboard? _pulseAnimation;

    public CursorAvatarView()
    {
        InitializeComponent();
        _pulseAnimation = (Storyboard)Resources["PulseAnimation"];
    }

    public void UpdateState(CompanionState state)
    {
        _pulseAnimation?.Stop();

        switch (state)
        {
            case CompanionState.FollowingCursor:
                AvatarCore.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(77, 208, 225)); // Light Blue
                GlowEffect.Color = System.Windows.Media.Color.FromRgb(0, 188, 212);
                GlowEffect.BlurRadius = 15;
                break;
            case CompanionState.Listening:
                AvatarCore.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(129, 199, 132)); // Light Green
                GlowEffect.Color = System.Windows.Media.Color.FromRgb(76, 175, 80);
                GlowEffect.BlurRadius = 25;
                break;
            case CompanionState.Processing:
                AvatarCore.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 213, 79)); // Light Yellow
                GlowEffect.Color = System.Windows.Media.Color.FromRgb(255, 193, 7);
                _pulseAnimation?.Begin();
                break;
            case CompanionState.Error:
                AvatarCore.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 115, 115)); // Light Red
                GlowEffect.Color = System.Windows.Media.Color.FromRgb(244, 67, 54);
                GlowEffect.BlurRadius = 15;
                break;
            case CompanionState.Hidden:
                AvatarCore.Fill = System.Windows.Media.Brushes.Transparent;
                GlowEffect.Color = System.Windows.Media.Colors.Transparent;
                break;
        }
    }
}
