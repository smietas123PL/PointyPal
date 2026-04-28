using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace PointyPal.UI.Controls;

public partial class CoreNodeVisual : UserControl
{
    public CoreNodeVisual()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Fallback: start storyboards from code-behind in case EventTrigger doesn't fire
        // (e.g. when control is added dynamically at runtime)
        if (Resources["OuterGlowPulse"] is Storyboard outerPulse)
            outerPulse.Begin(this, true);

        if (Resources["CenterSpotPulse"] is Storyboard centerPulse)
            centerPulse.Begin(this, true);
    }
}
