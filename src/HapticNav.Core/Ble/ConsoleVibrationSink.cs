using HapticNav.Core.Haptics;

namespace HapticNav.Core.Ble;

/// <summary>
/// Collects vibration pulses so the console can describe what the user would
/// feel on their wrist. A real bracelet plays the pulses on its motors instead.
/// </summary>
public sealed class ConsoleVibrationSink : IVibrationSink
{
    private readonly List<Pulse> _pulseBuffer = new();

    public IReadOnlyList<Pulse> PulseBuffer => _pulseBuffer;

    public void Vibrate(Pulse pulse) => _pulseBuffer.Add(pulse);

    public void Clear() => _pulseBuffer.Clear();

    public static string Describe(IReadOnlyList<Pulse> pulses)
    {
        var byMotor = pulses
            .GroupBy(p => p.Motor)
            .Select(g => $"{g.Key}: {g.Count()}x{g.First().OnMs}ms");
        return string.Join(", ", byMotor);
    }
}
