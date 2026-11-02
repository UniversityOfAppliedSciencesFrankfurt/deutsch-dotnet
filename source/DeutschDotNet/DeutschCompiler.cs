namespace DeutschDotNet;

/// <summary>Compiler pipeline for one source file: Lexer → Parser → AST → IL → .dll</summary>
public static class DeutschCompiler
{
    public const string SourceExtension = ".deu";

    /// <returns>Path of the generated assembly.</returns>
    public static string CompileFile(string sourcePath, string outputDirectory)
    {
        var source = File.ReadAllText(sourcePath);
        var tokens = new Lexer(source).Tokenize();
        var program = new Parser(tokens).ParseProgram();

        var assemblyName = Path.GetFileNameWithoutExtension(sourcePath);
        var outputPath = Path.Combine(outputDirectory, assemblyName + ".dll");
        AssemblyEmitter.Save(program, assemblyName, outputPath);
        return outputPath;
    }
}
