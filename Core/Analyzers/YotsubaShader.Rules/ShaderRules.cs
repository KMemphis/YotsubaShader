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
        new(id, title, message, Category, DiagnosticSeverity.Error, isEnabledByDefault: true,
            helpLinkUri: HelpBase + id + ".md", customTags: tags);

    private static DiagnosticDescriptor Warning(string id, string title, string message, params string[] tags) =>
        new(id, title, message, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true,
            helpLinkUri: HelpBase + id + ".md", customTags: tags);

    private static DiagnosticDescriptor Portability(string id, string title, string message) =>
        new(id, title, message, PortabilityCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true,
            helpLinkUri: HelpBase + id + ".md");

    private const string End = WellKnownDiagnosticTags.CompilationEnd;

    // ── 00xx Tipos GPU ─────────────────────────────────────────────────────────────────────────────
    public static readonly DiagnosticDescriptor VectorComponentCount = Error("YS0001", "Número de componentes inválido",
        "El vector '{0}' tiene {1} componentes; un vector de GPU tiene entre 2 y 4. Ajusta los campos [VectorComponent].");
    public static readonly DiagnosticDescriptor VectorNegativeIndex = Error("YS0002", "Índice de componente negativo",
        "El componente '{0}' usa el índice {1}; los índices empiezan en 0.");
    public static readonly DiagnosticDescriptor VectorDuplicateIndex = Error("YS0003", "Índice de componente repetido",
        "El índice {0} está repetido en el vector '{1}'. Cada componente necesita un índice distinto.");
    public static readonly DiagnosticDescriptor VectorMissingIndex = Error("YS0004", "Falta un índice de componente",
        "Al vector '{0}' le falta el componente {1}; los índices deben ser consecutivos desde 0.");
    public static readonly DiagnosticDescriptor VectorMixedTypes = Error("YS0005", "Componentes con tipos distintos",
        "Los componentes del vector '{0}' tienen tipos distintos ({1} y {2}). Usa el mismo tipo en todos.");
    public static readonly DiagnosticDescriptor VectorComponentType = Error("YS0006", "Tipo de componente no soportado",
        "El componente '{0}' es de tipo '{1}'. Los vectores admiten bool, short, ushort, int, uint, Half, float y double.");
    public static readonly DiagnosticDescriptor VectorMemberWithoutComponent = Warning("YS0007", "Miembro del vector sin [VectorComponent]",
        "'{0}' no es un componente del vector '{1}' y no existe en la GPU. Márcalo con [VectorComponent] o quítalo.");
    public static readonly DiagnosticDescriptor VectorComponentIgnored = Warning("YS0008", "[VectorComponent] ignorado",
        "[VectorComponent] en '{0}' se ignora: un componente debe ser un campo o propiedad de instancia.");
    public static readonly DiagnosticDescriptor VectorComponentOutsideVector = Warning("YS0009", "[VectorComponent] fuera de un vector",
        "'{0}' tiene [VectorComponent] pero el tipo '{1}' no está marcado con [Vector].");
    public static readonly DiagnosticDescriptor VectorNonConstantInitializer = Warning("YS0010", "Valor inicial no constante",
        "El valor inicial de '{0}' no es constante; en la GPU el componente empieza en 0.");
    public static readonly DiagnosticDescriptor UnsupportedType = Error("YS0011", "Tipo sin representación en la GPU",
        "El tipo '{0}' no existe en la GPU ({1}). Usa escalares, vectores, matrices, arrays o structs de esos tipos.");

    // ── 01xx Puntos de entrada ─────────────────────────────────────────────────────────────────────
    public static readonly DiagnosticDescriptor EntryPointKind = Error("YS0100", "Punto de entrada no válido",
        "'{0}' no puede ser un punto de entrada: {1}.");
    public static readonly DiagnosticDescriptor ComputeWithoutWorkgroupSize = Error("YS0101", "Compute shader sin tamaño de grupo",
        "El compute shader '{0}' necesita un tamaño de grupo: añade [numthreads(x, y, z)], [layout_local_size(...)] o [workgroup_size(...)].");
    public static readonly DiagnosticDescriptor WorkgroupSizeZero = Error("YS0102", "Tamaño de grupo inválido",
        "La dimensión {0} del tamaño de grupo de '{1}' es {2}; cada dimensión debe ser al menos 1.");
    public static readonly DiagnosticDescriptor EntryPointCalled = Error("YS0103", "Llamada directa a un punto de entrada",
        "'{0}' es un punto de entrada y no puede llamarse desde código de shader. Mueve la lógica a una función auxiliar.");
    public static readonly DiagnosticDescriptor WorkgroupSizeTooLarge = Error("YS0104", "Tamaño de grupo demasiado grande",
        "El tamaño de grupo {0}x{1}x{2} de '{3}' supera el máximo de la GPU ({4}).");
    public static readonly DiagnosticDescriptor WorkgroupSizeIgnored = Warning("YS0105", "Tamaño de grupo ignorado",
        "El tamaño de grupo solo tiene efecto en compute shaders; '{0}' es un shader {1}.");
    public static readonly DiagnosticDescriptor MultipleStages = Error("YS0106", "Varias etapas en un método",
        "'{0}' tiene más de un [ShaderStage]. Un punto de entrada pertenece a una sola etapa.");
    public static readonly DiagnosticDescriptor EntryPointParameterRef = Error("YS0107", "Parámetro por referencia en un punto de entrada",
        "El parámetro '{0}' de '{1}' es '{2}'. En un punto de entrada solo valen parámetros normales (entradas) y 'out' (salidas).");
    public static readonly DiagnosticDescriptor EntryPointOverloaded = Error("YS0108", "Punto de entrada duplicado",
        "Hay varios puntos de entrada llamados '{0}' en '{1}'. El nombre del punto de entrada debe ser único.");

    // ── 02xx Interfaz entre etapas ─────────────────────────────────────────────────────────────────
    public static readonly DiagnosticDescriptor BuiltInWrongStage = Error("YS0201", "Built-in no válido en esta etapa",
        "[{0}] ({1}) no existe en un shader {2}. Se usa en: {3}.");
    public static readonly DiagnosticDescriptor BuiltInWrongDirection = Error("YS0202", "Built-in en el sentido equivocado",
        "[{0}] ({1}) es {2} en un shader {3}, no {4}.");
    public static readonly DiagnosticDescriptor BuiltInWrongType = Error("YS0203", "Tipo incorrecto para el built-in",
        "[{0}] ({1}) debe ser {2}, pero '{3}' es '{4}'.");
    public static readonly DiagnosticDescriptor BuiltInDuplicate = Error("YS0204", "Built-in repetido",
        "{0} aparece más de una vez como {1} de '{2}'. Cada built-in se declara una sola vez por etapa.");
    public static readonly DiagnosticDescriptor LocationOverlap = Error("YS0205", "Location duplicada",
        "La location {0} de '{1}' se solapa con '{2}' en las {3} de '{4}'.");
    public static readonly DiagnosticDescriptor SlotConflict = Error("YS0206", "Atributos de interfaz en conflicto",
        "'{0}' tiene a la vez [{1}] y [{2}]. Un miembro de la interfaz lleva una sola location, semántica o built-in.");
    public static readonly DiagnosticDescriptor MissingSlot = Error("YS0207", "Miembro de la interfaz sin slot",
        "'{0}' es {1} de '{2}' y no tiene location, semántica ni built-in. Añade por ejemplo [TEXCOORD(n)], [location(n)] o un built-in.");
    public static readonly DiagnosticDescriptor BoolInInterface = Error("YS0208", "bool en la interfaz",
        "'{0}' es bool; las variables entre etapas no pueden ser bool en la GPU. Usa uint o int.");
    public static readonly DiagnosticDescriptor LocationInCompute = Error("YS0209", "Location en un compute shader",
        "'{0}' tiene [{1}], pero un compute shader no tiene entradas ni salidas por location; solo built-ins como SV_DispatchThreadID.");
    public static readonly DiagnosticDescriptor RenderTargetRange = Error("YS0210", "Render target fuera de rango",
        "El render target {0} de '{1}' no existe; los índices válidos son 0 a 7.");
    public static readonly DiagnosticDescriptor NegativeSlotIndex = Error("YS0211", "Índice de location negativo",
        "[{0}] en '{1}' usa el índice {2}; debe ser 0 o mayor.");
    public static readonly DiagnosticDescriptor SemanticInFragmentOutput = Error("YS0212", "Semántica en una salida de fragment",
        "'{0}' usa [{1}] como salida de un fragment shader. Las salidas de fragment son render targets: usa [SV_Target(n)] o [location(n)].");
    public static readonly DiagnosticDescriptor InterfaceArrayLength = Error("YS0213", "Array de interfaz sin longitud",
        "'{0}' es un array en la interfaz y su longitud no se conoce. Indica la longitud con [ArrayLength(n)].");
    public static readonly DiagnosticDescriptor ResourceInInterface = Error("YS0214", "Recurso en la interfaz",
        "'{0}' es {1} y no puede pasar entre etapas. Decláralo como campo del shader con [Binding].");
    public static readonly DiagnosticDescriptor FragmentInputWithoutVertexOutput = Error("YS0215", "Entrada de fragment sin salida de vertex",
        "La entrada '{0}' ({1}) de '{2}' no la escribe ninguna salida del vertex shader '{3}'.", End);
    public static readonly DiagnosticDescriptor InterfaceTypeMismatch = Error("YS0216", "Tipos distintos entre etapas",
        "'{0}' es '{1}' en el vertex shader '{2}' y '{3}' en el fragment shader '{4}'.", End);
    public static readonly DiagnosticDescriptor DescriptorOnInterface = Error("YS0217", "Descriptor en la interfaz",
        "'{0}' tiene [{1}], que es para recursos (texturas, samplers, buffers), no para entradas o salidas.");
    public static readonly DiagnosticDescriptor InterfaceUnsupportedType = Error("YS0218", "Tipo no válido en la interfaz",
        "'{0}' es '{1}', que no puede pasar entre etapas. Usa escalares, vectores, matrices o arrays de ellos.");
    public static readonly DiagnosticDescriptor BuiltInNotSupported = Error("YS0219", "Built-in no soportado todavía",
        "[{0}] ({1}) todavía no está soportado por el compilador de YotsubaShader.");
    public static readonly DiagnosticDescriptor NestedInterfaceStruct = Error("YS0220", "Struct de interfaz anidado",
        "'{0}' es un struct dentro de un struct de interfaz. Declara sus campos directamente en '{1}'.");
    public static readonly DiagnosticDescriptor VertexWithoutPosition = Warning("YS0221", "Vertex shader sin posición",
        "El vertex shader '{0}' no escribe SV_Position; la GPU no podrá rasterizar sus primitivas.");

    // ── 03xx Recursos ──────────────────────────────────────────────────────────────────────────────
    public static readonly DiagnosticDescriptor ResourceWithoutBinding = Error("YS0301", "Recurso sin binding",
        "'{0}' es {1} y no tiene binding. Añade [Binding(n)] (y [DescriptorSet(n)]), [register(...)] o [layout(set, binding)].");
    public static readonly DiagnosticDescriptor DuplicateBinding = Error("YS0302", "Binding repetido",
        "'{0}' y '{1}' usan el mismo set {2} y binding {3}.");
    public static readonly DiagnosticDescriptor NegativeBinding = Error("YS0303", "Binding negativo",
        "'{0}' usa {1} {2}; debe ser 0 o mayor.");
    public static readonly DiagnosticDescriptor RegisterKindMismatch = Error("YS0304", "Registro HLSL de otro tipo",
        "'{0}' es {1} y usa el registro '{2}'; para este recurso el prefijo es '{3}'.");
    public static readonly DiagnosticDescriptor BufferNotStruct = Error("YS0305", "Buffer que no es struct",
        "'{0}' es un buffer y su tipo '{1}' no es un struct. Envuelve los datos en un struct.");
    public static readonly DiagnosticDescriptor ResourceDeclaration = Error("YS0306", "Recurso declarado mal",
        "'{0}' {1}. Declara los recursos como campos de instancia del shader.");
    public static readonly DiagnosticDescriptor BufferMemberType = Error("YS0307", "Tipo no válido en un buffer",
        "'{0}' en el buffer '{1}' es '{2}': {3}.");
    public static readonly DiagnosticDescriptor ReadOnlyBufferWrite = Error("YS0308", "Escritura en un buffer de solo lectura",
        "'{0}' es un buffer de solo lectura y no se puede escribir.");
    public static readonly DiagnosticDescriptor LocationOnResource = Error("YS0309", "Location en un recurso",
        "'{0}' es un recurso y tiene [{1}]. Los recursos se enlazan con [Binding]/[DescriptorSet], no con locations ni semánticas.");
    public static readonly DiagnosticDescriptor PlainFieldInShader = Error("YS0310", "Campo sin significado en la GPU",
        "El campo '{0}' no es un recurso. Para pasar valores desde la CPU agrúpalos en un struct con [Uniform] y [Binding].");
    public static readonly DiagnosticDescriptor RuntimeArrayNotLast = Error("YS0311", "Array sin tamaño en mitad del buffer",
        "'{0}' es un array sin tamaño y no es el último miembro de '{1}'. Solo el último miembro de un storage buffer puede no tener tamaño.");

    // ── 04xx Métodos ───────────────────────────────────────────────────────────────────────────────
    public static readonly DiagnosticDescriptor GenericMethod = Error("YS0401", "Método genérico en un shader",
        "'{0}' es genérico; los shaders no tienen genéricos. Escribe una versión por tipo.");
    public static readonly DiagnosticDescriptor RefParameter = Error("YS0402", "Parámetro ref, in u out en una función auxiliar",
        "El parámetro '{0}' de '{1}' es '{2}'; las funciones auxiliares todavía no admiten parámetros por referencia. Devuelve el valor.");
    public static readonly DiagnosticDescriptor ParamsParameter = Error("YS0403", "Parámetro params en un shader",
        "El parámetro '{0}' de '{1}' es params; usa un array de longitud fija.");
    public static readonly DiagnosticDescriptor Recursion = Error("YS0404", "Recursión",
        "'{0}' es recursiva ({1}). La GPU no admite recursión; reescríbela con un bucle.", End);
    public static readonly DiagnosticDescriptor VirtualCall = Error("YS0405", "Llamada virtual",
        "'{0}' es {1}; en la GPU solo hay llamadas directas a métodos concretos.");
    public static readonly DiagnosticDescriptor NoGpuTranslation = Error("YS0406", "Método sin traducción a la GPU",
        "'{0}' no tiene traducción a la GPU. Usa una intrínseca de YotsubaShader o escribe la función en el shader.");
    public static readonly DiagnosticDescriptor AsyncOrIterator = Error("YS0407", "Método async o iterador",
        "'{0}' es {1}; los shaders no tienen async ni iteradores.");

    // ── 05xx C# no soportado ───────────────────────────────────────────────────────────────────────
    public static readonly DiagnosticDescriptor UnsupportedStatement = Error("YS0501", "Sentencia no soportada",
        "'{0}' no existe en la GPU. {1}");
    public static readonly DiagnosticDescriptor UnsupportedExpression = Error("YS0502", "Expresión no soportada",
        "'{0}' no tiene traducción a la GPU. {1}");
    public static readonly DiagnosticDescriptor UnsupportedTypeUse = Error("YS0503", "Tipo no soportado en el shader",
        "'{0}' usa el tipo '{1}', que no existe en la GPU.");
    public static readonly DiagnosticDescriptor UnusedObjectCreation = Error("YS0504", "Creación de objeto sin usar",
        "El 'new {0}(...)' no se usa; en la GPU no tiene efecto. Asígnalo o quítalo.");
    public static readonly DiagnosticDescriptor MutableStaticField = Error("YS0505", "Campo estático mutable",
        "'{0}' es un campo estático que no es constante. La GPU no tiene estado global mutable; usa 'const' o un recurso.");
    public static readonly DiagnosticDescriptor SwitchNotExhaustive = Error("YS0506", "switch sin caso por defecto",
        "La expresión switch no cubre todos los valores. Añade un brazo '_ => ...'.");
    public static readonly DiagnosticDescriptor UnsupportedArray = Error("YS0507", "Array no soportado",
        "{0}. En la GPU los arrays tienen una dimensión y una longitud constante.");
    public static readonly DiagnosticDescriptor UnsupportedIntrinsic = Error("YS0508", "Intrínseca no soportada con estos argumentos",
        "'{0}' {1}.");

    // ── 06xx Uso por stage ─────────────────────────────────────────────────────────────────────────
    public static readonly DiagnosticDescriptor FragmentOnly = Error("YS0601", "Operación solo de fragment",
        "'{0}' {1} solo existe en fragment shaders, y se usa desde el shader {2} '{3}'.", End);
    public static readonly DiagnosticDescriptor ComputeOnly = Error("YS0602", "Operación solo de compute",
        "'{0}' solo existe en compute shaders, y se usa desde el shader {1} '{2}'.", End);
    public static readonly DiagnosticDescriptor WriteToInput = Error("YS0603", "Escritura en una entrada",
        "'{0}' es una entrada del shader y no se puede modificar. Cópiala a una variable local.");
    public static readonly DiagnosticDescriptor AtomicTarget = Error("YS0604", "Atómica sobre una variable local",
        "La operación atómica '{0}' necesita un campo de un storage buffer como destino.");

    // ── 07xx Portabilidad ──────────────────────────────────────────────────────────────────────────
    public static readonly DiagnosticDescriptor PortableType = Portability("YS0701", "Tipo no portable",
        "'{0}' no está disponible en todas las APIs: {1}. Afecta a: {2}.");
    public static readonly DiagnosticDescriptor PortableBuiltIn = Portability("YS0702", "Built-in no portable",
        "[{0}] ({1}) {2}. Afecta a: {3}.");
    public static readonly DiagnosticDescriptor PortableIntrinsic = Portability("YS0703", "Intrínseca no portable",
        "'{0}' {1}. Afecta a: {2}.");
    public static readonly DiagnosticDescriptor PortableLimit = Portability("YS0704", "Límite de hardware no portable",
        "{0} Afecta a: {1}.");
    public static readonly DiagnosticDescriptor PortableResource = Portability("YS0705", "Recurso no portable",
        "'{0}' {1}. Afecta a: {2}.");
    public static readonly DiagnosticDescriptor PortableLayout = Portability("YS0706", "Layout de buffer distinto entre APIs",
        "'{0}' en '{1}' {2}. Afecta a: {3}.");
    public static readonly DiagnosticDescriptor NonUniformDerivative = Portability("YS0707", "Operación con derivadas en control de flujo no uniforme",
        "'{0}' está dentro de un {1} cuya condición depende de una entrada del shader. Es un error en WGSL y da resultados indefinidos en las demás APIs. Afecta a: {2}.");
    public static readonly DiagnosticDescriptor FloatSpecialValues = Portability("YS0708", "NaN e infinito no fiables",
        "'{0}' puede devolver siempre false con las optimizaciones de coma flotante de {1}.");
    public static readonly DiagnosticDescriptor IntegerDivisionByZero = Warning("YS0709", "División entera por cero",
        "La división entera por cero da un resultado indefinido en la GPU (en C# lanza una excepción).");
    public static readonly DiagnosticDescriptor NotInGodot = Portability("YS0710", "No disponible en Godot",
        "'{0}' {1}. Afecta a: Godot.");

    /// <summary>Todos los descriptores, para <c>SupportedDiagnostics</c>.</summary>
    public static IReadOnlyList<DiagnosticDescriptor> All { get; } = typeof(ShaderRules)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(DiagnosticDescriptor))
        .Select(f => (DiagnosticDescriptor)f.GetValue(null)!)
        .ToList();
}
