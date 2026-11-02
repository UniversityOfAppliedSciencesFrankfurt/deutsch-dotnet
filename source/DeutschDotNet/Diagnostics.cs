namespace DeutschDotNet;

/// <summary>Diagnostic codes, shown in the MSBuild/Visual Studio error list.</summary>
public static class ErrorCodes
{
    public const string Lexical = "GDC0001";
    public const string Syntax = "GDC0002";
    public const string Semantic = "GDC0003";
    public const string Io = "GDC0004";
    public const string CommandLine = "GDC0100";
    public const string Input = "GDC0101";
}

/// <summary>A compile error at a position in a .deu source file. Messages are in German on purpose.</summary>
public sealed class CompileError(string code, string message, int line, int column) : Exception(message)
{
    public string Code { get; } = code;
    public int Line { get; } = line;
    public int Column { get; } = column;

    /// <summary>Canonical MSBuild error format, so errors show up in the Visual Studio error list.</summary>
    public string Format(string file) => $"{file}({Line},{Column}): error {Code}: {Message}";
}
