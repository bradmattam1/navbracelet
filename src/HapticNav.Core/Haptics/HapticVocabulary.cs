namespace HapticNav.Core.Haptics;

/// <summary>
/// Motors placed around the wrist of the bracelet.
/// The final motor count (2-4) is a design-phase decision; the vocabulary
/// only depends on logical positions so it survives that change.
/// </summary>
public enum MotorPosition
{
    Left = 0,
    Front = 1,
    Right = 2,
    Back = 3
}

public readonly record struct Pulse(MotorPosition Motor, int OnMs, int GapMs);

/// <summary>A distinct vibration pattern for one direction or system signal (FR-01).</summary>
public sealed record HapticPattern(string Code, string Name, string Description, IReadOnlyList<Pulse> Pulses)
{
    public override string ToString() => $"{Name}: {Description}";
}

/// <summary>
/// The shared "vibration language" between app and bracelet.
/// Patterns are deliberately maximally distinct (length, rhythm, motor side)
/// so a new user can learn them in minutes (NFR-01). To be validated with
/// users in the lab tests.
/// </summary>
public static class HapticVocabulary
{
    public static readonly HapticPattern TurnLeft = new(
        "TL", "Turn left",
        "2 long pulses on the LEFT motor",
        new[]
        {
            new Pulse(MotorPosition.Left, 400, 200),
            new Pulse(MotorPosition.Left, 400, 0)
        });

    public static readonly HapticPattern TurnRight = new(
        "TR", "Turn right",
        "2 long pulses on the RIGHT motor",
        new[]
        {
            new Pulse(MotorPosition.Right, 400, 200),
            new Pulse(MotorPosition.Right, 400, 0)
        });

    public static readonly HapticPattern TurnNow = new(
        "TN", "Turn now (urgent)",
        "3 rapid pulses on BOTH left and right motors",
        new[]
        {
            new Pulse(MotorPosition.Left, 100, 80),
            new Pulse(MotorPosition.Right, 100, 80),
            new Pulse(MotorPosition.Left, 100, 80),
            new Pulse(MotorPosition.Right, 100, 80),
            new Pulse(MotorPosition.Left, 100, 80),
            new Pulse(MotorPosition.Right, 100, 0)
        });

    public static readonly HapticPattern Stop = new(
        "ST", "Stop",
        "1 very long continuous pulse on all motors",
        new[]
        {
            new Pulse(MotorPosition.Left, 1200, 0),
            new Pulse(MotorPosition.Front, 1200, 0),
            new Pulse(MotorPosition.Right, 1200, 0)
        });

    public static readonly HapticPattern Arrived = new(
        "AR", "Arrived",
        "rising rhythm: short, short, long (all motors)",
        new[]
        {
            new Pulse(MotorPosition.Front, 120, 120),
            new Pulse(MotorPosition.Front, 120, 120),
            new Pulse(MotorPosition.Front, 500, 0),
            new Pulse(MotorPosition.Left, 120, 120),
            new Pulse(MotorPosition.Right, 120, 120),
            new Pulse(MotorPosition.Left, 500, 0),
            new Pulse(MotorPosition.Right, 500, 0)
        });

    public static readonly HapticPattern ConnectionLost = new(
        "CL", "Connection lost",
        "long-short-long on all motors (FR-04)",
        new[]
        {
            new Pulse(MotorPosition.Front, 500, 150),
            new Pulse(MotorPosition.Front, 120, 150),
            new Pulse(MotorPosition.Front, 500, 0),
            new Pulse(MotorPosition.Left, 500, 150),
            new Pulse(MotorPosition.Right, 120, 150),
            new Pulse(MotorPosition.Left, 500, 0),
            new Pulse(MotorPosition.Right, 500, 0)
        });

    public static readonly HapticPattern OffRoute = new(
        "OR", "Off route",
        "2 medium pulses on the FRONT motor (FR-07)",
        new[]
        {
            new Pulse(MotorPosition.Front, 250, 150),
            new Pulse(MotorPosition.Front, 250, 0)
        });

    public static readonly HapticPattern LowBattery = new(
        "LB", "Low battery",
        "3 slow pulses on the BACK motor",
        new[]
        {
            new Pulse(MotorPosition.Back, 200, 400),
            new Pulse(MotorPosition.Back, 200, 400),
            new Pulse(MotorPosition.Back, 200, 0)
        });

    public static readonly IReadOnlyList<HapticPattern> All = new[]
    {
        TurnLeft, TurnRight, TurnNow, Stop, Arrived, ConnectionLost, OffRoute, LowBattery
    };

    public static HapticPattern? ByCode(string code) =>
        All.FirstOrDefault(p => p.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

    /// <summary>True when every pattern has a unique name and a non-empty pulse list.</summary>
    public static bool IsConsistent() =>
        All.Select(p => p.Name).Distinct().Count() == All.Count &&
        All.All(p => p.Pulses.Count > 0);
}
