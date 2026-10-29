using YotsubaShader.Compiler.SpirV.Builder;
using YotsubaShader.Compiler.SpirV.YS_Types;
using YotsubaShader.IR.Intermediate;

namespace YotsubaShader.Compiler.SpirV;

public partial class IRToSpirvModule
{
    public SpirVModule Compile(Shader shader)
    {
        ArgumentNullException.ThrowIfNull(shader);
        var module = new SpirVModule();

        // 1. Cabecera del módulo.
        BuildHeader(module, shader);
        BuildEntryPoints(module, shader);
        // 2. Tipos, constantes e interfaces globales.
        BuildFunctionTypes(module);
        BuildIrTypes.BuildRequiredTypes(module, shader);
        // 3. Entry points.
        BuildFunctionSkeletons(module, shader);
        // 4. Funciones y sus cuerpos.
        return module;
    }
}