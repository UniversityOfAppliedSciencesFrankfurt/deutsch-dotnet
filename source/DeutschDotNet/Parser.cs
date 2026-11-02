namespace DeutschDotNet;

/// <summary>
/// Recursive-descent parser. Operator precedence, lowest to highest:
/// oder → und → comparison (== != &lt; &gt; &lt;= &gt;=) → + - → * / % → unary (nicht, -) → primary
/// </summary>
public sealed class Parser(List<Token> tokens)
{
    int pos;

    Token Current => tokens[pos];

    bool Is(string value) => Current.Kind is TokenKind.Symbol or TokenKind.Keyword && Current.Value == value;

    CompileError Error(string message) => new(ErrorCodes.Syntax, message, Current.Line, Current.Column);

    void Expect(string value)
    {
        if (Is(value)) { pos++; return; }

        // A missing ';' or ')' is reported right after the previous token, like the C# compiler does.
        if (value is ";" or ")" && pos > 0 && tokens[pos - 1].Line < Current.Line)
        {
            var previous = tokens[pos - 1];
            int length = previous.Kind == TokenKind.String ? previous.Value.Length + 2 : previous.Value.Length;
            throw new CompileError(ErrorCodes.Syntax, $"'{value}' erwartet", previous.Line, previous.Column + length);
        }
        throw Error($"'{value}' erwartet, aber '{Current.Value}' gefunden");
    }

    string ExpectName()
    {
        if (Current.Kind != TokenKind.Identifier)
            throw Error(Current.Kind == TokenKind.Keyword
                ? $"'{Current.Value}' ist ein Schlüsselwort und kann nicht als Name verwendet werden"
                : $"Name erwartet, aber '{Current.Value}' gefunden");
        return tokens[pos++].Value;
    }

    public List<Statement> ParseProgram()
    {
        var statements = new List<Statement>();
        while (Current.Kind != TokenKind.End) statements.Add(ParseStatement());
        return statements;
    }

    List<Statement> ParseBlock()
    {
        Expect("{");
        var statements = new List<Statement>();
        while (!Is("}"))
        {
            if (Current.Kind == TokenKind.End) throw Error("'}' fehlt");
            statements.Add(ParseStatement());
        }
        pos++;
        return statements;
    }

    Statement ParseStatement()
    {
        int line = Current.Line, column = Current.Column;

        if (Is("zahl"))
        {
            pos++;
            var name = ExpectName();
            Expect("=");
            var value = ParseExpression();
            Expect(";");
            return new VariableDeclaration(name, value, line, column);
        }

        if (Is("schreibe"))
        {
            pos++;
            var value = ParseExpression();
            Expect(";");
            return new PrintStatement(value, line, column);
        }

        if (Is("wenn"))
        {
            pos++;
            var condition = ParseExpression();
            var then = ParseBlock();
            List<Statement>? otherwise = null;
            if (Is("sonst"))
            {
                pos++;
                otherwise = Is("wenn") ? [ParseStatement()] : ParseBlock();   // "sonst wenn" chains
            }
            return new IfStatement(condition, then, otherwise, line, column);
        }

        if (Is("solange"))
        {
            pos++;
            var condition = ParseExpression();
            return new WhileStatement(condition, ParseBlock(), line, column);
        }

        if (Current.Kind == TokenKind.Identifier)
        {
            var name = ExpectName();
            Expect("=");
            var value = ParseExpression();
            Expect(";");
            return new Assignment(name, value, line, column);
        }

        throw Error($"Unerwartetes '{Current.Value}' – Anweisung erwartet");
    }

    Expression ParseExpression() => ParseOr();

    Expression ParseOr()
    {
        var left = ParseAnd();
        while (Is("oder")) { pos++; left = new BinaryExpression("oder", left, ParseAnd()); }
        return left;
    }

    Expression ParseAnd()
    {
        var left = ParseComparison();
        while (Is("und")) { pos++; left = new BinaryExpression("und", left, ParseComparison()); }
        return left;
    }

    Expression ParseComparison()
    {
        var left = ParseAdditive();
        while (Current.Kind == TokenKind.Symbol && Current.Value is "==" or "!=" or "<" or ">" or "<=" or ">=")
        {
            var op = tokens[pos++].Value;
            left = new BinaryExpression(op, left, ParseAdditive());
        }
        return left;
    }

    Expression ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (Is("+") || Is("-"))
        {
            var op = tokens[pos++].Value;
            left = new BinaryExpression(op, left, ParseMultiplicative());
        }
        return left;
    }

    Expression ParseMultiplicative()
    {
        var left = ParseUnary();
        while (Is("*") || Is("/") || Is("%"))
        {
            var op = tokens[pos++].Value;
            left = new BinaryExpression(op, left, ParseUnary());
        }
        return left;
    }

    Expression ParseUnary()
    {
        if (Is("nicht")) { pos++; return new UnaryExpression("nicht", ParseUnary()); }
        if (Is("-")) { pos++; return new UnaryExpression("-", ParseUnary()); }
        return ParsePrimary();
    }

    Expression ParsePrimary()
    {
        var token = Current;
        switch (token.Kind)
        {
            case TokenKind.Number:
                pos++;
                if (!int.TryParse(token.Value, out var number))
                    throw new CompileError(ErrorCodes.Syntax, $"Zahl zu groß: {token.Value} (Maximum {int.MaxValue})", token.Line, token.Column);
                return new NumberLiteral(number);
            case TokenKind.String:
                pos++;
                return new StringLiteral(token.Value);
            case TokenKind.Identifier:
                pos++;
                return new VariableReference(token.Value, token.Line, token.Column);
        }

        if (Is("wahr")) { pos++; return new NumberLiteral(1); }
        if (Is("falsch")) { pos++; return new NumberLiteral(0); }
        if (Is("("))
        {
            pos++;
            var inner = ParseExpression();
            Expect(")");
            return inner;
        }

        throw Error($"Ausdruck erwartet, aber '{token.Value}' gefunden");
    }
}
