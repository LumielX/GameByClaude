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
                }
            }
            return a;
        }

        /// <summary>Next argument as a value, unless it is missing or another switch.</summary>
        static string Value(string[] args, ref int i)
        {
            if (i + 1 >= args.Length || args[i + 1].StartsWith("-")) return null;
            return args[++i];
        }
    }
}
