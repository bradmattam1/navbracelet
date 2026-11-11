using HapticNav.Core.Haptics;
using HapticNav.Core.Protocol;

namespace HapticNav.Core.Ble;

/// <summary>Receives vibration pulses; the stand-in for the real motor driver.</summary>
public interface IVibrationSink
{
    void Vibrate(Pulse pulse);
}

/// <summary>
/// Simulation of the bracelet firmware. Mirrors the security measures from the
/// threat model: it only accepts frames from the bonded phone (NFR-05) and it
/// raises a distinct "connection lost" signal when the link watchdog expires (FR-04).
/// </summary>
public sealed class BraceletDevice
{
    private readonly List<string> _eventLog = new();

    public string DeviceId { get; }
    public string PairingPin { get; }
    public string? BondedPhoneId { get; private set; }
    public string? ConnectedPhoneId { get; private set; }
    public int BatteryPercent { get; private set; } = 100;
    public TimeSpan WatchdogTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public IReadOnlyList<string> EventLog => _eventLog;

    public event Action<HapticPattern, string>? PatternPlayed;
    public event Action<string>? ConnectionLost;
    public event Action<string>? LowBattery;

    public BraceletDevice(string deviceId, string pairingPin)
    {
        DeviceId = deviceId;
        PairingPin = pairingPin;
    }

    /// <summary>Bond with a phone using the PIN shown on the bracelet. Only the bonded phone is ever accepted (NFR-05).</summary>
    public bool Bond(string phoneId, string pin)
    {
        if (pin != PairingPin)
        {
            Log($"REJECTED pairing attempt from {phoneId}: wrong PIN");
            return false;
        }
        BondedPhoneId = phoneId;
        ConnectedPhoneId = phoneId;
        Log($"Bonded and connected to {phoneId}");
        return true;
    }

    public void Disconnect()
    {
        if (ConnectedPhoneId is null) return;
        string phone = ConnectedPhoneId;
        ConnectedPhoneId = null;
        Log($"Link to {phone} lost");
        ConnectionLost?.Invoke(phone);
    }

    /// <summary>Accepts a frame only from the bonded, connected phone. Returns true when the pattern was played.</summary>
    public bool AcceptFrame(string phoneId, byte[] frame, out DirectionCommand? command)
    {
        command = null;
        if (ConnectedPhoneId is null || phoneId != BondedPhoneId)
        {
            Log($"REJECTED frame from {phoneId}: not the bonded phone");
            return false;
        }

        command = BleFrame.TryDecode(frame, out _);
        if (command is null)
        {
            Log("REJECTED frame: bad checksum or unknown command");
            return false;
        }

        if (command == DirectionCommand.Ping)
        {
            Log("Ping received");
            return true;
        }

        PlayPattern(CommandMap.PatternFor(command.Value));
        return true;
    }

    /// <summary>Watchdog: when no frame arrives within the timeout the bracelet signals connection loss (FR-04).</summary>
    public void Tick(TimeSpan timeSinceLastFrame)
    {
        if (ConnectedPhoneId is not null && timeSinceLastFrame > WatchdogTimeout)
            Disconnect();

        if (BatteryPercent <= 15)
            LowBattery?.Invoke(DeviceId);
    }

    public void DrainBattery(int percent)
    {
        BatteryPercent = Math.Max(0, BatteryPercent - percent);
        if (BatteryPercent <= 15)
            LowBattery?.Invoke(DeviceId);
    }

    private void PlayPattern(HapticPattern pattern)
    {
        foreach (Pulse pulse in pattern.Pulses)
            Vibrate(pulse);
        Log($"Vibrated: {pattern.Name}");
        PatternPlayed?.Invoke(pattern, DeviceId);
    }

    private void Vibrate(Pulse pulse) { }

    private void Log(string message) => _eventLog.Add(message);
}
