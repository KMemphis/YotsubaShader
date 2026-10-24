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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using YotsubaShader.IR.Intermediate;
using YotsubaShader.IR.Intermediate.FunctionBody;

namespace YotsubaShader.Roslyn;

public interface IStatementOperationProcessor
{
    void ProcessBlockOperation(
        IBlockOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        Compilation compilation,
        Stage stage,
        out Statement? statement);

    void ProcessVariableDeclarationGroupOperation(
        IVariableDeclarationGroupOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        Compilation compilation,
        Stage stage,
        out Statement? statement);

    void ProcessSimpleAssignmentOperation(
        ISimpleAssignmentOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessCompoundAssignmentOperation(
        ICompoundAssignmentOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessCoalesceAssignmentOperation(
        ICoalesceAssignmentOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessDeconstructionAssignmentOperation(
        IDeconstructionAssignmentOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessIncrementOrDecrementOperation(
        IIncrementOrDecrementOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessInvocationOperation(
        IInvocationOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessDynamicInvocationOperation(
        IDynamicInvocationOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessFunctionPointerInvocationOperation(
        IFunctionPointerInvocationOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessObjectCreationOperation(
        IObjectCreationOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessDynamicObjectCreationOperation(
        IDynamicObjectCreationOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessConditionalAccessOperation(
        IConditionalAccessOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessAwaitOperation(
        IAwaitOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessReturnOperation(
        IReturnOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessConditionalOperation(
        IConditionalOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessForLoopOperation(
        IForLoopOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessForEachLoopOperation(
        IForEachLoopOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessWhileLoopOperation(
        IWhileLoopOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessSwitchOperation(
        ISwitchOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessBranchOperation(
        IBranchOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessLabeledOperation(
        ILabeledOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessTryOperation(
        ITryOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessThrowOperation(
        IThrowOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessUsingOperation(
        IUsingOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessUsingDeclarationOperation(
        IUsingDeclarationOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessLockOperation(
        ILockOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessFixedOperation(
        IOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessLocalFunctionOperation(
        ILocalFunctionOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessEventAssignmentOperation(
        IEventAssignmentOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessEmptyOperation(
        IEmptyOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);

    void ProcessInvalidOperation(
        IInvalidOperation operation,
        Dictionary<ISymbol, Storage> inputs,
        Dictionary<ISymbol, Storage> outputs,
        out Statement? statement);
}