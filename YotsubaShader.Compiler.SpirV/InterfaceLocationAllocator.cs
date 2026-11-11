using YotsubaShader.IR.Intermediate;
using YotsubaShader.IR.Intermediate.Slot;
using YotsubaShader.IR.Intermediate.Slot.Types;
using YotsubaShader.IR.Intermediate.Types;
using Array = YotsubaShader.IR.Intermediate.Types.Array;

namespace YotsubaShader.Compiler.SpirV;

internal static class InterfaceLocationAllocator
{
    public static IReadOnlyDictionary<string, int> Allocate(Shader shader)
    {
        var result = new Dictionary<string, int>();
        var linked = new Dictionary<VertexSemantic, int>();

        var functions = shader.Functions.Where(f => f.Stage != Stage.None)
            .OrderBy(f => f.Stage == Stage.Vertex ? 0 : 1);
        
        foreach (var function in functions)
        {
            AllocateInterface(function, function.Inputs, StorageKind.Input, linked, result);
            AllocateInterface(function, function.Outputs, StorageKind.Output, linked, result);
        }

        return result;
    }

    private static void AllocateInterface(Function function, IReadOnlyList<Storage> functionInputs, StorageKind input, Dictionary<VertexSemantic, int> linked, Dictionary<string, int> result)
    {
        var leaves = functionInputs
            .Where(s => s.Kind == input)
            .SelectMany(s => BuildIrTypes.InterfaceVariables(function, s))
            .Where(l => l.Storage.Slot is LocationSlot or VertexSlot)
            .ToList();

        var seen = new HashSet<VertexSemantic>();
        foreach (var (leaf, _) in leaves)
        {
            if (leaf.Slot is VertexSlot vs && !seen.Add(vs.Semantic))
            {
                throw new InvalidOperationException(
                    $"La semántica {ToDisplay(vs.Semantic)} está repetida en los " +
                    $"{input.ToString().ToLowerInvariant()}s de '{function.HumanName}' " +
                    $"('{leaf.HumanName}'). Cada semántica debe ser única por interfaz.");
            }
        }
        
        static string ToDisplay(VertexSemantic s) => $"{s.Usage.ToString().ToUpperInvariant()}{s.Index}";
        
        var used = new Dictionary<int, string>();

        void Reserve(int location, Storage leaf)
        {
            int count = LocationCount(leaf);
            for (int i = 0; i < count; i++)
            {
                if (used.TryGetValue(location + i, out string? owner))
                {
                    throw new InvalidOperationException(
                        $"Location {location + i} duplicada en los {input.ToString().ToLowerInvariant()}s de " +
                        $"'{function.HumanName}': '{leaf.HumanName}' choca con '{owner}'. " +
                        "Usa [Location(n)] para asignarlas explícitamente.");
                }
                used[location + i] = leaf.HumanName;
            }
        }
        
        foreach (var (leaf, id) in leaves)
        {
            if (leaf.Slot is LocationSlot explicitSlot)
            {
                Reserve(explicitSlot.Location, leaf);
                result[id] = explicitSlot.Location;
            }
        }

        bool inheritsFromVertex = input == StorageKind.Input && function.Stage != Stage.Vertex;
        
        foreach (var (leaf, id) in leaves)
        {
            if (leaf.Slot is VertexSlot semantic &&
                inheritsFromVertex &&
                linked.TryGetValue(semantic.Semantic, out int inherited))
            {
                Reserve(inherited, leaf);
                result[id] = inherited;
            }
        }
        
        int next = 0;
        foreach (var (leaf, id) in leaves)
        {
            if (leaf.Slot is not VertexSlot semantic || result.ContainsKey(id))
                continue;

            int count = LocationCount(leaf);
            while (Enumerable.Range(next, count).Any(used.ContainsKey))
                next++;

            Reserve(next, leaf);
            result[id] = next;

            if (input == StorageKind.Output && function.Stage == Stage.Vertex)
                linked.TryAdd(semantic.Semantic, next);

            next += count;
        }
    }
    
    private static int LocationCount(Storage storage) => storage switch
    {
        Vector { ComponentsType: VectorComponentsType.Double, ComponentsLength: >= 3 } => 2,
        Matrix m => (int)m.Columns *
                    (m.ComponentsType == MatrixComponentType.Double && m.Rows >= 3 ? 2 : 1),
        Array a => (int)(a.Length ?? throw new InvalidOperationException(
                       $"El array de interfaz '{a.HumanName}' necesita longitud fija.")) *
                   LocationCount(a.ElementType),
        _ => 1
    };
}