using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using YotsubaShader.Frontend.Model;
using YotsubaShader.IR.Intermediate;
using YotsubaShader.IR.Intermediate.FunctionBody;
using YotsubaShader.IR.Intermediate.Slot;
using YotsubaShader.IR.Intermediate.Types;
using ShaderStage = YotsubaShader.Compiler.Stages.ShaderStage;

namespace YotsubaShader.Frontend;

public class ShaderProcessor
{
    public readonly List<Shader> Shaders = new List<Shader>();

    public void Process(Compilation compilation, CancellationToken token)
    { 
        var csharpShaderMetadata = ShaderDiscovery.Find(compilation, token);
        foreach (var shaderInfo in csharpShaderMetadata)
        {
            
            List<Function> shaderFunction = new List<Function>();
            foreach (var shaderInfoEntryPoint in shaderInfo.EntryPoints)
            {

                Stage stage = shaderInfoEntryPoint.Stage == ShaderStage.Compute ? Stage.Compute :
                    shaderInfoEntryPoint.Stage == ShaderStage.Fragment ? Stage.Fragment :
                    shaderInfoEntryPoint.Stage == ShaderStage.Vertex ? Stage.Vertex : Stage.None;

                List<Storage> inputs = new List<Storage>();
                List<Storage> outputs = new List<Storage>();
                List<Statement> bodyFunc = new List<Statement>();
                
                //Process Inputs
                foreach (var parameterInfo in shaderInfoEntryPoint.Parameters)
                {
                    inputs.Add(ProcessInput(parameterInfo));
                }
                    
                var syntax = shaderInfoEntryPoint.Method.DeclaringSyntaxReferences[0].GetSyntax();
                var semanticModel = compilation.GetSemanticModel(syntax.SyntaxTree);

                var operations = semanticModel.GetOperation(syntax);

                if (operations is IMethodBodyOperation body)
                {
                    IBlockOperation blockOperations = body.BlockBody ?? body.ExpressionBody ?? throw new NotImplementedException();
                    foreach (var op in blockOperations.Operations)
                    {
                        switch (op)
                        {
                            case IAssignmentOperation assignmentOperation:
                                    
                                break;
                        }
                    }
                }
                
                
                Function fn = new Function(shaderInfoEntryPoint.Name, stage, inputs, outputs, bodyFunc);

            }
        }
    }

    private Storage ProcessInput(ParameterInfo parameterInfo)
    {
        Storage storage;

        InterfaceSlot slot;
        var atributes = parameterInfo.Type.GetAttributes();
 
        foreach (var attributeData in atributes)
        {
            var attributeType =  attributeData.AttributeClass;

            switch (attributeType)
            {
                 
            }
        }
        
        switch (parameterInfo.Type.SpecialType)
        {
            case SpecialType.System_Int32:
                storage = new Int(parameterInfo.Name, StorageKind.Uniform, );
                break;
        }
        

        return storage;
    }
}