using HapticNav.Core.Haptics;

namespace HapticNav.Core.Protocol;

/// <summary>Direction and system commands the app can send to the bracelet.</summary>
public enum DirectionCommand : byte
{
    TurnLeft = 0x01,
    TurnRight = 0x02,
    TurnNow = 0x03,
    Stop = 0x04,
    Arrived = 0x05,
    ConnectionLost = 0x06,
    OffRoute = 0x07,
    LowBattery = 0x08,
    Ping = 0x10
}

public static class CommandMap
{
    public static HapticPattern PatternFor(DirectionCommand command) => command switch
    {
        DirectionCommand.TurnLeft => HapticVocabulary.TurnLeft,
        DirectionCommand.TurnRight => HapticVocabulary.TurnRight,
        DirectionCommand.TurnNow => HapticVocabulary.TurnNow,
        DirectionCommand.Stop => HapticVocabulary.Stop,
        DirectionCommand.Arrived => HapticVocabulary.Arrived,
        DirectionCommand.ConnectionLost => HapticVocabulary.ConnectionLost,
        DirectionCommand.OffRoute => HapticVocabulary.OffRoute,
        DirectionCommand.LowBattery => HapticVocabulary.LowBattery,
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, "No pattern for command.")
    };
}

/// <summary>
/// Binary frame format for the BLE link. Deliberately minimal:
/// only direction codes cross the link, never coordinates, so eavesdropping
/// on the link reveals nothing about the route (privacy by design, NFR-06).
///
/// Byte layout:
///   [0] 0xA5  header
///   [1]       command code
///   [2]       sequence number (wraps at 255)
///   [3]       payload length (0 for direction commands)
///   [4..]     payload
///   [last]    CRC-8 checksum over bytes [0..len-2]
/// </summary>
public static class BleFrame
{
    public const byte Header = 0xA5;
    public const int MinFrameLength = 5;

    public static byte[] Encode(DirectionCommand command, byte sequence)
    {
        byte[] frame = { Header, (byte)command, sequence, 0, 0 };
        frame[^1] = Checksum(frame, frame.Length - 1);
        return frame;
    }

    /// <summary>Decodes a frame; returns null when the frame is malformed or the checksum fails.</summary>
    public static DirectionCommand? TryDecode(ReadOnlySpan<byte> frame, out byte sequence)
    {
        sequence = 0;
        if (frame.Length < MinFrameLength || frame[0] != Header || frame[3] != 0)
            return null;

        int payloadLength = frame.Length - MinFrameLength;
        if (frame[3] != payloadLength)
            return null;

        if (frame[^1] != Checksum(frame, frame.Length - 1))
            return null;

        sequence = frame[2];
        byte code = frame[1];
        return Enum.IsDefined(typeof(DirectionCommand), code) ? (DirectionCommand)code : null;
    }

    public static byte Checksum(ReadOnlySpan<byte> data, int length)
    {
        byte crc = 0;
        for (int i = 0; i < length; i++)
        {
            crc ^= data[i];
            for (int bit = 0; bit < 8; bit++)
                crc = (byte)((crc & 0x80) != 0 ? (crc << 1) ^ 0x07 : crc << 1);
        }
        return crc;
    }
}
