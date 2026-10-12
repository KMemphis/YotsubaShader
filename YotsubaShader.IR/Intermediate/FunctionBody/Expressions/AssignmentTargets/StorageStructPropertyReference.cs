namespace YotsubaShader.IR.Intermediate.FunctionBody.Expressions.AssignmentTargets;

public record StorageStructPropertyReference(
    Expression Reference, 
    string PropertyName) : Expression;