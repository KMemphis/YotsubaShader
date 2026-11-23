using System.Text;
using System.Text.RegularExpressions;
using YotsubaShader.IR.Intermediate;
using YotsubaShader.IR.Intermediate.Slot;
using YotsubaShader.IR.Intermediate.Slot.Types;
using YotsubaShader.IR.Intermediate.Types;

namespace YotsubaShader.Extensions.Godot;

/// <summary>
/// SPIR-V → GLSL (SPIRV-Cross) → Godot Shading Language. Las etapas vertex y fragment se funden en un
/// único .gdshader: <c>main</c> pasa a <c>vertex()</c>/<c>fragment()</c>, las entradas y salidas de etapa se
/// convierten en built-ins o <c>varying</c>, y los uniform buffers en <c>uniform</c> sueltos.
/// </summary>
internal static class GodotShaderTranslator
{
    private static readonly Regex GlType = new(@"^(float|int|uint|bool|[biu]?vec[234]|mat[234](x[234])?)$", RegexOptions.Compiled);
    private static readonly Regex GlBuiltIn = new(@"\bgl_\w+", RegexOptions.Compiled);
    private static readonly Regex Return = new(@"\breturn\s*;", RegexOptions.Compiled);
    private static readonly Regex CombinedSampler = new(
        @"\bsampler(?:2D|3D|Cube|2DArray|CubeArray)\(\s*(\w+)\s*,\s*\w+\s*\)", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> TextureTypes = new()
    {
        ["sampler2D"] = "sampler2D",
        ["sampler3D"] = "sampler3D",
        ["samplerCube"] = "samplerCube",
        ["sampler2DArray"] = "sampler2DArray",
        ["samplerCubeArray"] = "samplerCubeArray",
        ["texture2D"] = "sampler2D",
        ["texture3D"] = "sampler3D",
        ["textureCube"] = "samplerCube",
        ["texture2DArray"] = "sampler2DArray",
        ["textureCubeArray"] = "samplerCubeArray",
    };

    public static string Translate(CompiledCode shader, GodotShaderType type)
    {
        bool spatial = type == GodotShaderType.Spatial;
        string shaderType = spatial ? "spatial" : "canvas_item";

        Function? vertexFunction = null, fragmentFunction = null;
        foreach (Function function in shader.Reflection.IntermediateLanguage.Functions)
        {
            if (function.Stage == Stage.Vertex) vertexFunction = function;
            else if (function.Stage == Stage.Fragment) fragmentFunction = function;
        }

        if (vertexFunction is null && fragmentFunction is null)
        {
            throw new NotSupportedException(
                $"El shader '{shader.CSharpFullClassName}' no tiene etapas vertex ni fragment. " +
                "Godot Shading Language no tiene compute shaders.");
        }

        GlslModule? vertex = vertexFunction is null ? null : GlslModule.Parse(shader.ToGlsl(Stage.Vertex));
        GlslModule? fragment = fragmentFunction is null ? null : GlslModule.Parse(shader.ToGlsl(Stage.Fragment));

        // Cada salida de usuario del vertex es un varying; el fragment la lee por location.
        var varyings = new List<(string Name, GlslInterface Variable)>();
        var vertexRenames = new Dictionary<string, string>();
        if (vertex is not null)
        {
            var used = new HashSet<string>();
            foreach (GlslInterface output in vertex.Outputs)
            {
                string name = "v_" + Regex.Replace(output.Name, @"^_*(return_)?", "");
                if (name == "v_") name = "v_out";
                if (!used.Add(name)) name += "_" + output.Location;
                varyings.Add((name, output));
                vertexRenames[output.Name] = name;
            }
        }

        var fragmentRenames = new Dictionary<string, string>();
        if (fragment is not null)
        {
            foreach (GlslInterface input in fragment.Inputs)
            {
                var source = varyings.FirstOrDefault(v => v.Variable.Location == input.Location);
                if (source.Name is null)
                {
                    throw new NotSupportedException(
                        $"El fragment lee la location {input.Location} ('{input.Name}') y el vertex no la escribe: " +
                        "en Godot las entradas del fragment vienen de varyings del vertex.");
                }

                fragmentRenames[input.Name] = source.Name;
            }
        }

        string vertexText = vertex is null ? "" : BuildVertex(vertex, vertexFunction!.Value, vertexRenames, spatial);
        string fragmentText = fragment is null ? "" : BuildFragment(fragment, fragmentRenames, spatial);

        // Piezas compartidas por las dos etapas, sin duplicados.
        var helperNames = new HashSet<string>();
        var structNames = new HashSet<string>();
        var globalTexts = new HashSet<string>();
        var shared = new StringBuilder();
        var modules = new[] { vertex, fragment }.Where(m => m is not null).Select(m => m!).ToList();

        foreach (GlslModule module in modules)
        {
            foreach (GlslFunction structure in module.Structs)
            {
                if (structNames.Add(structure.Name)) shared.AppendLine(StripLayout(structure.Text)).AppendLine();
            }
        }

        foreach (GlslModule module in modules)
        {
            foreach (GlslFunction global in module.Globals)
            {
                if (globalTexts.Add(global.Text)) shared.AppendLine(StripLayout(global.Text)).AppendLine();
            }
        }

        var instanceReplacements = new List<(Regex Pattern, string Replacement)>();
        foreach (GlslModule module in modules)
        {
            foreach (GlslBlock block in module.Blocks)
            {
                if (block.Instance is not null)
                {
                    instanceReplacements.Add((new Regex($@"\b{Regex.Escape(block.Instance)}\.(\w+)"), block.Instance + "_$1"));
                }
            }
        }

        foreach (GlslModule module in modules)
        {
            foreach (GlslFunction function in module.Functions)
            {
                if (helperNames.Add(function.Name)) shared.AppendLine(function.Text).AppendLine();
            }
        }

        string code = shared + vertexText + (vertexText.Length > 0 ? "\n" : "") + fragmentText;
        code = Finish(code, instanceReplacements);

        // Declaraciones: solo lo que el código usa (SPIRV-Cross declara todos los recursos del módulo).
        var declarations = new StringBuilder();
        var declared = new HashSet<string>();
        foreach (GlslModule module in modules)
        {
            foreach (GlslBlock block in module.Blocks)
            {
                foreach (GlslMember member in block.Members)
                {
                    string name = block.Instance is null ? member.Name : block.Instance + "_" + member.Name;
                    if (!IsUsed(code, name)) continue;

                    if (block.Qualifier == "buffer")
                    {
                        throw new NotSupportedException(
                            $"'{block.Instance ?? block.BlockName}': los storage buffers no existen en Godot Shading Language. " +
                            "Usa texturas o uniforms.");
                    }

                    if (!GlType.IsMatch(member.Type))
                    {
                        throw new NotSupportedException(
                            $"'{name}': Godot no admite structs como uniform. Declara los campos de '{member.Type}' por separado.");
                    }

                    if (declared.Add(name)) declarations.AppendLine($"uniform {member.Type} {name}{member.Array};");
                }
            }

            foreach (GlslResource resource in module.Resources)
            {
                if (resource.Type is "sampler" or "samplerShadow") continue;
                if (!IsUsed(code, resource.Name)) continue;

                if (!TextureTypes.TryGetValue(resource.Type, out string? godotType))
                {
                    throw new NotSupportedException(
                        $"'{resource.Name}': el tipo de recurso '{resource.Type}' no tiene equivalente en Godot Shading Language.");
                }

                if (declared.Add(resource.Name)) declarations.AppendLine($"uniform {godotType} {resource.Name};");
            }
        }

        var varyingText = new StringBuilder();
        foreach (var (name, variable) in varyings)
        {
            varyingText.AppendLine($"varying {(variable.Flat || IsInteger(variable.Type) ? "flat " : "")}{variable.Type} {name};");
        }

        var header = new StringBuilder();
        header.AppendLine($"// Generado por YotsubaShader desde {shader.CSharpFullClassName}. No lo edites a mano.");
        header.AppendLine($"shader_type {shaderType};");
        // unshaded: el fragment ya calcula el color final. cull_disabled: el vertex escribe el espacio de recorte
        // directamente y Godot (Y hacia abajo, como Vulkan) invierte el sentido de las caras; un ShaderMaterial
        // no puede cambiar el culling, así que se desactiva para que la malla no desaparezca.
        if (spatial) header.AppendLine("render_mode unshaded, cull_disabled;");
        header.AppendLine();
        if (declarations.Length > 0) header.AppendLine(declarations.ToString());
        if (varyingText.Length > 0) header.AppendLine(varyingText.ToString());

        return header + code.TrimEnd() + "\n";
    }

    private static string BuildVertex(GlslModule module, Function function, Dictionary<string, string> renames, bool spatial)
    {
        var prologue = new StringBuilder();
        string body = module.MainBody;

        foreach (GlslInterface input in module.Inputs)
        {
            VertexSlot slot = SlotOf(function, input.Name);
            try
            {
                prologue.AppendLine("    " + InputInitializer(input, slot.Semantic, spatial));
            }
            catch (NotSupportedException) when (!body.Contains(input.Name))
            {
                // Un dato que este tipo de shader no entrega (p. ej. NORMAL en canvas_item) y que el shader no usa:
                // se declara en cero para que la entrada exista, sin fallar.
                prologue.AppendLine($"    {input.Type} {input.Name} = {input.Type}(0);");
            }
        }

        body = Rename(body, renames);

        var epilogue = new StringBuilder();
        if (body.Contains("gl_Position"))
        {
            body = body.Replace("gl_Position", "ys_position");
            prologue.AppendLine("    vec4 ys_position = vec4(0.0);");
            epilogue.AppendLine(spatial ? "POSITION = ys_position;" : "VERTEX = ys_position.xy;");
        }

        var builtIns = new Dictionary<string, string>
        {
            ["gl_VertexIndex"] = "VERTEX_ID",
            ["gl_InstanceIndex"] = "INSTANCE_ID",
            ["gl_VertexID"] = "VERTEX_ID",
            ["gl_InstanceID"] = "INSTANCE_ID",
        };
        if (spatial) builtIns["gl_PointSize"] = "POINT_SIZE";

        body = MapBuiltIns(body, builtIns, "vertex");
        return Entry("vertex", prologue.ToString(), body, epilogue.ToString());
    }

    private static string BuildFragment(GlslModule module, Dictionary<string, string> renames, bool spatial)
    {
        var prologue = new StringBuilder();
        var epilogue = new StringBuilder();
        string body = Rename(module.MainBody, renames);

        foreach (GlslInterface output in module.Outputs)
        {
            if (output.Location != 0)
            {
                throw new NotSupportedException(
                    $"'{output.Name}': Godot Shading Language solo escribe un color por fragment (SV_Target0); " +
                    $"no hay render targets múltiples (location {output.Location}).");
            }

            if (IsInteger(output.Type))
            {
                throw new NotSupportedException($"'{output.Name}': el color de salida debe ser float, no {output.Type}.");
            }

            string local = "ys_color";
            body = Regex.Replace(body, $@"\b{Regex.Escape(output.Name)}\b", local);
            prologue.AppendLine($"    {output.Type} {local} = {Zero(output.Type)};");
            epilogue.AppendLine(ColorStore(local, output.Type, spatial));
        }

        var builtIns = new Dictionary<string, string>
        {
            ["gl_FragCoord"] = "FRAGCOORD",
            ["gl_PointCoord"] = "POINT_COORD",
        };
        if (spatial)
        {
            builtIns["gl_FrontFacing"] = "FRONT_FACING";
            builtIns["gl_FragDepth"] = "DEPTH";
        }

        body = MapBuiltIns(body, builtIns, "fragment");
        return Entry("fragment", prologue.ToString(), body, epilogue.ToString());
    }

    /// <summary>
    /// Godot Shading Language no admite <c>return</c> dentro de <c>vertex()</c> ni <c>fragment()</c>. Si el cuerpo
    /// tiene alguno, se envuelve en un <c>for</c> de una sola vuelta y cada <c>return;</c> pasa a ser un <c>break;</c>:
    /// así las salidas del epílogo se escriben una sola vez, al final.
    /// </summary>
    private static string Entry(string name, string prologue, string body, string epilogue)
    {
        epilogue = epilogue.TrimEnd();
        if (Return.IsMatch(body))
        {
            WrapEarlyReturns(body, false, out bool needsFlag);
            string rewritten = WrapEarlyReturns(body, needsFlag, out _);
            if (needsFlag) prologue += "    bool ys_returned = false;\n";
            body = "    for (int ys_once = 0; ys_once < 1; ys_once++)\n    {\n" + rewritten.Trim('\r', '\n') + "\n    }";
        }

        var text = new StringBuilder();
        text.AppendLine($"void {name}() {{");
        text.Append(prologue);
        text.Append(body.Trim('\r', '\n'));
        text.AppendLine();
        if (epilogue.Length > 0) text.AppendLine("    " + epilogue.Replace("\n", "\n    "));
        text.AppendLine("}");
        return text.ToString();
    }

    /// <summary>
    /// Sustituye cada <c>return;</c> por <c>break;</c>. Si el return está dentro de un bucle o un switch, el <c>break</c>
    /// solo saldría de ese, así que se levanta <c>ys_returned</c> y, al cerrar cada bucle, se vuelve a salir.
    /// </summary>
    private static string WrapEarlyReturns(string body, bool insertChecks, out bool usedFlag)
    {
        usedFlag = false;
        var output = new StringBuilder();
        var contexts = new Stack<int>(); // 0 = bloque normal, 1 = bucle/switch, 2 = cuerpo de un do-while
        bool pendingDoCheck = false;

        for (int i = 0; i < body.Length; i++)
        {
            char c = body[i];

            if (c == '{')
            {
                string before = output.ToString().TrimEnd();
                int kind = 0;
                if (Regex.IsMatch(before, @"\bdo$")) kind = 2;
                else if (before.EndsWith(')'))
                {
                    int depth = 0, j = before.Length - 1;
                    for (; j >= 0; j--)
                    {
                        if (before[j] == ')') depth++;
                        else if (before[j] == '(' && --depth == 0) break;
                    }

                    if (j > 0 && Regex.IsMatch(before[..j], @"\b(for|while|switch)\s*$")) kind = 1;
                }

                contexts.Push(kind);
                output.Append(c);
                continue;
            }

            if (c == '}')
            {
                int kind = contexts.Count > 0 ? contexts.Pop() : 0;
                output.Append(c);
                if (insertChecks && kind == 1) output.Append(" if (ys_returned) break;");
                if (insertChecks && kind == 2) pendingDoCheck = true;
                continue;
            }

            if (c == ';' && pendingDoCheck)
            {
                output.Append(c).Append(" if (ys_returned) break;");
                pendingDoCheck = false;
                continue;
            }

            if (c == 'r' && (i == 0 || !(char.IsLetterOrDigit(body[i - 1]) || body[i - 1] == '_')))
            {
                Match match = Return.Match(body, i);
                if (match.Success && match.Index == i)
                {
                    if (contexts.Any(k => k != 0))
                    {
                        usedFlag = true;
                        output.Append("{ ys_returned = true; break; }");
                    }
                    else
                    {
                        output.Append("break;");
                    }

                    i += match.Length - 1;
                    continue;
                }
            }

            output.Append(c);
        }

        return output.ToString();
    }

    private static string ColorStore(string local, string type, bool spatial) => (spatial, type) switch
    {
        (true, "vec4") => $"ALBEDO = {local}.rgb; ALPHA = {local}.a;",
        (true, "vec3") => $"ALBEDO = {local};",
        (true, "float") => $"ALBEDO = vec3({local});",
        (false, "vec4") => $"COLOR = {local};",
        (false, "vec3") => $"COLOR = vec4({local}, 1.0);",
        (false, "float") => $"COLOR = vec4(vec3({local}), 1.0);",
        _ => throw new NotSupportedException($"El color de salida de tipo {type} no se puede escribir en Godot.")
    };

    private static string Zero(string type) => type == "float" ? "0.0" : $"{type}(0.0)";

    private static bool IsInteger(string type) => type.StartsWith("int") || type.StartsWith("uint") || type.StartsWith("ivec")
        || type.StartsWith("uvec") || type == "bool";

    /// <summary>Declara la entrada de vertex como un local con su nombre original, inicializado desde el built-in de Godot.</summary>
    private static string InputInitializer(GlslInterface input, VertexSemantic semantic, bool spatial)
    {
        (string Builtin, string BuiltinType, float PadW)? source = (semantic.Usage, semantic.Index) switch
        {
            (VertexUsage.Position, 0) => ("VERTEX", spatial ? "vec3" : "vec2", 1f),
            (VertexUsage.Normal, 0) when spatial => ("NORMAL", "vec3", 0f),
            (VertexUsage.Tangent, 0) when spatial => ("TANGENT", "vec3", 0f),
            (VertexUsage.Binormal, 0) when spatial => ("BINORMAL", "vec3", 0f),
            (VertexUsage.Color, 0) => ("COLOR", "vec4", 1f),
            (VertexUsage.TextureCoordinate, 0) => ("UV", "vec2", 0f),
            (VertexUsage.TextureCoordinate, 1) when spatial => ("UV2", "vec2", 0f),
            (VertexUsage.TextureCoordinate, 2) => ("CUSTOM0", "vec4", 0f),
            (VertexUsage.TextureCoordinate, 3) => ("CUSTOM1", "vec4", 0f),
            (VertexUsage.TextureCoordinate, 4) when spatial => ("CUSTOM2", "vec4", 0f),
            (VertexUsage.TextureCoordinate, 5) when spatial => ("CUSTOM3", "vec4", 0f),
            (VertexUsage.BlendIndices, 0) when spatial => ("BONE_INDICES", "uvec4", 0f),
            (VertexUsage.BlendWeight, 0) when spatial => ("BONE_WEIGHTS", "vec4", 0f),
            (VertexUsage.PointSize, 0) => ("POINT_SIZE", "float", 0f),
            _ => null
        };

        if (source is null)
        {
            string mode = spatial ? "spatial" : "canvas_item";
            throw new NotSupportedException(
                $"'{input.Name}': la semántica {semantic.Usage.ToString().ToUpperInvariant()}{semantic.Index} " +
                $"no tiene entrada equivalente en un shader Godot '{mode}'.");
        }

        // POINT_SIZE: en las pruebas, leerlo en vertex() hacía que Godot no dibujara nada. Se entrega el valor por defecto (1).
        if (source.Value.Builtin == "POINT_SIZE") return $"{input.Type} {input.Name} = 1.0;";

        return $"{input.Type} {input.Name} = {Adapt(source.Value.Builtin, source.Value.BuiltinType, input.Type, source.Value.PadW)};";
    }

    private static string Adapt(string expression, string from, string to, float padW)
    {
        if (from == to) return expression;
        if (!Regex.IsMatch(to, "^(float|vec[234])$"))
        {
            throw new NotSupportedException($"No se puede convertir {from} a {to} al leer la entrada de vertex.");
        }

        int fromSize = Dimension(from), toSize = Dimension(to);
        if (toSize < fromSize)
        {
            return expression + "." + "xyzw".Substring(0, toSize);
        }

        var parts = new List<string> { expression };
        for (int i = fromSize; i < toSize; i++)
        {
            parts.Add(i == 3 ? padW.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) : "0.0");
        }

        return $"{to}({string.Join(", ", parts)})";
    }

    private static int Dimension(string type) => type == "float" ? 1 : type[^1] - '0';

    private static VertexSlot SlotOf(Function function, string glslName)
    {
        var leaves = new List<(string Full, string Member, Storage Storage)>();
        foreach (Storage input in function.Inputs)
        {
            if (input is CustomStruct structure)
            {
                foreach (Storage member in structure.Properties)
                {
                    leaves.Add((structure.HumanName + "_" + member.HumanName, member.HumanName, member));
                }
            }
            else
            {
                leaves.Add((input.HumanName, input.HumanName, input));
            }
        }

        var match = leaves.FirstOrDefault(l => l.Full == glslName);
        if (match.Storage is null) match = leaves.FirstOrDefault(l => glslName.EndsWith("_" + l.Member, StringComparison.Ordinal));
        if (match.Storage is null) match = leaves.FirstOrDefault(l => l.Member == glslName);

        // El nombre estándar del campo (VERTEX, UV...) manda sobre el número o la semántica que lleve.
        if (match.Storage is not null && ByName(match.Member) is { } named) return named;

        return match.Storage?.Slot switch
        {
            VertexSlot vertexSlot => vertexSlot,
            LocationSlot location => ByName(match.Member) ?? throw new NotSupportedException(
                $"'{glslName}': [Location({location.Location})] y el nombre '{match.Member}' no corresponden a ningún dato de vértice de Godot. " +
                "Nombres: VERTEX, NORMAL, TANGENT, BINORMAL, UV, UV2, COLOR, CUSTOM0-3, BONE_INDICES, BONE_WEIGHTS, POINT_SIZE."),
            _ => throw new NotSupportedException(
                $"No se puede determinar la semántica de la entrada de vertex '{glslName}'.")
        };
    }

    /// <summary>Convención: el nombre del campo (sin mayúsculas ni guiones bajos) dice qué dato de vértice es.</summary>
    private static VertexSlot? ByName(string member) => member.Replace("_", "").ToLowerInvariant() switch
    {
        "vertex" => new VertexSlot(new VertexSemantic(VertexUsage.Position, 0)),
        "normal" => new VertexSlot(new VertexSemantic(VertexUsage.Normal, 0)),
        "tangent" => new VertexSlot(new VertexSemantic(VertexUsage.Tangent, 0)),
        "binormal" => new VertexSlot(new VertexSemantic(VertexUsage.Binormal, 0)),
        "uv" => new VertexSlot(new VertexSemantic(VertexUsage.TextureCoordinate, 0)),
        "uv2" => new VertexSlot(new VertexSemantic(VertexUsage.TextureCoordinate, 1)),
        "color" => new VertexSlot(new VertexSemantic(VertexUsage.Color, 0)),
        "custom0" => new VertexSlot(new VertexSemantic(VertexUsage.TextureCoordinate, 2)),
        "custom1" => new VertexSlot(new VertexSemantic(VertexUsage.TextureCoordinate, 3)),
        "custom2" => new VertexSlot(new VertexSemantic(VertexUsage.TextureCoordinate, 4)),
        "custom3" => new VertexSlot(new VertexSemantic(VertexUsage.TextureCoordinate, 5)),
        "boneindices" => new VertexSlot(new VertexSemantic(VertexUsage.BlendIndices, 0)),
        "boneweights" => new VertexSlot(new VertexSemantic(VertexUsage.BlendWeight, 0)),
        "pointsize" => new VertexSlot(new VertexSemantic(VertexUsage.PointSize, 0)),
        _ => null
    };

    private static string Rename(string text, Dictionary<string, string> renames)
    {
        foreach ((string from, string to) in renames)
        {
            text = Regex.Replace(text, $@"\b{Regex.Escape(from)}\b", to);
        }

        return text;
    }

    private static string MapBuiltIns(string text, Dictionary<string, string> map, string stage)
    {
        foreach ((string from, string to) in map)
        {
            text = Regex.Replace(text, $@"\b{from}\b", to);
        }

        Match leftover = GlBuiltIn.Match(text);
        if (leftover.Success)
        {
            throw new NotSupportedException(
                $"El built-in '{leftover.Value}' no tiene equivalente en la etapa {stage} de Godot Shading Language.");
        }

        return text;
    }

    private static string Finish(string code, List<(Regex Pattern, string Replacement)> instances)
    {
        foreach ((Regex pattern, string replacement) in instances)
        {
            code = pattern.Replace(code, replacement);
        }

        code = CombinedSampler.Replace(code, "$1");
        Match sampler = Regex.Match(code, @"\bsampler\w*\s*\(");
        if (sampler.Success)
        {
            throw new NotSupportedException($"'{sampler.Value}': este uso de samplers no tiene equivalente en Godot.");
        }

        return code;
    }

    private static bool IsUsed(string code, string name) => Regex.IsMatch(code, $@"\b{Regex.Escape(name)}\b");

    private static string StripLayout(string text) =>
        Regex.Replace(text, @"layout\s*\([^)]*\)\s*", "");
}
