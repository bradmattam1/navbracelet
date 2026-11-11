using HapticNav.Core.Geo;

namespace HapticNav.Core.Navigation;

public sealed record RouteStep(GeoPoint Point, string Instruction);

/// <summary>A walking route: ordered waypoints with human-readable instructions (FR-03).</summary>
public sealed record WalkingRoute(string Name, IReadOnlyList<RouteStep> Steps)
{
    public RouteStep LastStep => Steps[^1];

    public double TotalLengthMeters()
    {
        double total = 0;
        for (int i = 1; i < Steps.Count; i++)
            total += Steps[i - 1].Point.DistanceMetersTo(Steps[i].Point);
        return total;
    }

    /// <summary>
    /// The fixed demo route used for validation (FR-03): a short loop around the
    /// Rachelsmolen campus in Eindhoven, suitable for a supervised test walk.
    /// </summary>
    public static WalkingRoute DemoCampusRoute() => new(
        "Rachelsmolen campus demo loop",
        new[]
        {
            new RouteStep(new GeoPoint(51.44553, 5.48974), "Start at the main entrance"),
            new RouteStep(new GeoPoint(51.44601, 5.49031), "Head north past the bike racks"),
            new RouteStep(new GeoPoint(51.44642, 5.49126), "Turn right onto the main path"),
            new RouteStep(new GeoPoint(51.44598, 5.49215), "Follow the path east past the pond"),
            new RouteStep(new GeoPoint(51.44531, 5.49187), "Turn left at the crossing"),
            new RouteStep(new GeoPoint(51.44494, 5.49062), "Walk south along the building"),
            new RouteStep(new GeoPoint(51.44553, 5.48974), "Arrive back at the main entrance")
        });
}
