using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace YotsubaShader.Rules;

/// <summary>
/// Catálogo único de diagnósticos de YotsubaShader. Lo usan el analizador del IDE y el compilador, así que un
/// shader que no da errores en el IDE no puede fallar en el compilador por la misma razón.
/// Rangos: 00xx tipos GPU, 01xx entry points, 02xx interfaz, 03xx recursos, 04xx métodos,
/// 05xx C# no soportado, 06xx uso por stage, 07xx portabilidad entre APIs.
/// </summary>
internal static class ShaderRules
{
    public const string Category = "YotsubaShader";
    public const string PortabilityCategory = "YotsubaShader.Portability";
    private const string HelpBase = "https://github.com/KMemphis/YotsubaShader/blob/main/docs/rules/";

    private static DiagnosticDescriptor Error(string id, string title, string message, params string[] tags) =>
        new(id, Loc.Text(title), Loc.Text(message), Category, DiagnosticSeverity.Error, isEnabledByDefault: true,
            helpLinkUri: HelpBase + id + ".md", customTags: tags);

    private static DiagnosticDescriptor Warning(string id, string title, string message, params string[] tags) =>
        new(id, Loc.Text(title), Loc.Text(message), Category, DiagnosticSeverity.Warning, isEnabledByDefault: true,
            helpLinkUri: HelpBase + id + ".md", customTags: tags);

    private static DiagnosticDescriptor Portability(string id, string title, string message) =>
        new(id, Loc.Text(title), Loc.Text(message), PortabilityCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true,
            helpLinkUri: HelpBase + id + ".md");

    private const string End = WellKnownDiagnosticTags.CompilationEnd;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DiagnosticDescriptor> ErrorVariants = new();

    /// <summary>
    /// El mismo descriptor (mismo id) con severidad de error. Las reglas de compatibilidad con una API avisan, pero dan
    /// error cuando el shader declara con <c>[ShaderTargets]</c> una API que no tiene lo que usa.
    /// </summary>
    public static DiagnosticDescriptor AsError(DiagnosticDescriptor descriptor) =>
        descriptor.DefaultSeverity == DiagnosticSeverity.Error
            ? descriptor
            : ErrorVariants.GetOrAdd(descriptor.Id, _ => new DiagnosticDescriptor(descriptor.Id, descriptor.Title, descriptor.MessageFormat,
                descriptor.Category, DiagnosticSeverity.Error, descriptor.IsEnabledByDefault, descriptor.Description,
                descriptor.HelpLinkUri, descriptor.CustomTags.ToArray()));

    // ── 00xx Tipos GPU ─────────────────────────────────────────────────────────────────────────────
    public static readonly DiagnosticDescriptor VectorComponentCount = Error("YS0001", "Invalid component count",
        "The vector '{0}' has {1} components; a GPU vector has between 2 and 4. Adjust the [VectorComponent] fields.");
    public static readonly DiagnosticDescriptor VectorNegativeIndex = Error("YS0002", "Negative component index",
        "The component '{0}' uses index {1}; indices start at 0.");
    public static readonly DiagnosticDescriptor VectorDuplicateIndex = Error("YS0003", "Duplicate component index",
        "Index {0} is repeated in the vector '{1}'. Each component needs a distinct index.");
    public static readonly DiagnosticDescriptor VectorMissingIndex = Error("YS0004", "Missing component index",
        "The vector '{0}' is missing component {1}; indices must be consecutive starting at 0.");
    public static readonly DiagnosticDescriptor VectorMixedTypes = Error("YS0005", "Components with different types",
        "The components of the vector '{0}' have different types ({1} and {2}). Use the same type for all of them.");
    public static readonly DiagnosticDescriptor VectorComponentType = Error("YS0006", "Unsupported component type",
        "The component '{0}' is of type '{1}'. Vectors support bool, short, ushort, int, uint, Half, float and double.");
    public static readonly DiagnosticDescriptor VectorMemberWithoutComponent = Warning("YS0007", "Vector member without [VectorComponent]",
        "'{0}' is not a component of the vector '{1}' and does not exist on the GPU. Mark it with [VectorComponent] or remove it.");
    public static readonly DiagnosticDescriptor VectorComponentIgnored = Warning("YS0008", "[VectorComponent] ignored",
        "[VectorComponent] on '{0}' is ignored: a component must be an instance field or property.");
    public static readonly DiagnosticDescriptor VectorComponentOutsideVector = Warning("YS0009", "[VectorComponent] outside a vector",
        "'{0}' has [VectorComponent] but the type '{1}' is not marked with [Vector].");
    public static readonly DiagnosticDescriptor VectorNonConstantInitializer = Warning("YS0010", "Non-constant initial value",
        "The initial value of '{0}' is not constant; on the GPU the component starts at 0.");
    public static readonly DiagnosticDescriptor UnsupportedType = Error("YS0011", "Type without a GPU representation",
        "The type '{0}' does not exist on the GPU ({1}). Use scalars, vectors, matrices, arrays or structs of those types.");

    // ── 01xx Puntos de entrada ─────────────────────────────────────────────────────────────────────
    public static readonly DiagnosticDescriptor EntryPointKind = Error("YS0100", "Invalid entry point",
        "'{0}' cannot be an entry point: {1}.");
    public static readonly DiagnosticDescriptor ComputeWithoutWorkgroupSize = Error("YS0101", "Compute shader without a group size",
        "The compute shader '{0}' needs a group size: add [numthreads(x, y, z)], [layout_local_size(...)] or [workgroup_size(...)].");
    public static readonly DiagnosticDescriptor WorkgroupSizeZero = Error("YS0102", "Invalid group size",
        "Dimension {0} of the group size of '{1}' is {2}; each dimension must be at least 1.");
    public static readonly DiagnosticDescriptor EntryPointCalled = Error("YS0103", "Direct call to an entry point",
        "'{0}' is an entry point and cannot be called from shader code. Move the logic to a helper function.");
    public static readonly DiagnosticDescriptor WorkgroupSizeTooLarge = Error("YS0104", "Group size too large",
        "The group size {0}x{1}x{2} of '{3}' exceeds the GPU maximum ({4}).");
    public static readonly DiagnosticDescriptor WorkgroupSizeIgnored = Warning("YS0105", "Group size ignored",
        "The group size only has an effect in compute shaders; '{0}' is a {1} shader.");
    public static readonly DiagnosticDescriptor MultipleStages = Error("YS0106", "Several stages in one method",
        "'{0}' has more than one [ShaderStage]. An entry point belongs to a single stage.");
    public static readonly DiagnosticDescriptor EntryPointParameterRef = Error("YS0107", "Reference parameter in an entry point",
        "The parameter '{0}' of '{1}' is '{2}'. An entry point only accepts normal parameters (inputs) and 'out' parameters (outputs).");
    public static readonly DiagnosticDescriptor EntryPointOverloaded = Error("YS0108", "Duplicate entry point",
        "There are several entry points named '{0}' in '{1}'. The name of an entry point must be unique.");

    // ── 02xx Interfaz entre etapas ─────────────────────────────────────────────────────────────────
    public static readonly DiagnosticDescriptor BuiltInWrongStage = Error("YS0201", "Built-in not valid in this stage",
        "[{0}] ({1}) does not exist in a {2} shader. It is used in: {3}.");
    public static readonly DiagnosticDescriptor BuiltInWrongDirection = Error("YS0202", "Built-in in the wrong direction",
        "[{0}] ({1}) is {2} in a {3} shader, not {4}.");
    public static readonly DiagnosticDescriptor BuiltInWrongType = Error("YS0203", "Wrong type for the built-in",
        "[{0}] ({1}) must be {2}, but '{3}' is '{4}'.");
    public static readonly DiagnosticDescriptor BuiltInDuplicate = Error("YS0204", "Duplicate built-in",
        "{0} appears more than once as {1} of '{2}'. Each built-in is declared only once per stage.");
    public static readonly DiagnosticDescriptor LocationOverlap = Error("YS0205", "Duplicate location",
        "Location {0} of '{1}' overlaps with '{2}' in the {3} of '{4}'.");
    public static readonly DiagnosticDescriptor SlotConflict = Error("YS0206", "Conflicting interface attributes",
        "'{0}' has both [{1}] and [{2}]. An interface member carries a single location, semantic or built-in.");
    public static readonly DiagnosticDescriptor MissingSlot = Error("YS0207", "Interface member without a slot",
        "'{0}' is {1} of '{2}' and has no location, semantic or built-in. Add for example [TEXCOORD(n)], [location(n)] or a built-in.");
    public static readonly DiagnosticDescriptor BoolInInterface = Error("YS0208", "bool in the interface",
        "'{0}' is bool; variables passed between stages cannot be bool on the GPU. Use uint or int.");
    public static readonly DiagnosticDescriptor LocationInCompute = Error("YS0209", "Location in a compute shader",
        "'{0}' has [{1}], but a compute shader has no location inputs or outputs; only built-ins such as SV_DispatchThreadID.");
    public static readonly DiagnosticDescriptor RenderTargetRange = Error("YS0210", "Render target out of range",
        "The render target {0} of '{1}' does not exist; valid indices are 0 to 7.");
    public static readonly DiagnosticDescriptor NegativeSlotIndex = Error("YS0211", "Negative location index",
        "[{0}] on '{1}' uses index {2}; it must be 0 or greater.");
    public static readonly DiagnosticDescriptor SemanticInFragmentOutput = Error("YS0212", "Semantic in a fragment output",
        "'{0}' uses [{1}] as the output of a fragment shader. Fragment outputs are render targets: use [SV_Target(n)] or [location(n)].");
    public static readonly DiagnosticDescriptor InterfaceArrayLength = Error("YS0213", "Interface array without a length",
        "'{0}' is an array in the interface and its length is unknown. Specify the length with [ArrayLength(n)].");
    public static readonly DiagnosticDescriptor ResourceInInterface = Error("YS0214", "Resource in the interface",
        "'{0}' is {1} and cannot be passed between stages. Declare it as a shader field with [Binding].");
    public static readonly DiagnosticDescriptor FragmentInputWithoutVertexOutput = Error("YS0215", "Fragment input without a vertex output",
        "The input '{0}' ({1}) of '{2}' is not written by any output of the vertex shader '{3}'.", End);
    public static readonly DiagnosticDescriptor InterfaceTypeMismatch = Error("YS0216", "Different types between stages",
        "'{0}' is '{1}' in the vertex shader '{2}' and '{3}' in the fragment shader '{4}'.", End);
    public static readonly DiagnosticDescriptor DescriptorOnInterface = Error("YS0217", "Descriptor in the interface",
        "'{0}' has [{1}], which is for resources (textures, samplers, buffers), not for inputs or outputs.");
    public static readonly DiagnosticDescriptor InterfaceUnsupportedType = Error("YS0218", "Type not valid in the interface",
        "'{0}' is '{1}', which cannot be passed between stages. Use scalars, vectors, matrices or arrays of them.");
    public static readonly DiagnosticDescriptor BuiltInNotSupported = Error("YS0219", "Built-in not supported yet",
        "[{0}] ({1}) is not supported by the YotsubaShader compiler yet.");
    public static readonly DiagnosticDescriptor NestedInterfaceStruct = Error("YS0220", "Nested interface struct",
        "'{0}' is a struct inside an interface struct. Declare its fields directly in '{1}'.");
    public static readonly DiagnosticDescriptor VertexWithoutPosition = Warning("YS0221", "Vertex shader without a position",
        "The vertex shader '{0}' does not write SV_Position; the GPU will not be able to rasterize its primitives.");

    // ── 03xx Recursos ──────────────────────────────────────────────────────────────────────────────
    public static readonly DiagnosticDescriptor ResourceWithoutBinding = Error("YS0301", "Resource without a binding",
        "'{0}' is {1} and has no binding. Add [Binding(n)] (and [DescriptorSet(n)]), [register(...)] or [layout(set, binding)].");
    public static readonly DiagnosticDescriptor DuplicateBinding = Error("YS0302", "Duplicate binding",
        "'{0}' and '{1}' use the same set {2} and binding {3}.");
    public static readonly DiagnosticDescriptor NegativeBinding = Error("YS0303", "Negative binding",
        "'{0}' uses {1} {2}; it must be 0 or greater.");
    public static readonly DiagnosticDescriptor RegisterKindMismatch = Error("YS0304", "HLSL register of another kind",
        "'{0}' is {1} and uses the register '{2}'; the prefix for this resource is '{3}'.");
    public static readonly DiagnosticDescriptor BufferNotStruct = Error("YS0305", "Buffer that is not a struct",
        "'{0}' is a buffer and its type '{1}' is not a struct. Wrap the data in a struct.");
    public static readonly DiagnosticDescriptor ResourceDeclaration = Error("YS0306", "Badly declared resource",
        "'{0}' {1}. Declare resources as instance fields of the shader.");
    public static readonly DiagnosticDescriptor BufferMemberType = Error("YS0307", "Type not valid in a buffer",
        "'{0}' in the buffer '{1}' is '{2}': {3}.");
    public static readonly DiagnosticDescriptor ReadOnlyBufferWrite = Error("YS0308", "Write to a read-only buffer",
        "'{0}' is a read-only buffer and cannot be written.");
    public static readonly DiagnosticDescriptor LocationOnResource = Error("YS0309", "Location on a resource",
        "'{0}' is a resource and has [{1}]. Resources are bound with [Binding]/[DescriptorSet], not with locations or semantics.");
    public static readonly DiagnosticDescriptor PlainFieldInShader = Error("YS0310", "Field with no meaning on the GPU",
        "The field '{0}' is not a resource. To pass values from the CPU, group them in a struct with [Uniform] and [Binding].");
    public static readonly DiagnosticDescriptor RuntimeArrayNotLast = Error("YS0311", "Unsized array in the middle of a buffer",
        "'{0}' is an unsized array and is not the last member of '{1}'. Only the last member of a storage buffer can be unsized.");

    // ── 04xx Métodos ───────────────────────────────────────────────────────────────────────────────
    public static readonly DiagnosticDescriptor GenericMethod = Error("YS0401", "Generic method in a shader",
        "'{0}' is generic; shaders have no generics. Write one version per type.");
    public static readonly DiagnosticDescriptor RefParameter = Error("YS0402", "ref, in or out parameter in a helper function",
        "The parameter '{0}' of '{1}' is '{2}'; helper functions do not support reference parameters yet. Return the value instead.");
    public static readonly DiagnosticDescriptor ParamsParameter = Error("YS0403", "params parameter in a shader",
        "The parameter '{0}' of '{1}' is params; use a fixed-length array.");
    public static readonly DiagnosticDescriptor Recursion = Error("YS0404", "Recursion",
        "'{0}' is recursive ({1}). The GPU does not support recursion; rewrite it with a loop.", End);
    public static readonly DiagnosticDescriptor VirtualCall = Error("YS0405", "Virtual call",
        "'{0}' is {1}; on the GPU there are only direct calls to concrete methods.");
    public static readonly DiagnosticDescriptor NoGpuTranslation = Error("YS0406", "Method without a GPU translation",
        "'{0}' has no GPU translation. Use a YotsubaShader intrinsic or write the function in the shader.");
    public static readonly DiagnosticDescriptor AsyncOrIterator = Error("YS0407", "Async method or iterator",
        "'{0}' is {1}; shaders have no async or iterators.");

    // ── 05xx C# no soportado ───────────────────────────────────────────────────────────────────────
    public static readonly DiagnosticDescriptor UnsupportedStatement = Error("YS0501", "Unsupported statement",
        "'{0}' does not exist on the GPU. {1}");
    public static readonly DiagnosticDescriptor UnsupportedExpression = Error("YS0502", "Unsupported expression",
        "'{0}' has no GPU translation. {1}");
    public static readonly DiagnosticDescriptor UnsupportedTypeUse = Error("YS0503", "Type not supported in the shader",
        "'{0}' uses the type '{1}', which does not exist on the GPU.");
    public static readonly DiagnosticDescriptor UnusedObjectCreation = Error("YS0504", "Unused object creation",
        "The 'new {0}(...)' is not used; it has no effect on the GPU. Assign it or remove it.");
    public static readonly DiagnosticDescriptor MutableStaticField = Error("YS0505", "Mutable static field",
        "'{0}' is a static field that is not constant. The GPU has no mutable global state; use 'const' or a resource.");
    public static readonly DiagnosticDescriptor SwitchNotExhaustive = Error("YS0506", "switch without a default case",
        "The switch expression does not cover all values. Add a '_ => ...' arm.");
    public static readonly DiagnosticDescriptor UnsupportedArray = Error("YS0507", "Unsupported array",
        "{0}. On the GPU arrays have one dimension and a constant length.");
    public static readonly DiagnosticDescriptor UnsupportedIntrinsic = Error("YS0508", "Intrinsic not supported with these arguments",
        "'{0}' {1}.");

    // ── 06xx Uso por stage ─────────────────────────────────────────────────────────────────────────
    public static readonly DiagnosticDescriptor FragmentOnly = Error("YS0601", "Fragment-only operation",
        "'{0}' {1} only exists in fragment shaders, and it is used from the {2} shader '{3}'.", End);
    public static readonly DiagnosticDescriptor ComputeOnly = Error("YS0602", "Compute-only operation",
        "'{0}' only exists in compute shaders, and it is used from the {1} shader '{2}'.", End);
    public static readonly DiagnosticDescriptor WriteToInput = Error("YS0603", "Write to an input",
        "'{0}' is a shader input and cannot be modified. Copy it to a local variable.");
    public static readonly DiagnosticDescriptor AtomicTarget = Error("YS0604", "Atomic on a local variable",
        "The atomic operation '{0}' needs a storage buffer field as its target.");

    // ── 07xx Portabilidad ──────────────────────────────────────────────────────────────────────────
    public static readonly DiagnosticDescriptor PortableType = Portability("YS0701", "Non-portable type",
        "'{0}' is not available in all graphics APIs: {1}. Affects: {2}.");
    public static readonly DiagnosticDescriptor PortableBuiltIn = Portability("YS0702", "Non-portable built-in",
        "[{0}] ({1}) {2}. Affects: {3}.");
    public static readonly DiagnosticDescriptor PortableIntrinsic = Portability("YS0703", "Non-portable intrinsic",
        "'{0}' {1}. Affects: {2}.");
    public static readonly DiagnosticDescriptor PortableLimit = Portability("YS0704", "Non-portable hardware limit",
        "{0} Affects: {1}.");
    public static readonly DiagnosticDescriptor PortableResource = Portability("YS0705", "Non-portable resource",
        "'{0}' {1}. Affects: {2}.");
    public static readonly DiagnosticDescriptor PortableLayout = Portability("YS0706", "Buffer layout differs between APIs",
        "'{0}' in '{1}' {2}. Affects: {3}.");
    public static readonly DiagnosticDescriptor NonUniformDerivative = Portability("YS0707", "Derivative operation in non-uniform control flow",
        "'{0}' is inside a {1} whose condition depends on a shader input. It is an error in WGSL and gives undefined results in the other APIs. Affects: {2}.");
    public static readonly DiagnosticDescriptor FloatSpecialValues = Portability("YS0708", "NaN and infinity not reliable",
        "'{0}' may always return false with the floating-point optimizations of {1}.");
    public static readonly DiagnosticDescriptor IntegerDivisionByZero = Warning("YS0709", "Integer division by zero",
        "Integer division by zero gives an undefined result on the GPU (in C# it throws an exception).");
    public static readonly DiagnosticDescriptor FeatureUnavailable = Portability("YS0711", "Feature not available in the declared API",
        "{0} {1}. Affects: {2}.");
    public static readonly DiagnosticDescriptor TargetWithoutBackend = Error("YS0712", "Graphics API version without a backend",
        "{0}");
    public static readonly DiagnosticDescriptor UniformBufferTooLarge = Portability("YS0713", "Uniform buffer too large",
        "The uniform buffer '{0}' takes {1} bytes (std140), more than what is guaranteed by {2}. Affects: {3}.");
    public static readonly DiagnosticDescriptor NotInGodot = Portability("YS0710", "Not available in Godot",
        "'{0}' {1}. Affects: Godot.");

    /// <summary>Todos los descriptores, para <c>SupportedDiagnostics</c>.</summary>
    public static IReadOnlyList<DiagnosticDescriptor> All { get; } = typeof(ShaderRules)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(DiagnosticDescriptor))
        .Select(f => (DiagnosticDescriptor)f.GetValue(null)!)
        .ToList();
}
