using System.Xml.Linq;

namespace DeutschDotNet;

public sealed record BuildInput(IReadOnlyList<string> Sources, string OutputDirectory, string Description);

/// <summary>
/// Resolves the command line inputs like the standard .NET tools do:
/// source files (csc style), a project file or a directory (dotnet build style), or nothing (current directory).
/// </summary>
public static class InputResolver
{
    static readonly string[] ProjectExtensions = [".csproj", ".proj", ".deuproj"];
    const string DefaultTargetFramework = "net10.0";

    public static BuildInput Resolve(CommandLineOptions options)
    {
        var cwd = Directory.GetCurrentDirectory();

        if (options.Inputs.Count == 0)
        {
            var projects = ProjectExtensions.SelectMany(ext => Directory.EnumerateFiles(cwd, "*" + ext)).ToList();
            if (projects.Count > 1)
                throw new ArgumentException($"Found more than one project file in '{cwd}'. Specify which one to use.");
            return projects.Count == 1
                ? FromProject(projects[0], options)
                : FromDirectory(cwd, options);
        }

        if (options.Inputs.Count == 1)
        {
            var single = Path.GetFullPath(options.Inputs[0]);
            if (IsProjectFile(single)) return FromProject(single, options);
            if (Directory.Exists(single)) return FromDirectory(single, options);
        }

        // csc style: one or more source files, wildcards allowed (e.g. *.deu on Windows shells)
        var sources = new List<string>();
        foreach (var input in options.Inputs)
        {
            if (IsProjectFile(Path.GetFullPath(input)) || Directory.Exists(input))
                throw new ArgumentException($"'{input}': a project or directory must be the only input.");
            sources.AddRange(Expand(cwd, input, mustExist: true));
        }
        return new BuildInput(Distinct(sources), Path.GetFullPath(options.OutputDirectory ?? cwd), "source files");
    }

    static bool IsProjectFile(string path) =>
        File.Exists(path) && ProjectExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    static BuildInput FromDirectory(string directory, CommandLineOptions options)
    {
        var sources = Expand(directory, "**/*" + DeutschCompiler.SourceExtension, mustExist: false);
        var output = options.OutputDirectory ?? Path.Combine(directory, "bin");
        return new BuildInput(Distinct(sources), Path.GetFullPath(output), $"directory {directory}");
    }

    /// <summary>
    /// Reads &lt;DeuCompile Include="..." Exclude="..."/&gt; items and the TargetFramework property.
    /// Without DeuCompile items all **/*.deu files of the project directory are compiled (like SDK default items).
    /// </summary>
    static BuildInput FromProject(string projectPath, CommandLineOptions options)
    {
        var directory = Path.GetDirectoryName(projectPath)!;
        var xml = XDocument.Load(projectPath);
        var items = xml.Descendants().Where(e => e.Name.LocalName == "DeuCompile").ToList();

        var sources = new List<string>();
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (items.Count == 0)
        {
            sources.AddRange(Expand(directory, "**/*" + DeutschCompiler.SourceExtension, mustExist: false));
        }
        else
        {
            foreach (var item in items)
            {
                foreach (var pattern in SplitList(item.Attribute("Include")?.Value))
                    sources.AddRange(Expand(directory, pattern, mustExist: true));
                foreach (var pattern in SplitList(item.Attribute("Exclude")?.Value))
                    excluded.UnionWith(Expand(directory, pattern, mustExist: false));
            }
        }

        var tfm = xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "TargetFramework")?.Value.Trim();
        var output = options.OutputDirectory
                     ?? Path.Combine(directory, "bin", options.Configuration, string.IsNullOrEmpty(tfm) ? DefaultTargetFramework : tfm);

        return new BuildInput(Distinct(sources.Where(s => !excluded.Contains(s))), Path.GetFullPath(output), $"project {Path.GetFileName(projectPath)}");
    }

    static IEnumerable<string> SplitList(string? value) =>
        (value ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Minimal MSBuild-like globbing: "file.deu", "*.deu", "dir/*.deu", "**/*.deu", "dir/**/*.deu".</summary>
    static IEnumerable<string> Expand(string baseDirectory, string pattern, bool mustExist)
    {
        pattern = pattern.Replace('\\', '/');

        if (!pattern.Contains('*') && !pattern.Contains('?'))
        {
            var file = Path.GetFullPath(Path.Combine(baseDirectory, pattern));
            if (File.Exists(file)) return [file];
            if (mustExist) throw new FileNotFoundException($"Source file not found: {file}");
            return [];
        }

        string root, filePattern;
        SearchOption search;
        int recursive = pattern.IndexOf("**/", StringComparison.Ordinal);
        if (recursive >= 0)
        {
            root = Path.Combine(baseDirectory, pattern[..recursive]);
            filePattern = pattern[(recursive + 3)..];
            search = SearchOption.AllDirectories;
        }
        else
        {
            root = Path.Combine(baseDirectory, Path.GetDirectoryName(pattern) ?? "");
            filePattern = Path.GetFileName(pattern);
            search = SearchOption.TopDirectoryOnly;
        }

        if (!Directory.Exists(root)) return [];
        return Directory.EnumerateFiles(root, filePattern, search)
            .Select(Path.GetFullPath)
            .Where(f => !IsInBuildFolder(f, root));
    }

    static bool IsInBuildFolder(string file, string root)
    {
        var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
        return relative.StartsWith("bin/", StringComparison.OrdinalIgnoreCase)
            || relative.StartsWith("obj/", StringComparison.OrdinalIgnoreCase)
            || relative.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || relative.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    static List<string> Distinct(IEnumerable<string> files) =>
        files.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
}
