using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace DeutschDotNet;

/// <summary>Writes a compiled program as an executable .NET assembly (.dll + .runtimeconfig.json).</summary>
public static class AssemblyEmitter
{
    public const string ProgramTypeName = "Programm";

    public static void Save(IReadOnlyList<Statement> program, string assemblyName, string outputPath)
    {
        var assembly = new PersistedAssemblyBuilder(
            new AssemblyName(assemblyName) { Version = new Version(1, 0, 0, 0) },
            typeof(object).Assembly);
        var module = assembly.DefineDynamicModule(assemblyName + ".dll");

        var type = module.DefineType(ProgramTypeName,
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class);
        var main = type.DefineMethod("Main", MethodAttributes.Public | MethodAttributes.Static,
            typeof(void), Type.EmptyTypes);

        new CodeGenerator(main.GetILGenerator()).Generate(program);   // throws CompileError before anything is written
        type.CreateType();

        var metadata = assembly.GenerateMetadata(out BlobBuilder ilStream, out BlobBuilder fieldData);
        var peBuilder = new ManagedPEBuilder(
            header: new PEHeaderBuilder(imageCharacteristics: Characteristics.ExecutableImage),
            metadataRootBuilder: new MetadataRootBuilder(metadata),
            ilStream: ilStream,
            mappedFieldData: fieldData,
            entryPoint: MetadataTokens.MethodDefinitionHandle(main.MetadataToken));

        var peBlob = new BlobBuilder();
        peBuilder.Serialize(peBlob);
        using (var stream = File.Create(outputPath))
            peBlob.WriteContentTo(stream);

        WriteRuntimeConfig(Path.ChangeExtension(outputPath, ".runtimeconfig.json"));
    }

    /// <summary>Lets "dotnet program.dll" start the assembly on the same runtime the compiler runs on (or newer).</summary>
    static void WriteRuntimeConfig(string path)
    {
        var v = Environment.Version;
        File.WriteAllText(path, $$"""
            {
              "runtimeOptions": {
                "tfm": "net{{v.Major}}.{{v.Minor}}",
                "rollForward": "Major",
                "framework": {
                  "name": "Microsoft.NETCore.App",
                  "version": "{{v.Major}}.{{v.Minor}}.0"
                }
              }
            }
            """);
    }
}
