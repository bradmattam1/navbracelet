using HapticNav.Core.Ble;
using HapticNav.Core.Haptics;
using HapticNav.Core.Protocol;

namespace HapticNav.Core.Ble;

/// <summary>What the app needs from a real BLE link. A future MAUI app swaps this for a real GATT client.</summary>
public interface IBraceletLink
{
    bool IsConnected { get; }
    string? ConnectedDeviceId { get; }
    TimeSpan LastCommandLatency { get; }

    bool Connect(BraceletDevice bracelet, string pin);
    void Disconnect();
    bool Send(DirectionCommand command);
}

/// <summary>
/// In-process simulation of the BLE client on the phone side.
/// Measures artificial link latency so NFR-02 (command within 1 second)
/// can be demonstrated without hardware.
/// </summary>
public sealed class SimulatedBraceletLink : IBraceletLink
{
    private readonly IVibrationSink _sink;
    private readonly Random _random = new();
    private byte _sequence;
    private BraceletDevice? _bracelet;

    public bool IsConnected => _bracelet is not null && _bracelet.ConnectedPhoneId is not null;
    public string? ConnectedDeviceId => _bracelet?.ConnectedDeviceIdWhenConnected();
    public TimeSpan LastCommandLatency { get; private set; }
    public string PhoneId { get; }

    public SimulatedBraceletLink(string phoneId, IVibrationSink sink)
    {
        PhoneId = phoneId;
        _sink = sink;
    }

    public bool Connect(BraceletDevice bracelet, string pin)
    {
        _bracelet = bracelet;
        return bracelet.Bond(PhoneId, pin);
    }

    public void Disconnect() => _bracelet?.Disconnect();

    public bool Send(DirectionCommand command)
    {
        if (_bracelet is null || !IsConnected)
            return false;

        byte[] frame = BleFrame.Encode(command, NextSequence());

        int latencyMs = _random.Next(40, 120);
        LastCommandLatency = TimeSpan.FromMilliseconds(latencyMs);

        bool accepted = _bracelet.AcceptFrame(PhoneId, frame, out DirectionCommand? decoded);
        if (accepted && decoded is { } cmd && cmd != DirectionCommand.Ping)
            foreach (Pulse pulse in CommandMap.PatternFor(cmd).Pulses)
                _sink.Vibrate(pulse);

        return accepted;
    }

    private byte NextSequence() => _sequence = (byte)((_sequence + 1) & 0xFF);
}

file static class BraceletDeviceExtensions
{
    public static string? ConnectedDeviceIdWhenConnected(this BraceletDevice bracelet) =>
        bracelet.ConnectedPhoneId is not null ? bracelet.DeviceId : null;
}
