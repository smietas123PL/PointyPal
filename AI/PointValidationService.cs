using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using PointyPal.Capture;
using PointyPal.Infrastructure;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace PointyPal.AI;

public class PointValidationService
{
    private readonly ConfigService _configService;

    public PointValidationService(ConfigService configService)
    {
        _configService = configService;
    }

    public (PointingAttempt Attempt, PointValidationResult Validation) ProcessPoint(
        PointTag tag, 
        CaptureResult capture, 
        UiAutomationContext? uiContext, 
        Point initialMappedPoint,
        string userText,
        string providerName,
        string rawResponse,
        string cleanResponse)
    {
        var config = _configService.Config;
        var attempt = new PointingAttempt
        {
            Timestamp = DateTime.Now,
            UserText = userText,
            ProviderName = providerName,
            ScreenshotWidth = capture.Image.Width,
            ScreenshotHeight = capture.Image.Height,
            MonitorBounds = ScreenUtilities.GetMonitorBounds(initialMappedPoint),
            RawAiResponse = rawResponse,
            CleanResponse = cleanResponse,
            ParsedPointImageX = tag.HasPoint ? tag.X : null,
            ParsedPointImageY = tag.HasPoint ? tag.Y : null,
            ParsedPointLabel = tag.Label,
            MappedScreenX = initialMappedPoint.X,
            MappedScreenY = initialMappedPoint.Y,
            FinalPointScreenX = initialMappedPoint.X,
            FinalPointScreenY = initialMappedPoint.Y
        };

        var validation = new PointValidationResult
        {
            HasPoint = tag.HasPoint,
            IsValid = true
        };

        if (!tag.HasPoint)
        {
            validation.IsValid = true;
            return (attempt, validation);
        }

        // 1. Image bounds check
        bool inImageBounds = tag.X >= 0 && tag.X < capture.Image.Width &&
                             tag.Y >= 0 && tag.Y < capture.Image.Height;
        validation.InImageBounds = inImageBounds;
        
        if (!inImageBounds)
        {
            attempt.WasPointClamped = true;
            validation.WasClamped = true;
        }

        // 2. Monitor bounds check
        var monitorBounds = attempt.MonitorBounds;
        validation.InMonitorBounds = monitorBounds.Contains(initialMappedPoint);

        // 3. Virtual desktop bounds check
        var virtualBounds = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        validation.InVirtualDesktopBounds = virtualBounds.Contains(initialMappedPoint);

        // 4. UI Automation Context analysis
        if (uiContext != null && uiContext.IsAvailable)
        {
            attempt.UiElementUnderMappedPoint = uiContext.ElementUnderCursor?.Name ?? uiContext.ElementUnderCursor?.ControlType;
            
            var (nearest, distance) = FindNearestUsefulElement(initialMappedPoint, uiContext.NearbyElements, config);
            if (nearest != null)
            {
                attempt.NearestUiElement = $"{nearest.Name} ({nearest.ControlType})";
                attempt.DistanceToNearestUiElement = distance;

                // 5. Snapping logic
                if (config.PointSnappingEnabled && distance <= config.PointSnappingMaxDistancePx)
                {
                    var center = new Point(
                        nearest.BoundingRectangle.Left + nearest.BoundingRectangle.Width / 2,
                        nearest.BoundingRectangle.Top + nearest.BoundingRectangle.Height / 2);
                    
                    attempt.FinalPointScreenX = center.X;
                    attempt.FinalPointScreenY = center.Y;
                    
                    if (IsButtonLike(nearest.ControlType))
                        attempt.AdjustmentReason = "SnappedToNearestButton";
                    else
                        attempt.AdjustmentReason = "SnappedToNearestUiElement";
                }
                else if (config.PointSnappingEnabled)
                {
                    attempt.AdjustmentReason = "NoSnapDistanceTooLarge";
                }
            }
            else if (config.PointSnappingEnabled)
            {
                attempt.AdjustmentReason = "NoSnapNoNearbyElements";
            }
        }
        else if (config.PointSnappingEnabled)
        {
            attempt.AdjustmentReason = "NoUiAutomationContext";
        }

        return (attempt, validation);
    }

    private (UiElementInfo? element, double distance) FindNearestUsefulElement(Point point, List<UiElementInfo> elements, AppConfig config)
    {
        if (elements == null || elements.Count == 0) return (null, 0);

        UiElementInfo? bestMatch = null;
        double minDistance = double.MaxValue;

        foreach (var el in elements)
        {
            if (el.BoundingRectangle.IsEmpty) continue;

            var center = new Point(
                el.BoundingRectangle.Left + el.BoundingRectangle.Width / 2,
                el.BoundingRectangle.Top + el.BoundingRectangle.Height / 2);

            double dist = Math.Sqrt(Math.Pow(center.X - point.X, 2) + Math.Pow(center.Y - point.Y, 2));

            bool isUseful = IsUsefulControl(el.ControlType);
            bool isButton = IsButtonLike(el.ControlType);

            if (!isUseful) continue;

            // Simple heuristic: if it's a button and we prefer buttons, give it a "bonus" (virtual reduction in distance)
            double effectiveDist = dist;
            if (config.PointSnappingPreferButtons && isButton)
            {
                effectiveDist *= 0.7; // Bias towards buttons
            }

            if (effectiveDist < minDistance)
            {
                minDistance = effectiveDist;
                bestMatch = el;
            }
        }

        if (bestMatch != null)
        {
            // Calculate actual distance to center for the return value
            var center = new Point(
                bestMatch.BoundingRectangle.Left + bestMatch.BoundingRectangle.Width / 2,
                bestMatch.BoundingRectangle.Top + bestMatch.BoundingRectangle.Height / 2);
            double actualDist = Math.Sqrt(Math.Pow(center.X - point.X, 2) + Math.Pow(center.Y - point.Y, 2));
            return (bestMatch, actualDist);
        }

        return (null, 0);
    }

    private bool IsUsefulControl(string? controlType)
    {
        if (string.IsNullOrEmpty(controlType)) return false;
        string ct = controlType.Replace("ControlType.", "");
        return ct == "Button" || ct == "MenuItem" || ct == "CheckBox" || ct == "RadioButton" || 
               ct == "Hyperlink" || ct == "ListItem" || ct == "TreeItem" || ct == "TabItem" || ct == "Edit";
    }

    private bool IsButtonLike(string? controlType)
    {
        if (string.IsNullOrEmpty(controlType)) return false;
        string ct = controlType.Replace("ControlType.", "");
        return ct == "Button" || ct == "MenuItem" || ct == "TabItem";
    }
}
