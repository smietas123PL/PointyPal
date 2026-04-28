using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using PointyPal.Capture;
using PointyPal.Core;
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
        var mapper = new CoordinateMapper();
        
        var target = new PointerTarget
        {
            OriginalImagePoint = new Point(tag.X, tag.Y),
            Label = tag.Label,
            Source = PointSource.ClaudePoint,
            Confidence = PointConfidence.Unknown,
            MonitorDeviceName = capture.Geometry.MonitorDeviceName,
            CaptureGeometrySummary = capture.Geometry.ToString()
        };

        var attempt = new PointingAttempt
        {
            Timestamp = DateTime.Now,
            UserText = userText,
            ProviderName = providerName,
            ScreenshotWidth = capture.Image.Width,
            ScreenshotHeight = capture.Image.Height,
            MonitorBounds = capture.Geometry.MonitorBoundsPhysical,
            RawAiResponse = rawResponse,
            CleanResponse = cleanResponse,
            Target = target
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

        // 1. Hardened mapping and clamping
        target.ClampedImagePoint = mapper.ClampImagePoint(target.OriginalImagePoint, capture.Geometry);
        var mappingResult = mapper.ImageToScreenPhysical(target.OriginalImagePoint, capture.Geometry);
        
        target.WasClamped = mappingResult.WasClamped;
        target.MappedScreenPhysicalPoint = mappingResult.OutputPoint;
        target.FinalScreenPhysicalPoint = mappingResult.OutputPoint;
        
        validation.InImageBounds = !mappingResult.WasClamped;
        validation.WasClamped = mappingResult.WasClamped;

        // 2. Monitor bounds check
        validation.InMonitorBounds = capture.Geometry.MonitorBoundsPhysical.Contains(target.MappedScreenPhysicalPoint);

        // 3. Virtual desktop bounds check
        validation.InVirtualDesktopBounds = capture.Geometry.VirtualScreenBounds.Contains(target.MappedScreenPhysicalPoint);

        // 4. UI Automation Context analysis & Snapping
        if (uiContext != null && uiContext.IsAvailable)
        {
            attempt.UiElementUnderMappedPoint = uiContext.ElementUnderCursor?.Name ?? uiContext.ElementUnderCursor?.ControlType;
            
            var (nearest, distance) = FindNearestUsefulElement(target.MappedScreenPhysicalPoint, uiContext.NearbyElements, config);
            if (nearest != null)
            {
                target.NearestUiElementName = nearest.Name;
                target.NearestUiElementType = nearest.ControlType;
                target.DistanceToNearestUiElement = distance;

                // 5. Snapping logic polish
                bool shouldSnap = config.PointSnappingEnabled && distance <= config.PointSnappingMaxDistancePx;
                
                // Don't snap if nearest is a huge container
                if (shouldSnap && nearest.BoundingRectangle.Width > 800 && nearest.BoundingRectangle.Height > 600)
                {
                    shouldSnap = false;
                    target.AdjustmentReason = "NoSnapHugeElement";
                }

                if (shouldSnap)
                {
                    Point snapPoint;
                    if (config.PointSnappingSnapToElementCenter)
                    {
                        snapPoint = new Point(
                            nearest.BoundingRectangle.Left + nearest.BoundingRectangle.Width / 2,
                            nearest.BoundingRectangle.Top + nearest.BoundingRectangle.Height / 2);
                    }
                    else
                    {
                        snapPoint = new Point(
                            nearest.BoundingRectangle.Left + nearest.BoundingRectangle.Width / 2,
                            nearest.BoundingRectangle.Top + nearest.BoundingRectangle.Height / 2);
                    }
                    
                    target.FinalScreenPhysicalPoint = snapPoint;
                    target.WasSnapped = true;
                    target.Source = PointSource.UiAutomationSnap;
                    
                    if (IsButtonLike(nearest.ControlType))
                        target.AdjustmentReason = "SnappedToNearestButton";
                    else
                        target.AdjustmentReason = "SnappedToNearestUiElement";
                }
                else if (config.PointSnappingEnabled && string.IsNullOrEmpty(target.AdjustmentReason))
                {
                    target.AdjustmentReason = "NoSnapDistanceTooLarge";
                }
            }
            else if (config.PointSnappingEnabled)
            {
                target.AdjustmentReason = "NoSnapNoNearbyElements";
            }
        }
        else if (config.PointSnappingEnabled)
        {
            target.AdjustmentReason = "NoUiAutomationContext";
        }

        // 6. Convert final physical point to DIPs for the overlay
        var dipResult = mapper.ScreenPhysicalToOverlayDip(target.FinalScreenPhysicalPoint, capture.Geometry);
        target.FinalOverlayDipPoint = dipResult.OutputPoint;

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
