using System.Windows;
using System.Windows.Controls;

namespace PointyPal.UI.Controls;

public partial class ConcentricCircles : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(ConcentricCircles),
            new PropertyMetadata(string.Empty, OnLabelChanged));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ConcentricCircles ctrl)
            ctrl.LabelText.Text = (string)e.NewValue;
    }

    public ConcentricCircles()
    {
        InitializeComponent();
    }
}
