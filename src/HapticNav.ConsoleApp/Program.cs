using HapticNav.Core.Ble;
using HapticNav.Core.Geo;
using HapticNav.Core.Haptics;
using HapticNav.Core.Navigation;
using HapticNav.Core.Protocol;

namespace HapticNav.ConsoleApp;

/// <summary>
/// Interactive demo of the Haptic Navigation Bracelet companion app.
/// Plain labelled text throughout so the app stays usable with a screen
/// reader (NFR-04).
/// </summary>
public static class Program
{
    private const string PhoneId = "phone-sanne-01";

    private static BraceletDevice? _bracelet;
    private static SimulatedBraceletLink? _link;

    public static void Main(string[] args)
    {
        Console.WriteLine("HapticNav - companion app for the Haptic Navigation Bracelet");
        Console.WriteLine("Group project Challenge 3 - directions by vibration, ears and hands free.");
        Console.WriteLine();
        _fixedPin = args.FirstOrDefault(a => a.StartsWith("--pin=")) is { } arg && arg.Length > 6 ? arg[6..] : null;

        while (true)
        {
            ShowMenu();
            switch (Prompt("Choose an option"))
            {
                case "1": PairBracelet(); break;
                case "2": RunDemoRoute(autoWalk: true); break;
                case "3": RunDemoRoute(autoWalk: false); break;
                case "4": TestPatterns(); break;
                case "5": SimulateConnectionLoss(); break;
                case "6": RunTrainingQuiz(); break;
                case "7": ShowPrivacyAndSecurity(); break;
                case "0": return;
                default: Console.WriteLine("Unknown option."); break;
            }
            Console.WriteLine();
        }
    }

    private static void ShowMenu()
    {
        Console.WriteLine("--- Menu ---");
        Console.WriteLine("1. Pair with bracelet (BLE bonding demo)");
        Console.WriteLine("2. Walk the demo route (automatic walk)");
        Console.WriteLine("3. Walk the demo route (you choose each step)");
        Console.WriteLine("4. Test vibration patterns");
        Console.WriteLine("5. Simulate connection loss (FR-04)");
        Console.WriteLine("6. Pattern training quiz (NFR-01)");
        Console.WriteLine("7. Privacy and security notes");
        Console.WriteLine("0. Quit");
        Console.WriteLine($"Bracelet: {(IsPaired() ? "paired" : "not paired")}");
    }

    private static string? _fixedPin;
    private static readonly List<string> _braceletMessages = new();

    private static void FlushBraceletMessages()
    {
        foreach (string message in _braceletMessages)
            Console.WriteLine(message);
        _braceletMessages.Clear();
    }

    private static bool IsPaired() => _link is { IsConnected: true };

    private static void PairBracelet()
    {
        string pin = GeneratePin();
        if (!string.IsNullOrEmpty(_fixedPin)) pin = _fixedPin;
        _bracelet = new BraceletDevice("bracelet-hnb-01", pin);
        _bracelet.ConnectionLost += _ => _braceletMessages.Add(">> [Bracelet] CONNECTION LOST signal played (long-short-long)");
        _bracelet.LowBattery += _ => _braceletMessages.Add(">> [Bracelet] Low battery signal played");
        var sink = new ConsoleVibrationSink();
        _bracelet.PatternPlayed += (pattern, _) =>
        {
            sink.Clear();
            _braceletMessages.Add($">> [Bracelet] Vibrates: {pattern.Name} ({pattern.Description})");
        };

        _link = new SimulatedBraceletLink(PhoneId, sink);

        Console.WriteLine($"The bracelet shows pairing PIN: {pin}");
        string entered = Prompt("Enter the PIN shown on the bracelet");
        bool ok = _link.Connect(_bracelet, entered);
        Console.WriteLine(ok
            ? $"Paired and bonded. Only {PhoneId} may send commands to this bracelet from now on (NFR-05)."
            : "Pairing failed: wrong PIN. The bracelet rejected the bond.");
    }

    private static void RunDemoRoute(bool autoWalk)
    {
        if (!RequirePairing()) return;

        WalkingRoute route = WalkingRoute.DemoCampusRoute();
        var engine = new NavigationEngine(route);
        var session = new NavigationSession(engine, _link!);

        Console.WriteLine($"Route: {route.Name} ({route.TotalLengthMeters():F0} m, {route.Steps.Count} waypoints)");

        var random = new Random();
        GeoPoint position = route.Steps[0].Point;
        double heading = position.BearingDegreesTo(route.Steps[1].Point);
        int stepNumber = 0;
        int turnDirection = 0;
        int turnsRemaining = 0;

        while (true)
        {
            stepNumber++;
            double bearingToTarget = position.BearingDegreesTo(route.Steps[Math.Min(engine.StepIndex + 1, route.Steps.Count - 1)].Point);

            if (!autoWalk)
            {
                heading = ReadHeading(heading);
                if (Math.Abs(heading - bearingToTarget) > 170)
                    position = position.Move(15, heading);
                else
                    position = position.Move(15, bearingToTarget);

                if (stepNumber == 4)
                {
                    position = position.Move(40, heading + 90);
                    Console.WriteLine("(you wandered off the path)");
                }
            }

            NavigationUpdate update = session.ProcessFix(position, heading);
            Report(update, stepNumber);
            FlushBraceletMessages();

            if (autoWalk)
            {
                switch (update.Command)
                {
                    case DirectionCommand.TurnLeft:
                        turnDirection = -1;
                        turnsRemaining = 3;
                        break;
                    case DirectionCommand.TurnRight:
                        turnDirection = 1;
                        turnsRemaining = 3;
                        break;
                    case DirectionCommand.TurnNow:
                        turnDirection = turnDirection == 0 ? 1 : turnDirection;
                        turnsRemaining = 6;
                        break;
                    case DirectionCommand.OffRoute:
                        heading = bearingToTarget;
                        turnDirection = 0;
                        turnsRemaining = 0;
                        break;
                }

                if (turnsRemaining > 0)
                {
                    heading = (heading + turnDirection * 35 + random.Next(-2, 3) + 360.0) % 360.0;
                    turnsRemaining--;
                }
                else
                {
                    heading = (heading + random.Next(-3, 4) + 360.0) % 360.0;
                }

                position = position.Move(15, heading);
            }

            if (update.Status == NavigationStatus.Arrived)
            {
                Console.WriteLine("Route finished. The bracelet played the 'Arrived' pattern.");
                break;
            }
            if (update.OffRouteDistanceMeters > 60)
            {
                Console.WriteLine("Too far off route; guiding back to the start of the demo.");
                position = route.Steps[engine.StepIndex].Point;
            }

            if (autoWalk) Thread.Sleep(150);
        }
    }

    private static double ReadHeading(double current)
    {
        string input = Prompt("Heading for your next step (N/E/S/W, or Enter to keep walking forward)");
        return input.Trim().ToUpperInvariant() switch
        {
            "N" => 0, "E" => 90, "S" => 180, "W" => 270,
            "" => current,
            _ => current
        };
    }

    private static void Report(NavigationUpdate update, int stepNumber)
    {
        string status = update.Status switch
        {
            NavigationStatus.EnRoute => "on route",
            NavigationStatus.OffRoute => "OFF ROUTE",
            _ => "arrived"
        };
        Console.WriteLine($"[Fix {stepNumber}] {status}; next: {update.NextInstruction}; {update.DistanceToNextMeters:F0} m to go");
        if (update.Command is { } command)
            Console.WriteLine($"   -> sent {command} over BLE (latency {_link!.LastCommandLatency.TotalMilliseconds:F0} ms)");
    }

    private static void TestPatterns()
    {
        Console.WriteLine("The vibration vocabulary (FR-01). Each pattern is maximally distinct:");
        foreach (HapticPattern pattern in HapticVocabulary.All)
            Console.WriteLine($"  [{pattern.Code}] {pattern.Name,-18} {pattern.Description}");
    }

    private static void SimulateConnectionLoss()
    {
        if (!RequirePairing()) return;

        Console.WriteLine("Dropping the BLE link to demo FR-04...");
        _link!.Disconnect();
        Console.WriteLine("The bracelet watchdog noticed the silence and played the");
        Console.WriteLine("'Connection lost' pattern: long-short-long on all motors.");
        Console.WriteLine("The user is never left without a signal when the link dies.");
        FlushBraceletMessages();
    }

    private static void RunTrainingQuiz()
    {
        Console.WriteLine("Pattern training quiz (NFR-01: recognise all core patterns after 5 minutes).");
        Console.WriteLine("For each round, the bracelet plays a pattern; type the command it means.");
        Console.WriteLine("Answers: left, right, turnnow, stop, arrived, lost, offroute, battery. Q quits.");
        Console.WriteLine();

        var random = new Random();
        int correct = 0;
        int asked = 0;

        while (asked < 8)
        {
            HapticPattern pattern = HapticVocabulary.All[random.Next(HapticVocabulary.All.Count)];
            asked++;
            Console.WriteLine($"Round {asked}: the bracelet vibrates -> {pattern.Description}");
            string answer = Prompt("What does this mean?").Trim().ToLowerInvariant().Replace(" ", "");
            if (answer is "q" or "quit") break;

            bool right = answer switch
            {
                "left" or "tl" => pattern == HapticVocabulary.TurnLeft,
                "right" or "tr" => pattern == HapticVocabulary.TurnRight,
                "turnnow" or "urgent" or "tn" => pattern == HapticVocabulary.TurnNow,
                "stop" or "st" => pattern == HapticVocabulary.Stop,
                "arrived" or "ar" => pattern == HapticVocabulary.Arrived,
                "lost" or "connectionlost" or "cl" => pattern == HapticVocabulary.ConnectionLost,
                "offroute" or "or" => pattern == HapticVocabulary.OffRoute,
                "battery" or "lowbattery" or "lb" => pattern == HapticVocabulary.LowBattery,
                _ => false
            };

            if (right) { correct++; Console.WriteLine("Correct."); }
            else Console.WriteLine($"Not quite. This pattern means: {pattern.Name}.");
            Console.WriteLine();
        }

        if (asked > 0)
            Console.WriteLine($"Score: {correct}/{asked}. Target for NFR-01: 90% after 5 minutes of training.");
    }

    private static void ShowPrivacyAndSecurity()
    {
        Console.WriteLine("Privacy by design (NFR-06):");
        Console.WriteLine("  - Only direction codes cross the BLE link, never coordinates.");
        Console.WriteLine("  - No location history is stored on the phone or sent to a server.");
        Console.WriteLine("  - Routes are calculated on the phone; minimal permissions only.");
        Console.WriteLine();
        Console.WriteLine("Security (NFR-05):");
        Console.WriteLine("  - Pairing with bonding: only the bonded phone is accepted.");
        Console.WriteLine("  - Frames carry a CRC-8 checksum; malformed frames are rejected.");
        Console.WriteLine("  - A watchdog signals connection loss so the user is never left");
        Console.WriteLine("    without guidance (FR-04).");
    }

    private static bool RequirePairing()
    {
        if (IsPaired()) return true;
        Console.WriteLine("Pair with the bracelet first (menu option 1).");
        return false;
    }

    private static string GeneratePin() => Random.Shared.Next(1000, 10000).ToString("D4");

    private static string Prompt(string message)
    {
        Console.Write($"{message}: ");
        return Console.ReadLine() ?? "";
    }
}
