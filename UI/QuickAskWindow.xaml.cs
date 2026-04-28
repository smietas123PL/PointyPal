using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using PointyPal.Core;
using PointyPal.Infrastructure;

namespace PointyPal.UI;

public partial class QuickAskWindow : Window
{
    private const int WmKeyDown = 0x0100;
    private const int VkEscape = 0x1B;

    private readonly InteractionCoordinator _coordinator;
    private readonly ConfigService _configService;
    private HwndSource? _hwndSource;

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
            Activate();
            InputTextBox.Focus();
            Keyboard.Focus(InputTextBox);
            UpdatePlaceholderVisibility();
        };
        this.Activated += (s, e) => {
            InputTextBox.Focus();
            Keyboard.Focus(InputTextBox);
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

        UpdateSurfaceStatus();
        HideInlineMessage();
        UpdatePlaceholderVisibility();
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        SendRequest();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _hwndSource?.AddHook(WindowMessageHook);
    }

    protected override void OnClosed(EventArgs e)
    {
        _hwndSource?.RemoveHook(WindowMessageHook);
        _hwndSource = null;
        base.OnClosed(e);
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmKeyDown && wParam.ToInt32() == VkEscape)
        {
            handled = true;
            Close();
        }

        return IntPtr.Zero;
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

    private void QuickAskWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            this.Close();
        }
    }

    private void SendRequest()
    {
        string text = InputTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            ShowInlineMessage("Type a question first.");
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
        HideInlineMessage();
        UpdatePlaceholderVisibility();
    }

    private void UpdatePlaceholderVisibility()
    {
        if (PlaceholderText == null || InputTextBox == null) return;
        PlaceholderText.Visibility = string.IsNullOrEmpty(InputTextBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdateSurfaceStatus()
    {
        if (_configService.SafeModeActive)
        {
            ApplyStatusPill("SAFE MODE", "QuickAskWarningSurfaceBrush", "QuickAskWarningBorderBrush", "WarningBrush");
            ShowConfigNotice("Safe Mode is active. Real AI answers are disabled.");
            return;
        }

        var policy = new ProviderPolicyService(_configService);
        var validation = policy.ValidateRealProviderConfiguration();
        if (!validation.IsValid)
        {
            ApplyStatusPill("WORKER", "QuickAskWarningSurfaceBrush", "QuickAskWarningBorderBrush", "WarningBrush");
            ShowConfigNotice("PointyPal needs a Worker connection to answer. Open Control Center > Setup.");
            return;
        }

        if (_configService.Config.DeveloperModeEnabled)
        {
            ApplyStatusPill("DEVELOPER", "CyanAccentSubtleBrush", "AccentBorderBrush", "PrimaryAccentBrush");
            HideConfigNotice();
            return;
        }

        ApplyStatusPill("READY", "CyanAccentSubtleBrush", "AccentBorderBrush", "PrimaryAccentBrush");
        HideConfigNotice();
    }

    private void ApplyStatusPill(string text, string backgroundBrushKey, string borderBrushKey, string textBrushKey)
    {
        StatusPillText.Text = text;
        StatusPill.Background = ResourceBrush(backgroundBrushKey, Brushes.Transparent);
        StatusPill.BorderBrush = ResourceBrush(borderBrushKey, Brushes.Transparent);
        StatusPillText.Foreground = ResourceBrush(textBrushKey, Brushes.White);
    }

    private void ShowConfigNotice(string message)
    {
        ConfigNoticeText.Text = message;
        ConfigNoticeBorder.Visibility = Visibility.Visible;
    }

    private void HideConfigNotice()
    {
        ConfigNoticeText.Text = "";
        ConfigNoticeBorder.Visibility = Visibility.Collapsed;
    }

    private void ShowInlineMessage(string message)
    {
        StatusTextBlock.Text = message;
        StatusMessageBorder.Visibility = Visibility.Visible;
    }

    private void HideInlineMessage()
    {
        StatusTextBlock.Text = "";
        StatusMessageBorder.Visibility = Visibility.Collapsed;
    }

    private Brush ResourceBrush(string key, Brush fallback)
    {
        return TryFindResource(key) as Brush ?? fallback;
    }
}
