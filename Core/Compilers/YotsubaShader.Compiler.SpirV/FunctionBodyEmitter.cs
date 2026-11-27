#region MS-PL License - Copyright (c) 2026 Memphis Yomael Núñez Cruz - Dominican Republic
/*
╔════════════════════════════════════════════════════════════════════════════════════╗
║                                                                                    ║
║  ███╗   ███╗███████╗      ██████╗ ██╗                                              ║
║  ████╗ ████║██╔════╝      ██╔══██╗██║                                              ║
║  ██╔████╔██║███████╗█████╗██████╔╝██║                                              ║
║  ██║╚██╔╝██║╚════██║╚════╝██╔═══╝ ██║                                              ║
║  ██║ ╚═╝ ██║███████║      ██║     ███████╗                                         ║
║  ╚═╝     ╚═╝╚══════╝      ╚═╝     ╚══════╝                                         ║
║                                                                                    ║
╠════════════════════════════════════════════════════════════════════════════════════╣
║  Copyright (c) 2026 Memphis Yomael Núñez Cruz - Dominican Republic                 ║
╠════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                    ║
║  Microsoft Public License (MS-PL)                                                  ║
║                                                                                    ║
║  This license governs use of the accompanying software. If you use the software,   ║
║  you accept this license. If you do not accept the license, do not use the         ║
║  software.                                                                         ║
║                                                                                    ║
║  1. Definitions                                                                    ║
║  The terms "reproduce," "reproduction," "derivative works," and "distribution"     ║
║  have the same meaning here as under U.S. copyright law.                           ║
║  A "contribution" is the original software, to which copyright notices are         ║
║  attached, and any additions or changes to the software.                           ║
║  A "contributor" is any person that distributes its contribution under this        ║
║  license.                                                                          ║
║  "Licensed patents" are a contributor's patent claims that read directly on its    ║
║  contribution.                                                                     ║
║                                                                                    ║
║  2. Grant of Rights                                                                ║
║  (A) Copyright Grant- Subject to the terms of this license, including the license  ║
║  conditions and limitations in section 3, each contributor grants you a non-       ║
║  exclusive, worldwide, royalty-free copyright license to reproduce its             ║
║  contribution, prepare derivative works of its contribution, and distribute its    ║
║  contribution or any derivative works that you create.                             ║
║  (B) Patent Grant- Subject to the terms of this license, including the license     ║
║  conditions and limitations in section 3, each contributor grants you a non-       ║
║  exclusive, worldwide, royalty-free license under its licensed patents to make,    ║
║  have made, use, sell, offer for sale, import, and/or otherwise dispose of its     ║
║  contribution in the software or derivative works of the contribution in the       ║
║  software.                                                                         ║
║                                                                                    ║
║  3. Conditions and Limitations                                                     ║
║  (A) No Trademark License- This license does not grant you rights to use any       ║
║  contributors' name, logo, or trademarks.                                          ║
║  (B) If you bring a patent claim against any contributor over patents that you     ║
║  claim are infringed by the software, your patent license from such contributor    ║
║  to the software ends automatically.                                               ║
║  (C) If you distribute any portion of the software, you must retain all copyright, ║
║  patent, trademark, and attribution notices that are present in the software.      ║
║  (D) If you distribute any portion of the software in source code form, you may do ║
║  so only under this license by including a complete copy of this license with      ║
║  your distribution. If you distribute any portion of the software in compiled or   ║
║  object code form, you may only do so under a license that complies with this      ║
║  license.                                                                          ║
║  (E) The software is licensed "as-is." You bear the risk of using it. The          ║
║  contributors give no express warranties, guarantees or conditions. You may have   ║
║  additional consumer rights under your local laws which this license cannot        ║
║  change. To the extent permitted under your local laws, the contributors exclude   ║
║  the implied warranties of merchantability, fitness for a particular purpose and   ║
║  non-infringement.                                                                 ║
║                                                                                    ║
╚════════════════════════════════════════════════════════════════════════════════════╝
*/
#endregion

using System.Collections.Immutable;
using YotsubaShader.Compiler.SpirV.Builder;
using YotsubaShader.IR.Intermediate;
using YotsubaShader.IR.Intermediate.FunctionBody;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions;
using YotsubaShader.IR.Intermediate.FunctionBody.Expressions.FunctionCalls;
using YotsubaShader.IR.Intermediate.FunctionBody.Statements;

namespace YotsubaShader.Compiler.SpirV;

internal sealed partial class FunctionBodyEmitter
{
    private readonly SpirVModule _module;
    private readonly Dictionary<Storage, string> _pointers;
    private readonly Dictionary<Storage, string> _values;
    private IReadOnlyDictionary<FunctionId, Function> _functionsById = new Dictionary<FunctionId, Function>();
    private int _nextTemporaryValue;
    
    private readonly Stack<(string Merge, string Continue)> _loops = new();

    /// <summary>Destino de <c>break</c>: el merge del bucle o del switch más interno.</summary>
    private readonly Stack<string> _breakTargets = new();
    private bool _terminated;   // el bloque actual ya terminó (OpBranch/OpReturn/...)
    private int _nextLabel;
    private IReadOnlyList<Function> _otherFuncs = [];

    private static readonly SpirVInstructionParameter NoneControl =
        new("None", SpirVInstructionType.Enumerant, 0);
    
    

    public FunctionBodyEmitter(
        SpirVModule module,
        Dictionary<Storage, string> pointers,
        Dictionary<Storage, string> values)
    {
        _module = module;
        _pointers = pointers;
        _values = values;
    }

    public void EmitStatements(IReadOnlyList<Statement> statements, Function function, IReadOnlyList<Function> otherFuncs)
    {
        _functionsById = otherFuncs.ToDictionary(d => d.Id);
        _otherFuncs = otherFuncs;
        SpillArrayParameters(function, statements);
        foreach (var statement in statements)
        {
            EmitStatement(statement, function, otherFuncs);
        }
    }

    /// <summary>
    /// Un parámetro recibido por valor es un valor SPIR-V (SSA), no una variable. Hace falta una variable cuando el
    /// método lo modifica (<c>q = q * 2;</c>, también dentro de un bucle) o, si es un array, lo indexa con un índice
    /// variable. Se copia una vez al entrar en la función; el llamador no ve los cambios.
    /// </summary>
    private void SpillArrayParameters(Function function, IReadOnlyList<Statement> body)
    {
        var assigned = new HashSet<Storage>();
        CollectAssignedStorages(body, assigned);

        foreach (var (storage, valueId) in _values.Where(p => p.Key is IR.Intermediate.Types.Array || assigned.Contains(p.Key)).ToList())
        {
            string typeId = _module.DeclareType(storage, function.Stage);
            string pointerTypeId = _module.DeclarePointer(YS_Types.SpirVStorageClass.Function, typeId);
            string pointerId = $"%param_copy_{storage.HumanName}_{function.HumanName}_{function.Id.Value}";
            _module.AddResultLine(pointerId, SpirVOpTable.OpVariable, SpirVModuleHelpers.Id(pointerTypeId),
                SpirVInstructionParameter.Enumerant(YS_Types.SpirVStorageClass.Function));
            _module.AddLine(SpirVOpTable.OpStore, SpirVModuleHelpers.Id(pointerId), SpirVModuleHelpers.Id(valueId));
            _values.Remove(storage);
            _pointers[storage] = pointerId;
        }
    }

    /// <summary>La variable raíz de cada destino de una asignación, en cualquier nivel de anidamiento.</summary>
    private static void CollectAssignedStorages(IEnumerable<Statement> statements, HashSet<Storage> into)
    {
        foreach (Statement statement in statements)
        {
            switch (statement)
            {
                case Assignment assignment:
                    AddRoot(assignment.Target, into);
                    break;
                case Block block:
                    CollectAssignedStorages(block.Statements, into);
                    break;
                case If ifStatement:
                    CollectAssignedStorages(new[] { ifStatement.Then }, into);
                    if (ifStatement.Else is not null) CollectAssignedStorages(new[] { ifStatement.Else }, into);
                    break;
                case Loop loop:
                    CollectAssignedStorages(new[] { loop.Body, loop.Continuing }, into);
                    break;
                case Switch switchStatement:
                    foreach (SwitchSection section in switchStatement.Sections) CollectAssignedStorages(new[] { section.Body }, into);
                    break;
            }
        }
    }

    private static void AddRoot(Expression target, HashSet<Storage> into)
    {
        try { into.Add(GetRootStorage(target)); }
        catch (NotSupportedException) { /* no es una ubicación: la reporta el emisor al emitir la asignación */ }
    }

    private void EmitStatement(Statement statement, Function function,  IReadOnlyList<Function> otherFuncs)
    {
        
        if (_terminated)
        {
            return;
        }
        
        switch (statement)
        {
            case VariableDeclaration declaration:
                EmitLocal(declaration, ref  function);
                break;

            case Assignment assignment:
                EmitAssignment(assignment,ref  function);
                break;

            case Return returnStatement:
                EmitReturn(returnStatement,ref function);
                _terminated = true;
                break;
            
            case Block block:
                foreach (Statement child in block.Statements)
                {
                    EmitStatement(child, function, otherFuncs);
                }
                break;
            case FunctionCallStatement functionCallStatement:
                _ = EmitFunctionCall(
                    functionCallStatement.Target,
                    functionCallStatement.Arguments,
                    function,
                    requiresValue: false);
                break;
            case IntrinsicCallStatement intrinsicStatement:
                EmitIntrinsicStatement(intrinsicStatement, ref function);
                break;
            case Switch switchStatement:
                EmitSwitch(switchStatement, function, otherFuncs);
                break;
            case If ifStatement:
                EmitIf(ifStatement, function, otherFuncs);
                break;

            case Loop loop:
                EmitLoop(loop, function, otherFuncs);
                break;

            case Break:
                if (_breakTargets.Count == 0) throw new InvalidOperationException("'break' outside of a loop or switch.");
                Branch(_breakTargets.Peek());
                break;

            case Continue:
                if (_loops.Count == 0) throw new InvalidOperationException("'continue' outside of a loop.");
                Branch(_loops.Peek().Continue);
                break;
            default:
                throw new NotSupportedException(
                    $"IR statement '{statement.GetType().Name}' is not supported.");
        }
    }
    
    public void FinishFunction(Function function)
    {
        if (_terminated)
        {
            return;
        }

        // Un bloque final sin terminar es un `merge` inalcanzable (p. ej. if/else donde ambas ramas retornan).
        bool returnsValue = function.Stage == Stage.None &&
                            function.Outputs.Any(storage => storage.HumanName == "__return");

        _module.AddLine(returnsValue ? SpirVOpTable.OpUnreachable : SpirVOpTable.OpReturn);
        _terminated = true;
    }
}
