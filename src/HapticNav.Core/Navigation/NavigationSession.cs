using HapticNav.Core.Ble;
using HapticNav.Core.Geo;
using HapticNav.Core.Protocol;

namespace HapticNav.Core.Navigation;

/// <summary>
/// One guided walk: feeds position fixes into the engine and pushes any
/// resulting command to the bracelet over the link (FR-02, FR-03, FR-07).
/// Keeps no state beyond the current step index: no location history is
/// recorded anywhere (NFR-06).
/// </summary>
public sealed class NavigationSession
{
    private readonly NavigationEngine _engine;
    private readonly IBraceletLink _link;

    public NavigationSession(NavigationEngine engine, IBraceletLink link)
    {
        _engine = engine;
        _link = link;
    }

    public bool IsLinked => _link.IsConnected;
    public int StepIndex => _engine.StepIndex;

    /// <summary>Processes a GPS fix, sends the resulting command, and returns the update.</summary>
    public NavigationUpdate ProcessFix(GeoPoint position, double headingDegrees)
    {
        NavigationUpdate update = _engine.Update(position, headingDegrees);
        if (update.Command is { } command && _link.IsConnected)
            _link.Send(command);
        return update;
    }

    /// <summary>Manually sends a command (used to demo FR-04 connection-lost handling).</summary>
    public bool SendManual(DirectionCommand command) =>
        _link.IsConnected && _link.Send(command);
}
