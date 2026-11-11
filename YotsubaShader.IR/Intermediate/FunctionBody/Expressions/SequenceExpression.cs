namespace YotsubaShader.IR.Intermediate.FunctionBody.Expressions;

/// <summary>
/// Ejecuta unas sentencias y luego produce un valor. Permite expresar construcciones de C# con orden de
/// evaluación propio: <c>c ? a : b</c>, <c>i++</c> como valor, <c>a &amp;&amp; b</c> con cortocircuito.
/// </summary>
public record SequenceExpression(IReadOnlyList<Statement> Prelude, Expression Value) : Expression;
