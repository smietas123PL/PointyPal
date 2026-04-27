using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using PointyPal.Infrastructure;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace PointyPal.Capture;

public class UiAutomationContextService
{
    private readonly ConfigService _configService;

    public UiAutomationContextService(ConfigService configService)
    {
        _configService = configService;
    }

    public async Task<UiAutomationContext> CaptureContextAsync(Point cursorPoint, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var context = new UiAutomationContext
        {
            CapturedAt = DateTime.Now
        };

        if (!_configService.Config.UiAutomationEnabled)
        {
            context.IsAvailable = false;
            context.ErrorMessage = "UI Automation is disabled in config.";
            return context;
        }

        try
        {
            // We use a Task to wrap the blocking UI Automation calls and allow for a timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            int timeoutMs = _configService.Config.UiAutomationTimeoutMs > 0
                ? _configService.Config.UiAutomationTimeoutMs
                : 1000;
            cts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));

            await Task.Run(() =>
            {
                CollectBasicInfo(context);
                CollectElementInfo(context, cursorPoint, cts.Token);
            }, cts.Token);

            context.IsAvailable = true;
        }
        catch (OperationCanceledException)
        {
            context.IsAvailable = false;
            context.ErrorMessage = "UI Automation collection timed out.";
        }
        catch (Exception ex)
        {
            context.IsAvailable = false;
            context.ErrorMessage = $"UI Automation error: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"UI Automation capture failed: {ex}");
        }

        stopwatch.Stop();
        context.CollectionDurationMs = stopwatch.Elapsed.TotalMilliseconds;
        return context;
    }

    private void CollectBasicInfo(UiAutomationContext context)
    {
        IntPtr foregroundWindow = NativeMethods.GetForegroundWindow();
        if (foregroundWindow != IntPtr.Zero)
        {
            var titleBuilder = new StringBuilder(256);
            if (NativeMethods.GetWindowText(foregroundWindow, titleBuilder, titleBuilder.Capacity) > 0)
            {
                context.ActiveWindowTitle = titleBuilder.ToString();
            }

            if (NativeMethods.GetWindowThreadProcessId(foregroundWindow, out uint processId) != 0)
            {
                try
                {
                    using var process = Process.GetProcessById((int)processId);
                    context.ActiveProcessName = process.ProcessName;
                }
                catch
                {
                    // Ignore process name if access denied
                }
            }
        }
    }

    private void CollectElementInfo(UiAutomationContext context, Point cursorPoint, CancellationToken ct)
    {
        try
        {
            // 1. Element under cursor
            var elementAtPoint = AutomationElement.FromPoint(cursorPoint);
            if (elementAtPoint != null)
            {
                context.ElementUnderCursor = MapToInfo(elementAtPoint, cursorPoint);
            }

            if (ct.IsCancellationRequested) return;

            // 2. Focused element
            var focusedElement = AutomationElement.FocusedElement;
            if (focusedElement != null)
            {
                context.FocusedElement = MapToInfo(focusedElement, cursorPoint);
            }

            if (ct.IsCancellationRequested) return;

            // 3. Nearby elements
            CollectNearbyElements(context, cursorPoint, ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during element info collection: {ex.Message}");
        }
    }

    private void CollectNearbyElements(UiAutomationContext context, Point cursorPoint, CancellationToken ct)
    {
        double radius = _configService.Config.UiAutomationRadiusPx;
        int maxElements = _configService.Config.MaxUiElementsInPrompt;

        // Define search area
        var searchRect = new Rect(
            cursorPoint.X - radius,
            cursorPoint.Y - radius,
            radius * 2,
            radius * 2);

        // To avoid scanning everything (which is slow), we find the top-level windows that intersect our search area
        // and scan their descendants.
        var condition = new AndCondition(
            new PropertyCondition(AutomationElement.IsOffscreenProperty, false),
            new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ComboBox),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.RadioButton),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem)
            )
        );

        // For performance, we limit the search to descendants of the window under the cursor if possible, 
        // or just the root element descendants with a depth limit if available (not really in S.W.A).
        // Standard approach: RootElement.FindAll is slow. Let's try to find the window first.
        
        AutomationElement searchRoot = AutomationElement.RootElement;
        
        // Find top level windows that might contain our search area
        var windows = searchRoot.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window));
        
        var nearbyElements = new List<UiElementInfo>();

        foreach (AutomationElement window in windows)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var bounds = window.Current.BoundingRectangle;
                if (bounds.IntersectsWith(searchRect))
                {
                    // Scan this window for interesting controls
                    var elements = window.FindAll(TreeScope.Descendants, condition);
                    foreach (AutomationElement element in elements)
                    {
                        if (ct.IsCancellationRequested) break;

                        var info = MapToInfo(element, cursorPoint);
                        if (info != null && searchRect.IntersectsWith(info.BoundingRectangle))
                        {
                            // Skip huge containers unless they are small enough or have names
                            if (info.BoundingRectangle.Width > 1000 && info.BoundingRectangle.Height > 1000)
                            {
                                if (string.IsNullOrEmpty(info.Name) && string.IsNullOrEmpty(info.AutomationId))
                                    continue;
                            }

                            // Skip if empty name AND empty automation id
                            if (string.IsNullOrEmpty(info.Name) && string.IsNullOrEmpty(info.AutomationId))
                                continue;

                            nearbyElements.Add(info);
                        }
                    }
                }
            }
            catch
            {
                // Window might have closed or access denied
            }
        }

        context.NearbyElements = nearbyElements
            .OrderBy(e => e.DistanceFromCursor)
            .Take(maxElements)
            .ToList();
    }

    private UiElementInfo? MapToInfo(AutomationElement element, Point cursorPoint)
    {
        try
        {
            var current = element.Current;
            var info = new UiElementInfo
            {
                Name = current.Name,
                AutomationId = current.AutomationId,
                ClassName = current.ClassName,
                ControlType = current.ControlType.ProgrammaticName,
                LocalizedControlType = current.LocalizedControlType,
                BoundingRectangle = current.BoundingRectangle,
                IsEnabled = current.IsEnabled,
                IsOffscreen = current.IsOffscreen,
                HasKeyboardFocus = current.HasKeyboardFocus,
                ProcessId = current.ProcessId
            };

            // Calculate distance from cursor to center of element
            var center = new Point(
                info.BoundingRectangle.Left + info.BoundingRectangle.Width / 2,
                info.BoundingRectangle.Top + info.BoundingRectangle.Height / 2);
            
            info.DistanceFromCursor = Math.Sqrt(Math.Pow(center.X - cursorPoint.X, 2) + Math.Pow(center.Y - cursorPoint.Y, 2));

            return info;
        }
        catch
        {
            return null;
        }
    }
}
