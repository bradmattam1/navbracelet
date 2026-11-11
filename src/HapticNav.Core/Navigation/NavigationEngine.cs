using HapticNav.Core.Geo;
using HapticNav.Core.Protocol;

namespace HapticNav.Core.Navigation;

public enum NavigationStatus
{
    EnRoute,
    OffRoute,
    Arrived
}

public sealed record NavigationUpdate(
    NavigationStatus Status,
    int StepIndex,
    string NextInstruction,
    double DistanceToNextMeters,
    double OffRouteDistanceMeters,
    DirectionCommand? Command);

/// <summary>
/// Decides which direction command to send for the current position and heading.
/// Works on bearing differences only; coordinates never leave the phone (NFR-06).
/// </summary>
public sealed class NavigationEngine
{
    public double ArrivalRadiusMeters { get; init; } = 8.0;
    public double OffRouteThresholdMeters { get; init; } = 25.0;
    public double TurnAngleThresholdDegrees { get; init; } = 45.0;
    public double UrgentTurnAngleDegrees { get; init; } = 150.0;

    private readonly WalkingRoute _route;
    private int _stepIndex;

    public NavigationEngine(WalkingRoute route) => _route = route;

    public int StepIndex => _stepIndex;

    /// <summary>
    /// Processes one position fix. The returned command is what the app sends
    /// to the bracelet for this update; null when no new signal is needed.
    /// </summary>
    public NavigationUpdate Update(GeoPoint position, double headingDegrees)
    {
        if (_stepIndex >= _route.Steps.Count - 1)
            return new NavigationUpdate(NavigationStatus.Arrived, _stepIndex,
                _route.LastStep.Instruction, 0, 0, null);

        GeoPoint target = _route.Steps[_stepIndex + 1].Point;
        double offRoute = DistanceFromSegmentMeters(position, _route.Steps[_stepIndex].Point, target);

        if (offRoute > OffRouteThresholdMeters)
            return new NavigationUpdate(NavigationStatus.OffRoute, _stepIndex,
                _route.Steps[_stepIndex].Instruction,
                position.DistanceMetersTo(target), offRoute, DirectionCommand.OffRoute);

        double distance = position.DistanceMetersTo(target);
        if (distance <= ArrivalRadiusMeters)
        {
            _stepIndex++;
            bool finished = _stepIndex >= _route.Steps.Count - 1;
            return new NavigationUpdate(
                finished ? NavigationStatus.Arrived : NavigationStatus.EnRoute,
                _stepIndex,
                _route.Steps[_stepIndex].Instruction,
                finished ? 0 : position.DistanceMetersTo(_route.Steps[_stepIndex + 1].Point),
                offRoute,
                finished ? DirectionCommand.Arrived : null);
        }

        double bearing = position.BearingDegreesTo(target);
        double diff = AngleDifference(bearing, headingDegrees);

        DirectionCommand? command = diff switch
        {
            var d when d > UrgentTurnAngleDegrees || d < -UrgentTurnAngleDegrees => DirectionCommand.TurnNow,
            var d when d > TurnAngleThresholdDegrees => DirectionCommand.TurnRight,
            var d when d < -TurnAngleThresholdDegrees => DirectionCommand.TurnLeft,
            _ => null
        };

        return new NavigationUpdate(NavigationStatus.EnRoute, _stepIndex,
            _route.Steps[_stepIndex + 1].Instruction, distance, offRoute, command);
    }

    /// <summary>Signed difference between two bearings in (-180, 180]; positive means "target is to the right".</summary>
    public static double AngleDifference(double bearingToTarget, double heading)
    {
        double diff = (bearingToTarget - heading) % 360.0;
        if (diff > 180.0) diff -= 360.0;
        if (diff <= -180.0) diff += 360.0;
        return diff;
    }

    /// <summary>Shortest distance from <paramref name="p"/> to the segment <paramref name="a"/>-<paramref name="b"/> in metres.</summary>
    public static double DistanceFromSegmentMeters(GeoPoint p, GeoPoint a, GeoPoint b)
    {
        double length = a.DistanceMetersTo(b);
        if (length < 0.001)
            return p.DistanceMetersTo(a);

        double bearing = a.BearingDegreesTo(b);
        double along = Math.Clamp(ProjectedDistanceMeters(a, p, bearing), 0, length);
        GeoPoint projected = a.Move(along, bearing);
        return p.DistanceMetersTo(projected);
    }

    private static double ProjectedDistanceMeters(GeoPoint a, GeoPoint p, double segmentBearing)
    {
        double bearingToP = a.BearingDegreesTo(p);
        double distance = a.DistanceMetersTo(p);
        double angle = AngleDifference(bearingToP, segmentBearing) * Math.PI / 180.0;
        return distance * Math.Cos(angle);
    }
}
