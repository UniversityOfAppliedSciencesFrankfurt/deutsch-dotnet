namespace DeutschDotNet;

// ── Expressions ──
public abstract record Expression;
public sealed record NumberLiteral(int Value) : Expression;
public sealed record StringLiteral(string Value) : Expression;
public sealed record VariableReference(string Name, int Line, int Column) : Expression;
public sealed record UnaryExpression(string Operator, Expression Operand) : Expression;
public sealed record BinaryExpression(string Operator, Expression Left, Expression Right) : Expression;

// ── Statements ──
public abstract record Statement(int Line, int Column);
public sealed record VariableDeclaration(string Name, Expression Value, int Line, int Column) : Statement(Line, Column);
public sealed record Assignment(string Name, Expression Value, int Line, int Column) : Statement(Line, Column);
public sealed record PrintStatement(Expression Value, int Line, int Column) : Statement(Line, Column);
public sealed record IfStatement(Expression Condition, IReadOnlyList<Statement> Then, IReadOnlyList<Statement>? Else, int Line, int Column) : Statement(Line, Column);
public sealed record WhileStatement(Expression Condition, IReadOnlyList<Statement> Body, int Line, int Column) : Statement(Line, Column);
