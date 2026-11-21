using Cartographer;
using Cartographer.Cli;

return Cli.Discover().Run(args);

namespace Cartographer.Cli
{
    /// <summary>A group of commands. Implementations need a public parameterless constructor; they are found by reflection.</summary>
    public interface ICommands { void Register(Cli cli); }

    /// <summary>Parsed arguments: positionals, then --name value options and --flag switches.</summary>
    public sealed class Args
    {
        readonly Dictionary<string, string?> _options = new(StringComparer.Ordinal);
        public List<string> Positional { get; } = [];

        public Args(IEnumerable<string> args)
        {
            var list = args.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                if (!list[i].StartsWith("--")) { Positional.Add(list[i]); continue; }
                var hasValue = i + 1 < list.Count && !list[i + 1].StartsWith("--");
                _options[list[i][2..]] = hasValue ? list[++i] : null;
            }
        }

        public bool Flag(string name) => _options.ContainsKey(name);
        public string? Option(string name) => _options.GetValueOrDefault(name);
        public string Required(string name) => Option(name) ?? throw new UsageException($"--{name} <value> is required");
        public int Int(string name, int fallback) => Option(name) is { } v ? int.TryParse(v, out var n) ? n : throw new UsageException($"--{name} must be a number") : fallback;
        public string At(int index, string what) => index < Positional.Count ? Positional[index] : throw new UsageException($"missing argument: {what}");

        public string AtlasRoot => Path.GetFullPath(Option("atlas") ?? ".");

        /// <summary>The atlas at --atlas or the current directory.</summary>
        public Atlas Atlas()
        {
            if (!File.Exists(Path.Combine(AtlasRoot, Cartographer.Atlas.ConfigFile)))
                throw new UsageException($"{AtlasRoot} is not an atlas (no {Cartographer.Atlas.ConfigFile}). Pass --atlas <dir>, or create one with: cartographer init <dir> --organisation <name>");
            return Cartographer.Atlas.Load(AtlasRoot);
        }
    }

    public sealed class Cli
    {
        readonly SortedDictionary<string, (string Usage, string Help, Func<Args, int> Run)> _commands = new(StringComparer.Ordinal);

        /// <param name="name">One or two words, e.g. "check" or "repos list".</param>
        public void Add(string name, string usage, string help, Func<Args, int> run) => _commands[name] = (usage, help, run);

        public static Cli Discover()
        {
            var cli = new Cli();
            foreach (var type in typeof(Cli).Assembly.GetTypes().Where(t => typeof(ICommands).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false }).OrderBy(t => t.Name, StringComparer.Ordinal))
                ((ICommands)Activator.CreateInstance(type)!).Register(cli);
            return cli;
        }

        public int Run(string[] args)
        {
            try
            {
                if (args.Length == 0 || args[0] is "help" or "--help" or "-h") return Help();
                var two = args.Length >= 2 ? $"{args[0]} {args[1]}" : null;
                if (two is not null && _commands.TryGetValue(two, out var c)) return c.Run(new Args(args.Skip(2)));
                if (_commands.TryGetValue(args[0], out c)) return c.Run(new Args(args.Skip(1)));
                Console.Error.WriteLine($"unknown command '{args[0]}'. Run: cartographer help");
                return 2;
            }
            catch (UsageException e)
            {
                Console.Error.WriteLine(e.Message);
                return 2;
            }
        }

        int Help()
        {
            Console.WriteLine("cartographer: maps an organisation's repositories into an atlas. Never prompts.\nEvery command takes --atlas <dir> (default: the current directory).\n");
            var width = _commands.Values.Max(c => c.Usage.Length);
            foreach (var c in _commands.Values) Console.WriteLine($"  {c.Usage.PadRight(width)}  {c.Help}");
            return 0;
        }
    }
}
