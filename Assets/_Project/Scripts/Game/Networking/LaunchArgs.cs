namespace BooTrayal.Game.Networking
{
    /// <summary>Command-line switches used by the smoke tests and multi-instance testing on one PC.</summary>
    public sealed class LaunchArgs
    {
        public string Profile { get; private set; } = "main";
        public bool AutoHost { get; private set; }
        public string AutoJoinCode { get; private set; }
        public bool AutoHostLocal { get; private set; }
        public bool AutoJoinLocal { get; private set; }
        public string FakeVersion { get; private set; }
        public string CodeFile { get; private set; }
        public string PlayerName { get; private set; }
        /// <summary>Smoke-test bot role (see Game/Dev/SmokeProbe).</summary>
        public string SmokeProbe { get; private set; }
        /// <summary>Scene the host loads (default: SessionFlow.DefaultPlayScene).</summary>
        public string PlayScene { get; private set; }
        /// <summary>Smoke only: the ghost holds still until it first hears or sees someone.</summary>
        public bool GhostWait { get; private set; }

        // Night/game overrides for smoke tests until the M6 Create Room screen sets them.
        /// <summary>Host starts the game once this many players are connected (0 = wait for the HUD button).</summary>
        public int AutoStart { get; private set; }
        /// <summary>0 = default (3).</summary>
        public int Nights { get; private set; }
        /// <summary>Per-night lengths in seconds; the last value repeats. Null = from the room config.</summary>
        public float[] NightSeconds { get; private set; }
        public bool NoSpy { get; private set; }
        /// <summary>0 = default (1).</summary>
        public int Spies { get; private set; }
        public bool NoGhost { get; private set; }
        /// <summary>Multiplies the 45 s discussion and 15 s vote.</summary>
        public float MeetingScale { get; private set; } = 1f;
        /// <summary>Seeds roles, seals and codes for reproducible tests.</summary>
        public int? Seed { get; private set; }

        public float NightSecondsFor(int nightIndex) =>
            NightSeconds == null || NightSeconds.Length == 0 ? 0f : NightSeconds[System.Math.Min(nightIndex, NightSeconds.Length - 1)];

        public static LaunchArgs Parse(string[] args)
        {
            var a = new LaunchArgs();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-autohost": a.AutoHost = true; break;
                    case "-autohost-local": a.AutoHostLocal = true; break;
                    case "-autojoin-local": a.AutoJoinLocal = true; break;
                    case "-profile": a.Profile = Value(args, ref i) ?? a.Profile; break;
                    case "-autojoin": a.AutoJoinCode = Value(args, ref i); break;
                    case "-fakeversion": a.FakeVersion = Value(args, ref i); break;
                    case "-codefile": a.CodeFile = Value(args, ref i); break;
                    case "-name": a.PlayerName = Value(args, ref i); break;
                    case "-smokeprobe": a.SmokeProbe = Value(args, ref i); break;
                    case "-playscene": a.PlayScene = Value(args, ref i); break;
                    case "-ghostwait": a.GhostWait = true; break;
                    case "-autostart": a.AutoStart = PositiveInt(Value(args, ref i)); break;
                    case "-nights": a.Nights = PositiveInt(Value(args, ref i)); break;
                    case "-nightseconds": a.NightSeconds = FloatList(Value(args, ref i)); break;
                    case "-nospy": a.NoSpy = true; break;
                    case "-spies": a.Spies = PositiveInt(Value(args, ref i)); break;
                    case "-noghost": a.NoGhost = true; break;
                    case "-meetingscale":
                        var scale = FloatList(Value(args, ref i));
                        if (scale != null && scale.Length == 1) a.MeetingScale = scale[0];
                        break;
                    case "-seed":
                        if (int.TryParse(Value(args, ref i), out var seed)) a.Seed = seed;
                        break;
                }
            }
            return a;
        }

        static int PositiveInt(string v) => int.TryParse(v, out var n) && n > 0 ? n : 0;

        /// <summary>Comma-separated positive numbers, or null if any part is not one.</summary>
        static float[] FloatList(string v)
        {
            if (string.IsNullOrEmpty(v)) return null;
            var parts = v.Split(',');
            var result = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!float.TryParse(parts[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result[i])
                    || !(result[i] > 0f))
                    return null;
            }
            return result;
        }

        /// <summary>Next argument as a value, unless it is missing or another switch.</summary>
        static string Value(string[] args, ref int i)
        {
            if (i + 1 >= args.Length || args[i + 1].StartsWith("-")) return null;
            return args[++i];
        }
    }
}
