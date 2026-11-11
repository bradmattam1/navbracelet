using HapticNav.Core.Ble;
using HapticNav.Core.Haptics;
using HapticNav.Core.Protocol;
using Xunit;

namespace HapticNav.Core.Tests;

public class HapticVocabularyTests
{
    [Fact]
    public void AllCorePatternsArePresent()
    {
        string[] codes = { "TL", "TR", "TN", "ST", "AR", "CL", "OR" };
        Assert.All(codes, code => Assert.NotNull(HapticVocabulary.ByCode(code)));
    }

    [Fact]
    public void VocabularyIsConsistent()
    {
        Assert.True(HapticVocabulary.IsConsistent());
    }

    [Fact]
    public void LeftAndRightUseDifferentMotors()
    {
        Assert.Equal(MotorPosition.Left, HapticVocabulary.TurnLeft.Pulses[0].Motor);
        Assert.Equal(MotorPosition.Right, HapticVocabulary.TurnRight.Pulses[0].Motor);
    }
}

public class BleFrameTests
{
    [Fact]
    public void EncodeDecodeRoundTrip()
    {
        byte[] frame = BleFrame.Encode(DirectionCommand.TurnLeft, 7);
        DirectionCommand? decoded = BleFrame.TryDecode(frame, out byte sequence);
        Assert.Equal(DirectionCommand.TurnLeft, decoded);
        Assert.Equal((byte)7, sequence);
    }

    [Fact]
    public void CorruptedChecksumIsRejected()
    {
        byte[] frame = BleFrame.Encode(DirectionCommand.Stop, 1);
        frame[1] ^= 0xFF;
        Assert.Null(BleFrame.TryDecode(frame, out _));
    }

    [Fact]
    public void WrongHeaderIsRejected()
    {
        byte[] frame = BleFrame.Encode(DirectionCommand.Stop, 1);
        frame[0] = 0x00;
        Assert.Null(BleFrame.TryDecode(frame, out _));
    }

    [Fact]
    public void UnknownCommandCodeIsRejected()
    {
        byte[] frame = BleFrame.Encode(DirectionCommand.Ping, 1);
        frame[1] = 0x7F;
        frame[^1] = BleFrame.Checksum(frame.AsSpan(0, frame.Length - 1), frame.Length - 1);
        Assert.Null(BleFrame.TryDecode(frame, out _));
    }

    [Fact]
    public void FrameContainsNoCoordinates()
    {
        byte[] frame = BleFrame.Encode(DirectionCommand.TurnRight, 42);
        Assert.Equal(5, frame.Length);
    }
}

public class BraceletSecurityTests
{
    private static BraceletDevice NewBracelet() => new("bracelet-hnb-01", "1234");

    [Fact]
    public void WrongPinIsRejected()
    {
        BraceletDevice bracelet = NewBracelet();
        Assert.False(bracelet.Bond("phone-1", "9999"));
        Assert.Null(bracelet.BondedPhoneId);
    }

    [Fact]
    public void UnbondedPhoneCannotSendCommands()
    {
        BraceletDevice bracelet = NewBracelet();
        bracelet.Bond("phone-sanne", "1234");
        bracelet.Disconnect();

        byte[] frame = BleFrame.Encode(DirectionCommand.TurnLeft, 1);
        Assert.False(bracelet.AcceptFrame("attacker-phone", frame, out _));
        Assert.False(bracelet.AcceptFrame("phone-sanne", frame, out _));
    }

    [Fact]
    public void BondedPhoneCanSendCommands()
    {
        BraceletDevice bracelet = NewBracelet();
        bracelet.Bond("phone-sanne", "1234");

        HapticPattern? played = null;
        bracelet.PatternPlayed += (pattern, _) => played = pattern;

        byte[] frame = BleFrame.Encode(DirectionCommand.TurnRight, 1);
        Assert.True(bracelet.AcceptFrame("phone-sanne", frame, out _));
        Assert.Equal(HapticVocabulary.TurnRight, played);
    }

    [Fact]
    public void WatchdogRaisesConnectionLost()
    {
        BraceletDevice bracelet = NewBracelet();
        bracelet.Bond("phone-sanne", "1234");
        bracelet.WatchdogTimeout = TimeSpan.FromSeconds(5);

        bool lost = false;
        bracelet.ConnectionLost += _ => lost = true;

        bracelet.Tick(TimeSpan.FromSeconds(3));
        Assert.False(lost);

        bracelet.Tick(TimeSpan.FromSeconds(6));
        Assert.True(lost);
    }

    [Fact]
    public void LatencyStaysUnderOneSecond()
    {
        var sink = new ConsoleVibrationSink();
        var link = new SimulatedBraceletLink("phone-sanne", sink);
        BraceletDevice bracelet = NewBracelet();
        link.Connect(bracelet, "1234");

        link.Send(DirectionCommand.TurnLeft);
        Assert.True(link.LastCommandLatency < TimeSpan.FromSeconds(1), "NFR-02: command must reach the bracelet within 1 second");
    }
}
