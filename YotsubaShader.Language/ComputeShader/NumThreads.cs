namespace YotsubaShader.Language.ComputeShader;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class NumThreadsAttribute(uint X, uint Y, uint Z) : Attribute;