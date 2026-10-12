namespace YotsubaShader.IR.Intermediate.FunctionBody.Expressions.AssignmentTargets;

public record StorageArrayElementReference(Expression ArrayReference, Expression Index) : Expression;