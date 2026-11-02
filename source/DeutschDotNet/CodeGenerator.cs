using System.Reflection;
using System.Reflection.Emit;

namespace DeutschDotNet;

/// <summary>
/// Walks the syntax tree and emits IL into a method body.
/// Types: every variable is an Int32 ("zahl"); string literals and "+"-concatenation produce a String.
/// Truth values are Int32 (0 = falsch, otherwise wahr), exactly like bool on the IL evaluation stack.
/// </summary>
public sealed class CodeGenerator(ILGenerator il)
{
    static readonly MethodInfo WriteLineInt = typeof(Console).GetMethod(nameof(Console.WriteLine), [typeof(int)])!;
    static readonly MethodInfo WriteLineString = typeof(Console).GetMethod(nameof(Console.WriteLine), [typeof(string)])!;
    static readonly MethodInfo Concat = typeof(string).GetMethod(nameof(string.Concat), [typeof(object), typeof(object)])!;

    readonly Dictionary<string, LocalBuilder> variables = new();
    int line, column;

    CompileError Error(string message) => new(ErrorCodes.Semantic, message, line, column);

    public void Generate(IReadOnlyList<Statement> program)
    {
        foreach (var statement in program) EmitStatement(statement);
        il.Emit(OpCodes.Ret);
    }

    void EmitStatement(Statement statement)
    {
        (line, column) = (statement.Line, statement.Column);

        switch (statement)
        {
            case VariableDeclaration d:
                if (variables.ContainsKey(d.Name)) throw Error($"Die Variable '{d.Name}' ist bereits deklariert");
                EmitNumber(d.Value);                          // value first, so "zahl x = x;" is an error
                var local = il.DeclareLocal(typeof(int));
                variables[d.Name] = local;
                il.Emit(OpCodes.Stloc, local);
                break;

            case Assignment a:
                var target = Lookup(a.Name);
                EmitNumber(a.Value);
                il.Emit(OpCodes.Stloc, target);
                break;

            case PrintStatement p:
                var type = EmitExpression(p.Value);
                il.Emit(OpCodes.Call, type == typeof(string) ? WriteLineString : WriteLineInt);
                break;

            case IfStatement i:
            {
                var elseLabel = il.DefineLabel();
                var endLabel = il.DefineLabel();
                EmitNumber(i.Condition);
                il.Emit(OpCodes.Brfalse, elseLabel);
                foreach (var s in i.Then) EmitStatement(s);
                il.Emit(OpCodes.Br, endLabel);
                il.MarkLabel(elseLabel);
                if (i.Else != null) foreach (var s in i.Else) EmitStatement(s);
                il.MarkLabel(endLabel);
                break;
            }

            case WhileStatement w:
            {
                var startLabel = il.DefineLabel();
                var endLabel = il.DefineLabel();
                il.MarkLabel(startLabel);
                EmitNumber(w.Condition);
                il.Emit(OpCodes.Brfalse, endLabel);
                foreach (var s in w.Body) EmitStatement(s);
                il.Emit(OpCodes.Br, startLabel);
                il.MarkLabel(endLabel);
                break;
            }

            default:
                throw new InvalidOperationException($"Unknown statement {statement.GetType().Name}");
        }
    }

    LocalBuilder Lookup(string name) =>
        variables.TryGetValue(name, out var local) ? local : throw Error($"Unbekannte Variable '{name}' – fehlt 'zahl {name} = ...;'?");

    static Type TypeOf(Expression e) => e switch
    {
        StringLiteral => typeof(string),
        BinaryExpression { Operator: "+" } b when TypeOf(b.Left) == typeof(string) || TypeOf(b.Right) == typeof(string)
            => typeof(string),
        _ => typeof(int)
    };

    /// <summary>Emits an expression; afterwards its value is on the evaluation stack.</summary>
    Type EmitExpression(Expression e)
    {
        switch (e)
        {
            case NumberLiteral n:
                il.Emit(OpCodes.Ldc_I4, n.Value);
                return typeof(int);

            case StringLiteral s:
                il.Emit(OpCodes.Ldstr, s.Value);
                return typeof(string);

            case VariableReference v:
                (line, column) = (v.Line, v.Column);
                il.Emit(OpCodes.Ldloc, Lookup(v.Name));
                return typeof(int);

            case UnaryExpression u:
                EmitNumber(u.Operand);
                if (u.Operator == "-") il.Emit(OpCodes.Neg);
                else EmitNot();
                return typeof(int);

            case BinaryExpression b when TypeOf(b) == typeof(string):        // "Text: " + zahl
                EmitAsObject(b.Left);
                EmitAsObject(b.Right);
                il.Emit(OpCodes.Call, Concat);
                return typeof(string);

            case BinaryExpression b when b.Operator is "und" or "oder":
                EmitAsTruthValue(b.Left);
                EmitAsTruthValue(b.Right);
                il.Emit(b.Operator == "und" ? OpCodes.And : OpCodes.Or);
                return typeof(int);

            case BinaryExpression b:
                EmitNumber(b.Left);
                EmitNumber(b.Right);
                switch (b.Operator)
                {
                    case "+": il.Emit(OpCodes.Add); break;
                    case "-": il.Emit(OpCodes.Sub); break;
                    case "*": il.Emit(OpCodes.Mul); break;
                    case "/": il.Emit(OpCodes.Div); break;
                    case "%": il.Emit(OpCodes.Rem); break;
                    case "==": il.Emit(OpCodes.Ceq); break;
                    case "!=": il.Emit(OpCodes.Ceq); EmitNot(); break;
                    case "<": il.Emit(OpCodes.Clt); break;
                    case ">": il.Emit(OpCodes.Cgt); break;
                    case "<=": il.Emit(OpCodes.Cgt); EmitNot(); break;
                    case ">=": il.Emit(OpCodes.Clt); EmitNot(); break;
                    default: throw new InvalidOperationException($"Unknown operator {b.Operator}");
                }
                return typeof(int);
        }
        throw new InvalidOperationException($"Unknown expression {e.GetType().Name}");
    }

    void EmitNumber(Expression e)
    {
        if (EmitExpression(e) != typeof(int)) throw Error("Zahl erwartet, aber Text gefunden");
    }

    void EmitNot()
    {
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ceq);
    }

    /// <summary>Normalises any number to 0 or 1, so that "und"/"oder" work bitwise.</summary>
    void EmitAsTruthValue(Expression e)
    {
        EmitNumber(e);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Cgt_Un);
    }

    void EmitAsObject(Expression e)
    {
        if (EmitExpression(e) == typeof(int)) il.Emit(OpCodes.Box, typeof(int));
    }
}
