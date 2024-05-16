using Ryujinx.Common.Logging;
using Ryujinx.Graphics.Shader.CodeGen.Msl.Instructions;
using Ryujinx.Graphics.Shader.StructuredIr;
using Ryujinx.Graphics.Shader.Translation;
using System;
using System.Linq;
using static Ryujinx.Graphics.Shader.CodeGen.Msl.TypeConversion;

namespace Ryujinx.Graphics.Shader.CodeGen.Msl
{
    static class MslGenerator
    {
        public static string Generate(StructuredProgramInfo info, CodeGenParameters parameters)
        {
            if (parameters.Definitions.Stage is not (ShaderStage.Vertex or ShaderStage.Fragment or ShaderStage.Compute))
            {
                Logger.Warning?.Print(LogClass.Gpu, $"Attempted to generate unsupported shader type {parameters.Definitions.Stage}!");
                return "";
            }

            CodeGenContext context = new(info, parameters);

            Declarations.Declare(context, info);

            if (info.Functions.Count != 0)
            {
                for (int i = 1; i < info.Functions.Count; i++)
                {
                    PrintFunction(context, info.Functions[i], parameters.Definitions.Stage);

                    context.AppendLine();
                }
            }

            PrintFunction(context, info.Functions[0], parameters.Definitions.Stage, true);

            return context.GetCode();
        }

        private static void PrintFunction(CodeGenContext context, StructuredFunction function, ShaderStage stage, bool isMainFunc = false)
        {
            context.CurrentFunction = function;

            context.AppendLine(GetFunctionSignature(context, function, stage, isMainFunc));
            context.EnterScope();

            Declarations.DeclareLocals(context, function, stage);

            PrintBlock(context, function.MainBlock, isMainFunc);

            context.LeaveScope();
        }

        private static string GetFunctionSignature(
            CodeGenContext context,
            StructuredFunction function,
            ShaderStage stage,
            bool isMainFunc = false)
        {
            string[] args = new string[function.InArguments.Length + function.OutArguments.Length];

            for (int i = 0; i < function.InArguments.Length; i++)
            {
                args[i] = $"{Declarations.GetVarTypeName(context, function.InArguments[i])} {OperandManager.GetArgumentName(i)}";
            }

            for (int i = 0; i < function.OutArguments.Length; i++)
            {
                int j = i + function.InArguments.Length;

                // Likely need to be made into pointers
                args[j] = $"out {Declarations.GetVarTypeName(context, function.OutArguments[i])} {OperandManager.GetArgumentName(j)}";
            }

            string funcKeyword = "inline";
            string funcName = null;
            string returnType = Declarations.GetVarTypeName(context, function.ReturnType);

            if (isMainFunc)
            {
                if (stage == ShaderStage.Vertex)
                {
                    funcKeyword = "vertex";
                    funcName = "vertexMain";
                    returnType = "VertexOut";
                }
                else if (stage == ShaderStage.Fragment)
                {
                    funcKeyword = "fragment";
                    funcName = "fragmentMain";
                    returnType = "FragmentOut";
                }
                else if (stage == ShaderStage.Compute)
                {
                    funcKeyword = "kernel";
                    funcName = "kernelMain";
                    returnType = "void";
                }

                if (context.AttributeUsage.UsedInputAttributes != 0)
                {
                    if (stage == ShaderStage.Vertex)
                    {
                        args = args.Prepend("VertexIn in [[stage_in]]").ToArray();
                    }
                    else if (stage == ShaderStage.Fragment)
                    {
                        args = args.Prepend("FragmentIn in [[stage_in]]").ToArray();
                    }
                    else if (stage == ShaderStage.Compute)
                    {
                        args = args.Prepend("KernelIn in [[stage_in]]").ToArray();
                    }
                }

                // TODO: add these only if they are used
                if (stage == ShaderStage.Vertex)
                {
                    args = args.Append("uint vertex_id [[vertex_id]]").ToArray();
                    args = args.Append("uint instance_id [[instance_id]]").ToArray();
                }

                foreach (var constantBuffer in context.Properties.ConstantBuffers.Values)
                {
                    var varType = constantBuffer.Type.Fields[0].Type & ~AggregateType.Array;
                    args = args.Append($"constant {Declarations.GetVarTypeName(context, varType)} *{constantBuffer.Name} [[buffer({constantBuffer.Binding})]]").ToArray();
                }

                foreach (var storageBuffers in context.Properties.StorageBuffers.Values)
                {
                    var varType = storageBuffers.Type.Fields[0].Type & ~AggregateType.Array;
                    // Offset the binding by 15 to avoid clashing with the constant buffers
                    args = args.Append($"device {Declarations.GetVarTypeName(context, varType)} *{storageBuffers.Name} [[buffer({15 + storageBuffers.Binding})]]").ToArray();
                }

                foreach (var texture in context.Properties.Textures.Values)
                {
                    var textureTypeName = texture.Type.ToMslTextureType();
                    args = args.Append($"{textureTypeName} tex_{texture.Name} [[texture({texture.Binding})]]").ToArray();
                    // If the texture is not separate, we need to declare a sampler
                    if (!texture.Separate)
                    {
                        args = args.Append($"sampler samp_{texture.Name} [[sampler({texture.Binding})]]").ToArray();
                    }
                }
            }

            return $"{funcKeyword} {returnType} {funcName ?? function.Name}({string.Join(", ", args)})";
        }

        private static void PrintBlock(CodeGenContext context, AstBlock block, bool isMainFunction)
        {
            AstBlockVisitor visitor = new(block);

            visitor.BlockEntered += (sender, e) =>
            {
                switch (e.Block.Type)
                {
                    case AstBlockType.DoWhile:
                        context.AppendLine("do");
                        break;

                    case AstBlockType.Else:
                        context.AppendLine("else");
                        break;

                    case AstBlockType.ElseIf:
                        context.AppendLine($"else if ({GetCondExpr(context, e.Block.Condition)})");
                        break;

                    case AstBlockType.If:
                        context.AppendLine($"if ({GetCondExpr(context, e.Block.Condition)})");
                        break;

                    default:
                        throw new InvalidOperationException($"Found unexpected block type \"{e.Block.Type}\".");
                }

                context.EnterScope();
            };

            visitor.BlockLeft += (sender, e) =>
            {
                context.LeaveScope();

                if (e.Block.Type == AstBlockType.DoWhile)
                {
                    context.AppendLine($"while ({GetCondExpr(context, e.Block.Condition)});");
                }
            };

            bool supportsBarrierDivergence = context.HostCapabilities.SupportsShaderBarrierDivergence;
            bool mayHaveReturned = false;

            foreach (IAstNode node in visitor.Visit())
            {
                if (node is AstOperation operation)
                {
                    if (!supportsBarrierDivergence)
                    {
                        if (operation.Inst == IntermediateRepresentation.Instruction.Barrier)
                        {
                            // Barrier on divergent control flow paths may cause the GPU to hang,
                            // so skip emitting the barrier for those cases.
                            if (visitor.Block.Type != AstBlockType.Main || mayHaveReturned || !isMainFunction)
                            {
                                context.Logger.Log($"Shader has barrier on potentially divergent block, the barrier will be removed.");

                                continue;
                            }
                        }
                        else if (operation.Inst == IntermediateRepresentation.Instruction.Return)
                        {
                            mayHaveReturned = true;
                        }
                    }

                    string expr = InstGen.GetExpression(context, operation);

                    if (expr != null)
                    {
                        context.AppendLine(expr + ";");
                    }
                }
                else if (node is AstAssignment assignment)
                {
                    AggregateType dstType = OperandManager.GetNodeDestType(context, assignment.Destination);
                    AggregateType srcType = OperandManager.GetNodeDestType(context, assignment.Source);

                    string dest = InstGen.GetExpression(context, assignment.Destination);
                    string src = ReinterpretCast(context, assignment.Source, srcType, dstType);

                    context.AppendLine(dest + " = " + src + ";");
                }
                else if (node is AstComment comment)
                {
                    context.AppendLine("// " + comment.Comment);
                }
                else
                {
                    throw new InvalidOperationException($"Found unexpected node type \"{node?.GetType().Name ?? "null"}\".");
                }
            }
        }

        private static string GetCondExpr(CodeGenContext context, IAstNode cond)
        {
            AggregateType srcType = OperandManager.GetNodeDestType(context, cond);

            return ReinterpretCast(context, cond, srcType, AggregateType.Bool);
        }
    }
}
