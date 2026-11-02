# The Deutsch# Lexer

## What is a lexer?

A **lexer** (also called a *lexical analyzer* or *tokenizer*) is the first step in a compiler that understands source code. It reads a stream of characters and groups them into meaningful pieces called **tokens**.

For example, the source

```deutsch
zahl punkte = 12 + 3;
```

can be split into these tokens:

| Kind | Value |
|---|---|
| Keyword | `zahl` |
| Identifier | `punkte` |
| Symbol | `=` |
| Number | `12` |
| Symbol | `+` |
| Number | `3` |
| Symbol | `;` |

Spaces and line breaks separate tokens where needed, but do not usually become tokens themselves. The lexer does not decide whether the sequence makes a valid program. For instance, it can recognize `zahl` and `;` individually; the parser checks whether they appear in a valid declaration.

## How DeutschDotNet uses the lexer

For each `.deu` file, the compiler runs these steps:

```text
source text → lexer → tokens → parser → AST → .NET IL → .dll
```

[`DeutschCompiler.cs`](../source/DeutschDotNet/DeutschCompiler.cs) reads the file, calls `Lexer.Tokenize()`, and passes the returned tokens to the parser. The [`Parser`](../source/DeutschDotNet/Parser.cs) uses those tokens to recognize declarations, assignments, print statements, conditions, loops, and expressions. It builds the abstract syntax tree (AST), which later compiler stages turn into executable .NET code.

The lexer is implemented in [`Lexer.cs`](../source/DeutschDotNet/Lexer.cs). Each token records:

- its **kind** (`Number`, `Identifier`, `String`, `Keyword`, `Symbol`, or `End`),
- its **value** (the text or decoded string content), and
- its **line and column** in the source.

The parser uses this location information so that compiler errors can point back to the source file.

## Tokens recognized by this lexer

### Keywords and names

The language keywords are:

```text
zahl schreibe wenn sonst solange und oder nicht wahr falsch
```

Words not in that list are identifiers, such as variable names. An identifier starts with a letter or `_`; following characters can be letters, digits, or `_`. Letters include German umlauts, so names such as `größe` and `länge` are accepted. A keyword is always a keyword, not an identifier.

### Numbers

A number token consists of one or more decimal digits, for example `0`, `42`, or `2026`. A leading minus sign is a separate `-` symbol; the parser treats it as a unary operator when appropriate. The lexer recognizes the digit sequence, while the parser checks whether its value fits in a 32-bit integer.

### Strings

Text is enclosed in double quotes. The lexer removes the surrounding quotes and decodes these escape sequences:

| Source spelling | Value in the string token |
|---|---|
| `\n` | Newline |
| `\t` | Tab |
| `\"` | Double quote |
| `\\` | Backslash |

For example, `"Hallo\nWelt"` becomes one `String` token whose value contains a newline between `Hallo` and `Welt`.

### Symbols

The two-character symbols are `==`, `!=`, `<=`, and `>=`. The lexer checks these before one-character symbols so that, for example, `<=` is one token rather than `<` followed by `=`.

The one-character symbols are:

```text
+ - * / % < > = ( ) { } ;
```

### Whitespace and comments

Whitespace separates tokens and is ignored. The lexer tracks line and column positions while skipping it.

Comments begin with `//` and continue to the end of the line. They are skipped rather than emitted as tokens:

```deutsch
zahl anzahl = 3; // This comment is not part of the token stream.
```

At the end of every input, the lexer emits an `End` token so the parser knows there are no more tokens.

## Example from Deutsch#

Given this source:

```deutsch
zahl größe = 12 + 3; // Gesamtwert
schreibe "Größe: " + größe;
```

The lexer emits the following meaningful tokens; whitespace and the comment are omitted:

```text
Keyword("zahl")
Identifier("größe")
Symbol("=")
Number("12")
Symbol("+")
Number("3")
Symbol(";")
Keyword("schreibe")
String("Größe: ")
Symbol("+")
Identifier("größe")
Symbol(";")
End("<Dateiende>")
```

The parser can then interpret the first statement as a variable declaration and the second as a print statement. Those grammatical meanings are not assigned by the lexer.

## Lexical errors

If the lexer encounters a character it does not recognize, a string without a closing quote, or an unknown string escape, it reports a lexical compile error (`GDC0001`) at a source location. For example, `@` is not a Deutsch# symbol, and `"\q"` contains an unsupported escape.

Errors such as a missing semicolon are instead detected by the parser, because they concern the arrangement of otherwise valid tokens.