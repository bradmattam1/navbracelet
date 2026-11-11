using HapticNav.Core.Ble;
using HapticNav.Core.Geo;
using HapticNav.Core.Navigation;
using HapticNav.Core.Protocol;
using Xunit;

namespace HapticNav.Core.Tests;

public class NavigationEngineTests
{
    private static readonly GeoPoint A = new(51.44553, 5.48974);
    private static readonly GeoPoint B = new(51.44601, 5.49031);

    private static NavigationEngine NewEngine()
    {
        var route = new WalkingRoute("test", new[]
        {
            new RouteStep(A, "start"),
            new RouteStep(B, "walk north-east"),
            new RouteStep(A, "back")
        });
        return new NavigationEngine(route);
    }

    [Fact]
    public void StraightAheadGivesNoCommand()
    {
        NavigationEngine engine = NewEngine();
        double bearing = A.BearingDegreesTo(B);
        NavigationUpdate update = engine.Update(A, bearing);
        Assert.Equal(NavigationStatus.EnRoute, update.Status);
        Assert.Null(update.Command);
    }

    [Fact]
    public void TargetToTheRightRequestsTurnRight()
    {
        NavigationEngine engine = NewEngine();
        double bearing = A.BearingDegreesTo(B);
        NavigationUpdate update = engine.Update(A, bearing - 90);
        Assert.Equal(DirectionCommand.TurnRight, update.Command);
    }

    [Fact]
    public void TargetToTheLeftRequestsTurnLeft()
    {
        NavigationEngine engine = NewEngine();
        double bearing = A.BearingDegreesTo(B);
        NavigationUpdate update = engine.Update(A, bearing + 90);
        Assert.Equal(DirectionCommand.TurnLeft, update.Command);
    }

    [Fact]
    public void WalkingAwayRequestsUrgentTurn()
    {
        NavigationEngine engine = NewEngine();
        double bearing = A.BearingDegreesTo(B);
        NavigationUpdate update = engine.Update(A, bearing + 180);
        Assert.Equal(DirectionCommand.TurnNow, update.Command);
    }

    [Fact]
    public void StrayingTooFarReportsOffRoute()
    {
        NavigationEngine engine = NewEngine();
        double bearing = A.BearingDegreesTo(B);
        GeoPoint far = A.Move(60, bearing + 90);
        NavigationUpdate update = engine.Update(far, bearing);
        Assert.Equal(NavigationStatus.OffRoute, update.Status);
        Assert.Equal(DirectionCommand.OffRoute, update.Command);
    }

    [Fact]
    public void ReachingLastWaypointArrives()
    {
        var route = new WalkingRoute("short", new[]
        {
            new RouteStep(A, "start"),
            new RouteStep(B, "walk there")
        });
        var engine = new NavigationEngine(route);
        double bearing = A.BearingDegreesTo(B);

        NavigationUpdate enRoute = engine.Update(A.Move(5, bearing), bearing);
        Assert.Equal(NavigationStatus.EnRoute, enRoute.Status);

        NavigationUpdate update = engine.Update(B, bearing);
        Assert.Equal(NavigationStatus.Arrived, update.Status);
        Assert.Equal(DirectionCommand.Arrived, update.Command);
    }

    [Fact]
    public void AngleDifferenceIsSignedAndBounded()
    {
        Assert.Equal(90, NavigationEngine.AngleDifference(90, 0), 5);
        Assert.Equal(-90, NavigationEngine.AngleDifference(270, 0), 5);
        Assert.Equal(10, NavigationEngine.AngleDifference(370, 0), 5);
        Assert.Equal(0, NavigationEngine.AngleDifference(45, 45), 5);
    }

    [Fact]
    public void DemoRouteIsAPresentableLoop()
    {
        WalkingRoute route = WalkingRoute.DemoCampusRoute();
        Assert.Equal(route.Steps[0].Point, route.LastStep.Point);
        Assert.True(route.TotalLengthMeters() > 100);
    }

    [Fact]
    public void SessionSendsCommandsOverLink()
    {
        var sink = new ConsoleVibrationSink();
        var link = new SimulatedBraceletLink("phone-sanne", sink);
        var bracelet = new BraceletDevice("bracelet-1", "1234");
        link.Connect(bracelet, "1234");

        var engine = new NavigationEngine(new WalkingRoute("t", new[]
        {
            new RouteStep(A, "start"),
            new RouteStep(B, "go")
        }));
        var session = new NavigationSession(engine, link);

        double bearing = A.BearingDegreesTo(B);
        NavigationUpdate update = session.ProcessFix(A, bearing + 90);

        Assert.Equal(DirectionCommand.TurnLeft, update.Command);
        Assert.True(sink.PulseBuffer.Count > 0, "the vibration sink should have received pulses");
    }
}
