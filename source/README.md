# DeutschDotNet – Deutsch# → .NET IL

**Deutsch#** is a small programming language with German keywords. **DeutschDotNet** compiles `.deu` source files directly into .NET Intermediate Language (IL) and writes real, executable .NET assemblies – no C# in between, no interpreter.

```
zahl fakultät = 1;
zahl i = 1;

solange i <= 10 {
    fakultät = fakultät * i;
    i = i + 1;
}

wenn fakultät > 1000000 {
    schreibe "Das ist groß!";
}
```

```
> dotnet-de beispiel.deu
> dotnet beispiel.dll
Das ist groß!
```

The project was built by Dr. Damir Dobric together with the students of the Software Engineering course at Frankfurt University of Applied Sciences. Background article: [What if "wenn" worked just as well as "if"?](https://www.linkedin.com/pulse/what-wenn-worked-just-well-my-students-i-built-compiler-dobric-05sde/)

---

## Contents

- [Quick start](#quick-start)
- [Solution structure](#solution-structure)
- [Building the solution](#building-the-solution)
- [Using the compiler](#using-the-compiler)
- [Language reference](#language-reference)
- [Examples](#examples)
- [Diagnostics](#diagnostics)
- [How it works](#how-it-works)
- [Limitations and roadmap](#limitations-and-roadmap)

---

## Quick start

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download) or newer.

```bash
dotnet build DeutschDotNet.slnx
dotnet Examples/bin/Debug/net10.0/fizzbuzz.dll
```

Building the solution builds the compiler first and then compiles every `.deu` file in the **Examples** project into its own assembly.

In Visual Studio 2022 (17.13+) or Rider, open `DeutschDotNet.slnx` and press **F5**. The compiler starts with the Examples project as input, compiles all programs and runs them.

---

## Solution structure

```
DeutschDotNet.slnx                     Solution (new XML solution format)
│
├── DeutschDotNet/                     The compiler (console app, also packable as dotnet tool)
│   ├── Program.cs                     Command line entry point, build summary, --run
│   ├── CommandLineOptions.cs          Argument parsing (dotnet- and csc-style options)
│   ├── InputResolver.cs               Source files / project file / directory → list of .deu files
│   ├── DeutschCompiler.cs             Pipeline for one file: Lexer → Parser → AST → IL → .dll
│   ├── Lexer.cs                       Source text → tokens
│   ├── Parser.cs                      Tokens → abstract syntax tree (recursive descent)
│   ├── Ast.cs                         Syntax tree node types
│   ├── CodeGenerator.cs               Syntax tree → IL (System.Reflection.Emit)
│   ├── AssemblyEmitter.cs             Writes .dll + .runtimeconfig.json (PersistedAssemblyBuilder)
│   ├── Diagnostics.cs                 Error type and error codes
│   └── Properties/launchSettings.json F5 / dotnet run profiles
│
└── Examples/                          Deutsch# programs (no C#)
    ├── Examples.csproj                Runs the compiler on all .deu files during build
    ├── hallo.deu
    ├── beispiel.deu
    ├── fibonacci.deu
    ├── fizzbuzz.deu
    ├── ggt.deu
    ├── collatz.deu
    ├── einmaleins.deu
    ├── zinseszins.deu
    └── logik.deu
```

---

## Building the solution

```bash
dotnet build DeutschDotNet.slnx               # Debug
dotnet build DeutschDotNet.slnx -c Release    # Release
dotnet clean DeutschDotNet.slnx               # also removes the generated assemblies
```

Output of the Examples project:

```
Examples/bin/<Configuration>/net10.0/
    hallo.dll       hallo.runtimeconfig.json
    fibonacci.dll   fibonacci.runtimeconfig.json
    ...
```

Run any program with `dotnet <name>.dll`.

### How the Examples project works

`Examples.csproj` uses the [Microsoft.Build.NoTargets](https://github.com/microsoft/MSBuildSdks/tree/main/src/NoTargets) SDK, the standard SDK for projects that don't compile C#. It is restored from NuGet automatically on the first build. The project:

1. declares the sources as `<DeuCompile Include="**/*.deu" />` items,
2. references the compiler project so that it is always built first,
3. calls the compiler after `Build`, passing all `.deu` files and the output directory,
4. builds incrementally: only `.deu` files that are newer than their `.dll` are recompiled,
5. deletes the generated assemblies on `Clean`.

Compile errors appear in the Visual Studio error list and in `dotnet build` output with file, line and column, and they fail the build.

To add a new program, drop a `.deu` file into the `Examples` folder and build. It is picked up automatically.

### Visual Studio launch profiles

The compiler project ships three launch profiles (`Properties/launchSettings.json`):

| Profile | Command line |
|---|---|
| Compile and run Examples project *(default)* | `dotnet-de ../Examples/Examples.csproj --run` |
| Compile single file (beispiel.deu) | `dotnet-de ../Examples/beispiel.deu --output ../Examples/bin/single --run` |
| Show help | `dotnet-de --help` |

The same profiles work from the command line:

```bash
cd DeutschDotNet
dotnet run                                            # default profile
dotnet run -- ../Examples/hallo.deu --run             # any arguments
```

---

## Using the compiler

```
dotnet-de [options] <file.deu> [<file.deu> ...]      Compile source files (csc style)
dotnet-de [options] <project-file>                   Compile the .deu files of a project (dotnet build style)
dotnet-de [options] <directory>                      Compile all .deu files in a directory tree
dotnet-de [options]                                  Use the project file or .deu files in the current directory
```

Every source file becomes its own executable assembly: `hallo.deu` → `hallo.dll` + `hallo.runtimeconfig.json`.

### Inputs

| Input | Behaviour | Default output directory |
|---|---|---|
| One or more `.deu` files (wildcards like `*.deu` allowed) | Compiles exactly these files | Current directory |
| Project file (`.csproj`, `.proj`, `.deuproj`) | Reads `<DeuCompile Include="…" Exclude="…"/>` items. Without such items, all `**/*.deu` files of the project folder are used | `<project>/bin/<Configuration>/<TargetFramework>` |
| Directory | All `.deu` files in the directory tree (`bin/` and `obj/` are skipped) | `<directory>/bin` |
| Nothing | Uses the single project file in the current directory, otherwise all `.deu` files in it | as above |

### Options

| Option | Description |
|---|---|
| `-o`, `--output <dir>` | Output directory. Also accepted: `-out:<dir>`, `--output=<dir>` |
| `-c`, `--configuration <name>` | Configuration used for the project output path (default `Debug`) |
| `-r`, `--run` | Run the compiled programs after a successful build |
| `-q`, `--quiet` | Print errors only |
| `--version` | Print the compiler version |
| `-h`, `--help` | Show help |

### Examples

```bash
dotnet-de hallo.deu
dotnet-de fibonacci.deu fizzbuzz.deu -o out --run
dotnet-de Examples/Examples.csproj -c Release
dotnet-de Examples                 # directory
```

### Exit codes

| Code | Meaning |
|---|---|
| 0 | All files compiled |
| 1 | At least one file had compile errors (the others are still written) |
| 2 | Invalid command line or input (unknown option, missing file, no sources found) |

### Installing as a global command

The compiler project is configured as a .NET tool:

```bash
dotnet pack DeutschDotNet/DeutschDotNet.csproj -c Release -o ./nupkg
dotnet tool install --global dotnet-de --add-source ./nupkg
dotnet-de --help
```

If you changed the tool and want to update the global installation, repack and reinstall:

```bash
dotnet tool uninstall --global dotnet-de
dotnet pack DeutschDotNet/DeutschDotNet.csproj -c Release -o ./nupkg
dotnet tool install --global dotnet-de --add-source ./nupkg
dotnet-de --help
```

If you previously installed the old command name, remove it once:

```bash
dotnet tool uninstall --global DeutschDotNet
```

Without installing, run it via `dotnet DeutschDotNet/bin/Debug/net10.0/DeutschDotNet.dll …`.

---

## Language reference

### Keywords

| Deutsch# | Meaning | C# equivalent |
|---|---|---|
| `zahl x = 5;` | Declare an integer variable | `int x = 5;` |
| `x = x + 1;` | Assignment | `x = x + 1;` |
| `schreibe ausdruck;` | Print a line | `Console.WriteLine(...)` |
| `wenn bedingung { … }` | Condition | `if (…) { … }` |
| `sonst { … }` | Else branch | `else { … }` |
| `sonst wenn bedingung { … }` | Else-if chain | `else if (…) { … }` |
| `solange bedingung { … }` | Loop | `while (…) { … }` |
| `und`, `oder`, `nicht` | Logical operators | `&&`, `\|\|`, `!` |
| `wahr`, `falsch` | Truth values | `true`, `false` (as 1 and 0) |

### Operators (lowest to highest precedence)

| Precedence | Operators |
|---|---|
| 1 | `oder` |
| 2 | `und` |
| 3 | `==` `!=` `<` `>` `<=` `>=` |
| 4 | `+` `-` |
| 5 | `*` `/` `%` |
| 6 | unary `nicht`, unary `-` |
| 7 | literals, variables, `( … )` |

### Values and types

- **zahl**: the only variable type, a 32-bit integer (`System.Int32`). Integer division and `%` behave like in C#. Division by zero raises a `DivideByZeroException` at runtime.
- **Text**: string literals in double quotes, with the escapes `\n`, `\t`, `\"` and `\\`. Text can be printed and concatenated with `+` (`"Summe: " + summe`), but not stored in variables.
- **Truth values**: integers, where 0 is `falsch` and any other value is `wahr`. Comparisons produce 0 or 1.

### Rules

- Statements end with `;`, blocks are written in `{ }`.
- Variables must be declared with `zahl` before use and can only be declared once per program.
- Names can contain letters (including umlauts such as `größe`), digits and `_`, but can't start with a digit and can't be a keyword.
- Comments start with `//` and run to the end of the line.

---

## Examples

| File | Shows |
|---|---|
| `hallo.deu` | The smallest program, umlauts in text |
| `beispiel.deu` | Factorial, `wenn`/`sonst`, nested loops, prime numbers |
| `fibonacci.deu` | The first 20 Fibonacci numbers, text concatenation |
| `fizzbuzz.deu` | `sonst wenn` chains and the `%` operator |
| `ggt.deu` | Euclid's algorithm (ggT) and lowest common multiple (kgV) |
| `collatz.deu` | The Collatz sequence for 27: steps and maximum value |
| `einmaleins.deu` | Nested loops: multiplication table 1–5 |
| `zinseszins.deu` | Compound interest calculated in cents (integers only) |
| `logik.deu` | `und`, `oder`, `nicht`, parentheses, leap-year rule |

---

## Diagnostics

Errors use the canonical MSBuild format, so Visual Studio shows them in the error list and lets you jump to the exact position:

```
fehler.deu(2,11): error GDC0002: ';' erwartet
fehler.deu(5,1): error GDC0003: Unbekannte Variable 'b' – fehlt 'zahl b = ...;'?
```

The messages are in German on purpose; it's a German language after all.

| Code | Category | Example |
|---|---|---|
| GDC0001 | Lexical error | Unexpected character, unterminated text |
| GDC0002 | Syntax error | Missing `;`, `}` or `)`, keyword used as a name |
| GDC0003 | Semantic error | Unknown variable, duplicate declaration, text where a number is expected |
| GDC0004 | I/O error | Source file can't be read |
| GDC0100 | Command line | Unknown option, missing option value |
| GDC0101 | Input | File not found, no sources, two files producing the same `.dll` name |

---

## How it works

Every compiler, from Roslyn to GCC, follows the same pipeline. DeutschDotNet implements each stage in its simplest form.

```
 .deu source ──► Lexer ──► Tokens ──► Parser ──► AST ──► CodeGenerator ──► IL ──► AssemblyEmitter ──► .dll
```

1. **Lexer** (`Lexer.cs`) turns the text into tokens: keywords (`solange`), names (`fakultät`), numbers, text and symbols (`<=`), each with line and column.
2. **Parser** (`Parser.cs`) is a recursive-descent parser that builds the syntax tree (`Ast.cs`). Operator precedence is simply the order in which the parse methods call each other.
3. **Code generator** (`CodeGenerator.cs`) walks the tree and emits IL with `ILGenerator`. A few translations:

   | Deutsch# | IL |
   |---|---|
   | `zahl x = 5;` | `ldc.i4 5` · `stloc x` |
   | `a <= b` | `ldloc a` · `ldloc b` · `cgt` · `ldc.i4.0` · `ceq` (IL has no "less or equal" instruction) |
   | `solange c { … }` | `START:` c · `brfalse END` · … · `br START` · `END:` |
   | `"Text: " + x` | `ldstr` · `ldloc x` · `box int32` · `call string.Concat(object, object)` |
   | `schreibe x;` | `call Console.WriteLine(int32)` |

4. **Assembly emitter** (`AssemblyEmitter.cs`) uses `PersistedAssemblyBuilder` (available since .NET 9) to save the program as a real PE file with a static `Programm.Main` entry point. It also writes a `runtimeconfig.json`, so `dotnet program.dll` can start it.

You can inspect the generated IL with [ILSpy](https://github.com/icsharpcode/ILSpy) or `ildasm`.

---

## Limitations and roadmap

Deutsch# is intentionally tiny. Known limitations:

- Only one variable type (`zahl`); text can't be stored in variables.
- One global scope per program; variables declared in a block remain visible after it.
- No functions, no input, no access to .NET libraries.
- No debug symbols (PDB), so no breakpoints in `.deu` files.

Ideas for future student projects:

- Nested scopes with a symbol table
- More types: `text`, `kommazahl` (double), `wahrheit` (bool), plus a separate type-checking pass
- Functions: `funktion quadrat(zahl x) { gib zurück x * x; }`
- Input: `lies zahl x;`
- Calling .NET base class library methods
- Portable PDB generation for debugging
- A Visual Studio Code extension with syntax highlighting
