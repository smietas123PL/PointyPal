using System;
using System.Windows;
using Point = System.Windows.Point;

namespace PointyPal.Overlay;

public class PointerFlightAnimator
{
    private Point _start;
    private Point _target;
    private Point _control;
    private TimeSpan _duration;
    private DateTime _startTime;

    public void Start(Point start, Point target, TimeSpan duration)
    {
        _start = start;
        _target = target;
        _duration = duration;
        _startTime = DateTime.Now;

        // Simple control point for a quadratic Bezier curve
        // We'll add a little "arc" by pulling the control point up
        double distance = Math.Sqrt(Math.Pow(target.X - start.X, 2) + Math.Pow(target.Y - start.Y, 2));
        double midX = (start.X + target.X) / 2;
        double midY = (start.Y + target.Y) / 2;
        _control = new Point(midX, midY - (distance * 0.2)); // Pull up by 20% of distance
    }

    public Point GetPosition(DateTime now)
    {
        var elapsed = now - _startTime;
        double t = elapsed.TotalMilliseconds / _duration.TotalMilliseconds;
        if (t > 1.0) t = 1.0;
        if (t < 0.0) t = 0.0;

        // Apply an easing function (e.g., easeInOutQuad)
        double easeT = t < 0.5 ? 2 * t * t : -1 + (4 - 2 * t) * t;

        // Quadratic Bezier interpolation
        double x = Math.Pow(1 - easeT, 2) * _start.X + 2 * (1 - easeT) * easeT * _control.X + Math.Pow(easeT, 2) * _target.X;
        double y = Math.Pow(1 - easeT, 2) * _start.Y + 2 * (1 - easeT) * easeT * _control.Y + Math.Pow(easeT, 2) * _target.Y;

        return new Point(x, y);
    }

    public bool IsFinished(DateTime now)
    {
        return (now - _startTime) >= _duration;
    }
}
