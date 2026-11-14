using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace YotsubaShader.IDE.Analyzer.YotsubaGPUTypes;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class Vector : BaseAnalyser
{

    const string VectorAttribute = "YotsubaShader.Language.Native.Types.Vector.VectorAttribute";
    const string VectorComponentAttribute = "YotsubaShader.Language.Native.Types.Vector.VectorComponentAttribute";
    const string Category = "YotsubaShader";

    // {0} = vector, {1} = nº de componentes
    static readonly DiagnosticDescriptor ComponentCount = new(
        id: "YS0001",
        title: "Número de componentes inválido",
        messageFormat: "El vector '{0}' tiene {1} componentes; un vector necesita entre 2 y 4. Añade o elimina miembros [VectorComponent].",
        category: Category, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    // {0} = miembro, {1} = índice
    static readonly DiagnosticDescriptor NegativeIndex = new(
        id: "YS0002",
        title: "Índice de componente negativo",
        messageFormat: "'{0}' usa el índice {1}; el índice no puede ser negativo. Usa un valor entre 0 y 3.",
        category: Category, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    // {0} = miembro, {1} = índice, {2} = otro miembro que ya lo usa, {3} = primer índice libre
    static readonly DiagnosticDescriptor DuplicateIndex = new(
        id: "YS0003",
        title: "Índice de componente repetido",
        messageFormat: "'{0}' y '{2}' usan el índice {1}. Cambia '{0}' a [VectorComponent({3})].",
        category: Category, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    // {0} = miembro, {1} = su índice, {2} = primer índice que falta
    static readonly DiagnosticDescriptor MissingIndex = new(
        id: "YS0004",
        title: "Falta un índice de componente",
        messageFormat: "'{0}' usa el índice {1}, pero falta el índice {2}. Los índices deben ser contiguos desde 0: cambia '{0}' a [VectorComponent({2})].",
        category: Category, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    // {0} = vector, {1} = tipo de los primeros componentes, {2} = tipo del miembro en conflicto, {3} = miembro
    static readonly DiagnosticDescriptor InconsistentType = new(
        id: "YS0005",
        title: "Componentes con tipos distintos",
        messageFormat: "En '{0}', '{3}' es {2} pero los demás componentes son {1}. Todos deben tener el mismo tipo.",
        category: Category, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    // {0} = miembro, {1} = tipo usado
    static readonly DiagnosticDescriptor InvalidComponentType = new(
        id: "YS0006",
        title: "Tipo de componente no soportado",
        messageFormat: "'{0}' es {1}; los componentes de un vector solo pueden ser float, int o uint",
        category: Category, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    // {0} = miembro, {1} = vector, {2} = índice sugerido
    static readonly DiagnosticDescriptor NotAComponent = new(
        id: "YS0007",
        title: "Miembro del vector sin [VectorComponent]",
        messageFormat: "'{0}' pertenece al vector '{1}' pero no es un componente: no existirá en la GPU y su tamaño no coincidirá con el de la CPU. Márcalo [VectorComponent({2})] o muévelo fuera del vector.",
        category: Category, defaultSeverity: DiagnosticSeverity.Warning, isEnabledByDefault: true);

    // {0} = miembro, {1} = motivo ("es estático" | "es un indexador")
    static readonly DiagnosticDescriptor IgnoredComponent = new(
        id: "YS0008",
        title: "[VectorComponent] ignorado",
        messageFormat: "[VectorComponent] en '{0}' se ignora porque {1}. Quita el atributo o usa un campo o propiedad de instancia.",
        category: Category, defaultSeverity: DiagnosticSeverity.Warning, isEnabledByDefault: true);

    // {0} = miembro, {1} = tipo contenedor
    static readonly DiagnosticDescriptor ComponentOutsideVector = new(
        id: "YS0009",
        title: "[VectorComponent] fuera de un vector",
        messageFormat: "[VectorComponent] en '{0}' se ignora porque '{1}' no está marcado [Vector]. Añade [Vector] al tipo o quita el atributo.",
        category: Category, defaultSeverity: DiagnosticSeverity.Warning, isEnabledByDefault: true);

    // {0} = miembro
    static readonly DiagnosticDescriptor NonConstantInitializer = new(
        id: "YS0010",
        title: "Valor inicial no constante",
        messageFormat: "El valor inicial de '{0}' no es constante, por lo que valdrá 0 en la GPU. Usa una constante o asígnalo en el constructor.",
        category: Category, defaultSeverity: DiagnosticSeverity.Warning, isEnabledByDefault: true);


    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            ComponentCount, NegativeIndex, DuplicateIndex, MissingIndex, InconsistentType,
            InvalidComponentType, NotAComponent, IgnoredComponent, ComponentOutsideVector,
            NonConstantInitializer);
    
    protected override void Register(AnalysisContext context)
    {
        context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
        // context.RegisterOperationAction(AnalyzeFieldInitializer, OperationKind.FieldInitializer);
        // context.RegisterOperationAction(AnalyzePropertyInitializer, OperationKind.PropertyInitializer);
    }


    static void AnalyzeType(SymbolAnalysisContext ctx)
    {

    }
}