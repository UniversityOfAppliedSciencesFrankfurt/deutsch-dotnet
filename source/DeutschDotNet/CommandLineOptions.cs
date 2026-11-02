namespace DeutschDotNet;

public sealed class CommandLineOptions
{
    public List<string> Inputs { get; } = [];
    public string? OutputDirectory { get; private set; }
    public string Configuration { get; private set; } = "Debug";
    public bool Run { get; private set; }
    public bool Quiet { get; private set; }
    public bool ShowHelp { get; private set; }
    public bool ShowVersion { get; private set; }

    public static CommandLineOptions Parse(string[] args)
    {
        var o = new CommandLineOptions();
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            // also accept csc/MSBuild style: -out:dir, /out:dir, --output=dir
            if (TrySplit(arg, out var name, out var inlineValue))
                arg = name;

            string Value() => inlineValue ?? (++i < args.Length ? args[i] : throw new ArgumentException($"Option '{arg}' requires a value."));

            switch (arg)
            {
                case "-o" or "--output" or "-out" or "/out": o.OutputDirectory = Value(); break;
                case "-c" or "--configuration": o.Configuration = Value(); break;
                case "-r" or "--run": o.Run = true; break;
                case "-q" or "--quiet": o.Quiet = true; break;
                case "-h" or "--help" or "-?" or "/?": o.ShowHelp = true; break;
                case "--version": o.ShowVersion = true; break;
                default:
                    if (arg.StartsWith('-') && arg.Length > 1)
                        throw new ArgumentException($"Unknown option '{arg}'.");
                    o.Inputs.Add(arg);
                    break;
            }
        }
        return o;
    }

    static bool TrySplit(string arg, out string name, out string? value)
    {
        name = arg; value = null;
        if (!arg.StartsWith('-') && !arg.StartsWith('/')) return false;
        int sep = arg.IndexOfAny([':', '=']);
        if (sep <= 1) return false;
        name = arg[..sep];
        value = arg[(sep + 1)..];
        return true;
    }
}
