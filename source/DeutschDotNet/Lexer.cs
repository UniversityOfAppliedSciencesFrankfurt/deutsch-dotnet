using System.Text;

namespace DeutschDotNet;

public enum TokenKind { Number, Identifier, String, Keyword, Symbol, End }

public sealed record Token(TokenKind Kind, string Value, int Line, int Column);

/// <summary>
/// Represents a lexical analyzer that reads source text and produces tokens.
/// </summary>
/// <remarks>Tracks line and column information, skips whitespace and single-line comments, and recognizes
/// keywords, identifiers, numbers, string literals, and symbols.</remarks>
/// <param name="source">Source text to analyze.</param>
public sealed class Lexer(string source)
{
    public static readonly HashSet<string> Keywords =
        ["zahl", "schreibe", "wenn", "sonst", "solange", "und", "oder", "nicht", "wahr", "falsch"];

    static readonly HashSet<string> TwoCharSymbols = ["==", "!=", "<=", ">="];
    const string OneCharSymbols = "+-*/%<>=(){};";

    int pos;
    int line = 1;
    int lineStart;

    int Column => pos - lineStart + 1;


    /// <summary>
    /// Converts the source text into a sequence of lexical tokens.
    /// </summary>
    /// <remarks>Skips whitespace and comments before reading each token and records line and column
    /// information for each emitted token.</remarks>
    /// <returns>A list of tokens read from the source, including an end-of-file token of kind End as the final item.</returns>
    /// <exception cref="CompileError">Thrown when an unexpected character is encountered that cannot be tokenized.</exception>
    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        while (true)
        {
            SkipWhitespaceAndComments();
            int l = line, c = Column;

            if (pos >= source.Length)
            {
                tokens.Add(new(TokenKind.End, "<Dateiende>", l, c));
                return tokens;
            }

            char ch = source[pos];

            if (char.IsDigit(ch))
            {
                int start = pos;
                while (pos < source.Length && char.IsDigit(source[pos])) pos++;
                tokens.Add(new(TokenKind.Number, source[start..pos], l, c));
            }
            else if (char.IsLetter(ch) || ch == '_')          // umlauts allowed: größe, länge, ...
            {
                int start = pos;
                while (pos < source.Length && (char.IsLetterOrDigit(source[pos]) || source[pos] == '_')) pos++;
                var word = source[start..pos];
                tokens.Add(new(Keywords.Contains(word) ? TokenKind.Keyword : TokenKind.Identifier, word, l, c));
            }
            else if (ch == '"')
            {
                tokens.Add(new(TokenKind.String, ReadString(l, c), l, c));
            }
            else if (pos + 1 < source.Length && TwoCharSymbols.Contains(source.Substring(pos, 2)))
            {
                tokens.Add(new(TokenKind.Symbol, source.Substring(pos, 2), l, c));
                pos += 2;
            }
            else if (OneCharSymbols.Contains(ch))
            {
                tokens.Add(new(TokenKind.Symbol, ch.ToString(), l, c));
                pos++;
            }
            else
            {
                throw new CompileError(ErrorCodes.Lexical, $"Unerwartetes Zeichen '{ch}'", l, c);
            }
        }
    }

    /// <summary>
    /// Reads a string literal from the source, starting at the current position.
    /// </summary>
    /// <param name="startLine">The line number where the string literal starts.</param>
    /// <param name="startColumn">The column number where the string literal starts.</param>
    /// <returns>The contents of the string literal.</returns>
    /// <exception cref="CompileError">Thrown when the string literal is not properly terminated or contains an unknown escape sequence.</exception>
    string ReadString(int startLine, int startColumn)
    {
        pos++;                                                  // opening quote
        var sb = new StringBuilder();
        while (true)
        {
            if (pos >= source.Length || source[pos] == '\n')
                throw new CompileError(ErrorCodes.Lexical, "Text nicht abgeschlossen (fehlendes '\"')", startLine, startColumn);

            char ch = source[pos++];
            if (ch == '"') return sb.ToString();

            if (ch == '\\' && pos < source.Length)
            {
                char esc = source[pos++];
                sb.Append(esc switch
                {
                    'n' => '\n',
                    't' => '\t',
                    '"' => '"',
                    '\\' => '\\',
                    _ => throw new CompileError(ErrorCodes.Lexical, $"Unbekannte Escape-Sequenz '\\{esc}'", line, Column - 2)
                });
            }
            else
            {
                sb.Append(ch);
            }
        }
    }

    /// <summary>
    /// Skips whitespace and comments in the source text, updating the current position, line, and column.
    /// </summary>
    void SkipWhitespaceAndComments()
    {
        while (pos < source.Length)
        {
            char ch = source[pos];
            if (ch == '\n') { pos++; line++; lineStart = pos; }
            else if (char.IsWhiteSpace(ch)) pos++;
            else if (ch == '/' && pos + 1 < source.Length && source[pos + 1] == '/')
                while (pos < source.Length && source[pos] != '\n') pos++;
            else break;
        }
    }
}
