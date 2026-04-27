using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PointyPal.Core;
using PointyPal.Infrastructure;

namespace PointyPal.UI;

public partial class QuickAskWindow : Window
{
    private readonly InteractionCoordinator _coordinator;
    private readonly ConfigService _configService;

    public QuickAskWindow(InteractionCoordinator coordinator, ConfigService configService)
    {
        InitializeComponent();
        _coordinator = coordinator;
        _configService = configService;

        // Position near cursor
        NativeMethods.GetCursorPos(out var pt);
        this.Left = pt.X + 20;
        this.Top = pt.Y - 100;

        // Ensure within screen bounds
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(pt.X, pt.Y));
        if (this.Left + this.Width > screen.Bounds.Right) this.Left = pt.X - this.Width - 20;
        if (this.Top + this.Height > screen.Bounds.Bottom) this.Top = pt.Y - this.Height - 20;
        if (this.Top < screen.Bounds.Top) this.Top = screen.Bounds.Top + 20;

        // Default values from config
        SetInitialState();
        
        this.Loaded += (s, e) => {
            InputTextBox.Focus();
        };
    }

    private void SetInitialState()
    {
        var config = _configService.Config;
        
        // Mode mapping
        string defaultMode = config.QuickAskDefaultMode.ToString();
        foreach (ComboBoxItem item in ModeComboBox.Items)
        {
            if (item.Tag.ToString() == defaultMode)
            {
                ModeComboBox.SelectedItem = item;
                break;
            }
        }

        ScreenshotToggle.IsChecked = config.ScreenshotEnabled;
        UiAutomationToggle.IsChecked = config.UiAutomationEnabled;
        TtsToggle.IsChecked = config.TtsEnabled;
        PointerToggle.IsChecked = config.PointerFlightEnabled;
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        SendRequest();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void InputTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            SendRequest();
        }
        else if (e.Key == Key.Escape)
        {
            this.Close();
        }
    }

    private void SendRequest()
    {
        string text = InputTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            StatusTextBlock.Text = "Wpisz coś najpierw.";
            return;
        }

        var mode = InteractionMode.Assist;
        if (ModeComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
        {
            Enum.TryParse(selectedItem.Tag.ToString(), out mode);
        }

        // Apply temporary config overrides for this interaction
        // We'll pass these to StartInteractionAsync if we update it, 
        // or we can temporarily change config (not ideal)
        // Better: Update InteractionCoordinator.StartInteractionAsync to take options
        
        var options = new InteractionOptions
        {
            InteractionMode = mode,
            InteractionSource = InteractionSource.QuickAsk,
            ScreenshotEnabled = ScreenshotToggle.IsChecked ?? true,
            UiAutomationEnabled = UiAutomationToggle.IsChecked ?? true,
            TtsEnabled = TtsToggle.IsChecked ?? true,
            PointerFlightEnabled = PointerToggle.IsChecked ?? true
        };

        this.Close();
        
        // Start interaction in background
        _ = _coordinator.StartInteractionAsync(text, ProviderOverride.None, null, options);
    }

    private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        StatusTextBlock.Text = "";
    }
}

