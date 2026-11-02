using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;

namespace DeutschDotNet;

internal static class Program
{
    const string CommandName = "dotnet-de";

    static readonly string Version =
        typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "1.0.0";

    static int Main(string[] args)
    {
        CommandLineOptions options;
        try
        {
            options = CommandLineOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"{CommandName} : error {ErrorCodes.CommandLine}: {ex.Message}");
            Console.Error.WriteLine("Use --help to see the available options.");
            return 2;
        }

        if (options.ShowHelp) { PrintHelp(); return 0; }
        if (options.ShowVersion) { Console.WriteLine(Version); return 0; }

        BuildInput input;
        try
        {
            input = InputResolver.Resolve(options);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            Console.Error.WriteLine($"{CommandName} : error {ErrorCodes.Input}: {ex.Message}");
            return 2;
        }

        if (input.Sources.Count == 0)
        {
            Console.Error.WriteLine($"{CommandName} : error {ErrorCodes.Input}: No {DeutschCompiler.SourceExtension} source files found ({input.Description}).");
            return 2;
        }

        var duplicate = input.Sources.GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
        {
            Console.Error.WriteLine($"{CommandName} : error {ErrorCodes.Input}: Several source files would produce '{duplicate.Key}.dll': {string.Join(", ", duplicate)}");
            return 2;
        }

        if (!options.Quiet)
        {
            Console.WriteLine($"{CommandName} {Version} – Deutsch# → .NET IL");
            Console.WriteLine($"Compiling {input.Sources.Count} file(s) from {input.Description}");
        }

        Directory.CreateDirectory(input.OutputDirectory);
        var stopwatch = Stopwatch.StartNew();
        var assemblies = new List<string>();
        int errors = 0;

        foreach (var source in input.Sources)
        {
            try
            {
                var assembly = DeutschCompiler.CompileFile(source, input.OutputDirectory);
                assemblies.Add(assembly);
                if (!options.Quiet) Console.WriteLine($"  {Path.GetFileName(source)} -> {assembly}");
            }
            catch (CompileError error)
            {
                errors++;
                Console.WriteLine(error.Format(source));
            }
            catch (IOException ex)
            {
                errors++;
                Console.WriteLine($"{source} : error {ErrorCodes.Io}: {ex.Message}");
            }
        }

        if (!options.Quiet || errors > 0)
        {
            Console.WriteLine();
            Console.WriteLine(errors == 0
                ? $"Build succeeded. {Assemblies(assemblies.Count)} in {stopwatch.ElapsedMilliseconds} ms."
                : $"Build FAILED. {errors} error(s), {Assemblies(assemblies.Count)} written.");
        }

        if (options.Run && errors == 0)
            foreach (var assembly in assemblies) RunProgram(assembly);

        return errors == 0 ? 0 : 1;
    }

    static string Assemblies(int count) => count == 1 ? "1 assembly" : $"{count} assemblies";

    /// <summary>Loads a compiled program into an isolated, collectible load context and runs its entry point.</summary>
    static void RunProgram(string assemblyPath)
    {
        Console.WriteLine();
        Console.WriteLine($"── {Path.GetFileName(assemblyPath)} ──────────────────────────────");
        var context = new AssemblyLoadContext(Path.GetFileNameWithoutExtension(assemblyPath), isCollectible: true);
        try
        {
            using var stream = File.OpenRead(assemblyPath);              // load from a stream so the file is not locked
            var assembly = context.LoadFromStream(stream);
            var entryPoint = assembly.EntryPoint ?? throw new InvalidOperationException("No entry point.");
            entryPoint.Invoke(null, entryPoint.GetParameters().Length == 0 ? null : [Array.Empty<string>()]);
        }
        catch (TargetInvocationException ex)
        {
            Console.WriteLine($"Laufzeitfehler: {ex.InnerException?.GetType().Name}: {ex.InnerException?.Message}");
        }
        finally
        {
            context.Unload();
        }
    }

    static void PrintHelp()
    {
        Console.WriteLine($"""
            {CommandName} {Version} – compiles Deutsch# (.deu) programs to .NET assemblies

            Usage:
              {CommandName} [options] <file.deu> [<file.deu> ...]   Compile source files (csc style)
              {CommandName} [options] <project-file>               Compile the .deu files of a project (dotnet build style)
              {CommandName} [options] <directory>                  Compile all .deu files in a directory tree
              {CommandName} [options]                              Use the project file or .deu files in the current directory

            Every source file becomes its own assembly: hallo.deu -> hallo.dll + hallo.runtimeconfig.json.
            Run a result with: dotnet hallo.dll

            Options:
              -o, --output <dir>          Output directory
                                          (default: current directory for files, bin/<config>/<tfm> for projects)
              -c, --configuration <name>  Configuration used for the project output path (default: Debug)
              -r, --run                   Run the compiled programs after a successful build
              -q, --quiet                 Print errors only
                  --version               Print the compiler version
              -h, --help                  Show this help

            Examples:
              {CommandName} hallo.deu
              {CommandName} fibonacci.deu fizzbuzz.deu -o out --run
              {CommandName} Examples/Examples.csproj -c Release
            """);
    }
}
