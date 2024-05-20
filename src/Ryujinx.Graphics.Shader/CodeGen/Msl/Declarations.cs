using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.StructuredIr;
using Ryujinx.Graphics.Shader.Translation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ryujinx.Graphics.Shader.CodeGen.Msl
{
    static class Declarations
    {
        /*
         * Description of MSL Binding Strategy
         *
         * There are a few fundamental differences between how GLSL and MSL handle I/O.
         * This comment will set out to describe the reasons why things are done certain ways
         * and to describe the overall binding model that we're striving for here.
         *
         * Main I/O Structs
         *
         * Each stage will have a main input and output struct labeled as [Stage][In/Out], i.e VertexIn.
         * Every attribute within these structs will be labeled with an [[attribute(n)]] property,
         * and the overall struct will be labeled with [[stage_in]] for input structs, and defined as the
         * output type of the main shader function for the output struct. This struct also contains special
         * attribute-based properties like [[position]], therefore these are not confined to 'user-defined' variables.
         *
         * Samplers & Textures
         *
         * Metal does not have a combined image sampler like sampler2D in GLSL, as a result we need to bind
         * an individual texture and a sampler object for each instance of a combined image sampler.
         * Therefore, the binding indices of straight up textures (i.e. without a sampler) must start
         * after the last sampler/texture pair (n + Number of Pairs).
         *
         * Uniforms
         *
         * MSL does not have a concept of uniforms comparable to that of GLSL. As a result, instead of
         * being declared outside of any function body, uniforms are part of the function signature in MSL.
         * This applies to anything bound to the shader not included in the main I/O structs.
         */

        public static void Declare(CodeGenContext context, StructuredProgramInfo info)
        {
            context.AppendLine("#include <metal_stdlib>");
            context.AppendLine("#include <simd/simd.h>");
            context.AppendLine();
            context.AppendLine("using namespace metal;");
            context.AppendLine();

            if ((info.HelperFunctionsMask & HelperFunctionsMask.SwizzleAdd) != 0)
            {

            }

            DeclareInputAttributes(context, info.IoDefinitions.Where(x => IsUserDefined(x, StorageKind.Input)));
            context.AppendLine();
            DeclareOutputAttributes(context, info.IoDefinitions.Where(x => x.StorageKind == StorageKind.Output));
        }

        static bool IsUserDefined(IoDefinition ioDefinition, StorageKind storageKind)
        {
            return ioDefinition.StorageKind == storageKind && ioDefinition.IoVariable == IoVariable.UserDefined;
        }

        public static void DeclareLocals(CodeGenContext context, StructuredFunction function, ShaderStage stage)
        {
            switch (stage)
            {
                case ShaderStage.Vertex:
                    context.AppendLine("VertexOut out;");
                    break;
                case ShaderStage.Fragment:
                    context.AppendLine("FragmentOut out;");
                    break;
            }

            foreach (AstOperand decl in function.Locals)
            {
                string name = context.OperandManager.DeclareLocal(decl);

                context.AppendLine(GetVarTypeName(context, decl.VarType) + " " + name + ";");
            }
        }

        public static string GetVarTypeName(CodeGenContext context, AggregateType type)
        {
            return type switch
            {
                AggregateType.Void => "void",
                AggregateType.Bool => "bool",
                AggregateType.FP32 => "float",
                AggregateType.S32 => "int",
                AggregateType.U32 => "uint",
                AggregateType.Vector2 | AggregateType.Bool => "bool2",
                AggregateType.Vector2 | AggregateType.FP32 => "float2",
                AggregateType.Vector2 | AggregateType.S32 => "int2",
                AggregateType.Vector2 | AggregateType.U32 => "uint2",
                AggregateType.Vector3 | AggregateType.Bool => "bool3",
                AggregateType.Vector3 | AggregateType.FP32 => "float3",
                AggregateType.Vector3 | AggregateType.S32 => "int3",
                AggregateType.Vector3 | AggregateType.U32 => "uint3",
                AggregateType.Vector4 | AggregateType.Bool => "bool4",
                AggregateType.Vector4 | AggregateType.FP32 => "float4",
                AggregateType.Vector4 | AggregateType.S32 => "int4",
                AggregateType.Vector4 | AggregateType.U32 => "uint4",
                _ => throw new ArgumentException($"Invalid variable type \"{type}\"."),
            };
        }

        private static void DeclareInputAttributes(CodeGenContext context, IEnumerable<IoDefinition> inputs)
        {
            if (context.Definitions.IaIndexing)
            {
                // Not handled
            }
            else
            {
                if (inputs.Any())
                {
                    string prefix = "";

                    switch (context.Definitions.Stage)
                    {
                        case ShaderStage.Vertex:
                            context.AppendLine($"struct VertexIn");
                            break;
                        case ShaderStage.Fragment:
                            context.AppendLine($"struct FragmentIn");
                            break;
                        case ShaderStage.Compute:
                            context.AppendLine($"struct KernelIn");
                            break;
                    }

                    context.EnterScope();

                    if (context.Definitions.Stage == ShaderStage.Fragment)
                    {
                        // TODO: check if it's needed
                        context.AppendLine("float4 position [[position]];");
                    }

                    foreach (var ioDefinition in inputs.OrderBy(x => x.Location))
                    {
                        string type = GetVarTypeName(context, context.Definitions.GetUserDefinedType(ioDefinition.Location, isOutput: false));
                        string name = $"{DefaultNames.IAttributePrefix}{ioDefinition.Location}";
                        string suffix = context.Definitions.Stage switch
                        {
                            ShaderStage.Vertex => $" [[attribute({ioDefinition.Location})]]",
                            ShaderStage.Fragment => $" [[user(loc{ioDefinition.Location})]]",
                            _ => ""
                        };

                        context.AppendLine($"{type} {name}{suffix};");
                    }

                    context.LeaveScope(";");
                }
            }
        }

        private static void DeclareOutputAttributes(CodeGenContext context, IEnumerable<IoDefinition> inputs)
        {
            if (context.Definitions.IaIndexing)
            {
                // Not handled
            }
            else
            {
                if (inputs.Any())
                {
                    string prefix = "";

                    switch (context.Definitions.Stage)
                    {
                        case ShaderStage.Vertex:
                            context.AppendLine($"struct VertexOut");
                            break;
                        case ShaderStage.Fragment:
                            context.AppendLine($"struct FragmentOut");
                            break;
                        case ShaderStage.Compute:
                            context.AppendLine($"struct KernelOut");
                            break;
                    }

                    context.EnterScope();

                    foreach (var ioDefinition in inputs.OrderBy(x => x.Location))
                    {
                        string type = ioDefinition.IoVariable switch
                        {
                            IoVariable.Position => "float4",
                            IoVariable.PointSize => "float",
                            _ => GetVarTypeName(context, context.Definitions.GetUserDefinedType(ioDefinition.Location, isOutput: true))
                        };
                        string name = ioDefinition.IoVariable switch
                        {
                            IoVariable.Position => "position",
                            IoVariable.PointSize => "point_size",
                            IoVariable.FragmentOutputColor => $"color{ioDefinition.Location}",
                            _ => $"{DefaultNames.OAttributePrefix}{ioDefinition.Location}"
                        };
                        string suffix = ioDefinition.IoVariable switch
                        {
                            IoVariable.Position => " [[position]]",
                            IoVariable.PointSize => " [[point_size]]",
                            IoVariable.UserDefined => $" [[user(loc{ioDefinition.Location})]]",
                            IoVariable.FragmentOutputColor => $" [[color({ioDefinition.Location})]]",
                            _ => ""
                        };

                        context.AppendLine($"{type} {name}{suffix};");
                    }

                    context.LeaveScope(";");
                }
            }
        }
    }
}
