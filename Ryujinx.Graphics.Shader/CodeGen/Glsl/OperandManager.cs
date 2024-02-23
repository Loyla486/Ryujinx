using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.StructuredIr;
using Ryujinx.Graphics.Shader.Translation;
using System;
using System.Collections.Generic;

using static Ryujinx.Graphics.Shader.StructuredIr.InstructionInfo;

namespace Ryujinx.Graphics.Shader.CodeGen.Glsl
{
    class OperandManager
    {
        private static string[] _stagePrefixes = new string[] { "cp", "vp", "tcp", "tep", "gp", "fp" };

        private struct BuiltInAttribute
        {
            public string Name { get; }

            public VariableType Type { get; }

            public BuiltInAttribute(string name, VariableType type)
            {
                Name = name;
                Type = type;
            }
        }

        private static Dictionary<int, BuiltInAttribute> _builtInAttributes =
                   new Dictionary<int, BuiltInAttribute>()
        {
            { AttributeConsts.Layer,               new BuiltInAttribute("gl_Layer",           VariableType.S32)  },
            { AttributeConsts.PointSize,           new BuiltInAttribute("gl_PointSize",       VariableType.F32)  },
            { AttributeConsts.PositionX,           new BuiltInAttribute("gl_Position.x",      VariableType.F32)  },
            { AttributeConsts.PositionY,           new BuiltInAttribute("gl_Position.y",      VariableType.F32)  },
            { AttributeConsts.PositionZ,           new BuiltInAttribute("gl_Position.z",      VariableType.F32)  },
            { AttributeConsts.PositionW,           new BuiltInAttribute("gl_Position.w",      VariableType.F32)  },
            { AttributeConsts.ClipDistance0,       new BuiltInAttribute("gl_ClipDistance[0]", VariableType.F32)  },
            { AttributeConsts.ClipDistance1,       new BuiltInAttribute("gl_ClipDistance[1]", VariableType.F32)  },
            { AttributeConsts.ClipDistance2,       new BuiltInAttribute("gl_ClipDistance[2]", VariableType.F32)  },
            { AttributeConsts.ClipDistance3,       new BuiltInAttribute("gl_ClipDistance[3]", VariableType.F32)  },
            { AttributeConsts.ClipDistance4,       new BuiltInAttribute("gl_ClipDistance[4]", VariableType.F32)  },
            { AttributeConsts.ClipDistance5,       new BuiltInAttribute("gl_ClipDistance[5]", VariableType.F32)  },
            { AttributeConsts.ClipDistance6,       new BuiltInAttribute("gl_ClipDistance[6]", VariableType.F32)  },
            { AttributeConsts.ClipDistance7,       new BuiltInAttribute("gl_ClipDistance[7]", VariableType.F32)  },
            { AttributeConsts.PointCoordX,         new BuiltInAttribute("gl_PointCoord.x",    VariableType.F32)  },
            { AttributeConsts.PointCoordY,         new BuiltInAttribute("gl_PointCoord.y",    VariableType.F32)  },
            { AttributeConsts.TessCoordX,          new BuiltInAttribute("gl_TessCoord.x",     VariableType.F32)  },
            { AttributeConsts.TessCoordY,          new BuiltInAttribute("gl_TessCoord.y",     VariableType.F32)  },
            { AttributeConsts.InstanceId,          new BuiltInAttribute("gl_InstanceID",      VariableType.S32)  },
            { AttributeConsts.VertexId,            new BuiltInAttribute("gl_VertexID",        VariableType.S32)  },
            { AttributeConsts.FrontFacing,         new BuiltInAttribute("gl_FrontFacing",     VariableType.Bool) },

            // Special.
            { AttributeConsts.FragmentOutputDepth, new BuiltInAttribute("gl_FragDepth",                           VariableType.F32) },
            { AttributeConsts.ThreadIdX,           new BuiltInAttribute("gl_LocalInvocationID.x",                 VariableType.U32) },
            { AttributeConsts.ThreadIdY,           new BuiltInAttribute("gl_LocalInvocationID.y",                 VariableType.U32) },
            { AttributeConsts.ThreadIdZ,           new BuiltInAttribute("gl_LocalInvocationID.z",                 VariableType.U32) },
            { AttributeConsts.CtaIdX,              new BuiltInAttribute("gl_WorkGroupID.x",                       VariableType.U32) },
            { AttributeConsts.CtaIdY,              new BuiltInAttribute("gl_WorkGroupID.y",                       VariableType.U32) },
            { AttributeConsts.CtaIdZ,              new BuiltInAttribute("gl_WorkGroupID.z",                       VariableType.U32) },
            { AttributeConsts.LaneId,              new BuiltInAttribute("gl_SubGroupInvocationARB",               VariableType.U32) },
            { AttributeConsts.EqMask,              new BuiltInAttribute("unpackUint2x32(gl_SubGroupEqMaskARB).x", VariableType.U32) },
            { AttributeConsts.GeMask,              new BuiltInAttribute("unpackUint2x32(gl_SubGroupGeMaskARB).x", VariableType.U32) },
            { AttributeConsts.GtMask,              new BuiltInAttribute("unpackUint2x32(gl_SubGroupGtMaskARB).x", VariableType.U32) },
            { AttributeConsts.LeMask,              new BuiltInAttribute("unpackUint2x32(gl_SubGroupLeMaskARB).x", VariableType.U32) },
            { AttributeConsts.LtMask,              new BuiltInAttribute("unpackUint2x32(gl_SubGroupLtMaskARB).x", VariableType.U32) },
        };

        private Dictionary<AstOperand, string> _locals;

        public OperandManager()
        {
            _locals = new Dictionary<AstOperand, string>();
        }

        public string DeclareLocal(AstOperand operand)
        {
            string name = $"{DefaultNames.LocalNamePrefix}_{_locals.Count}";

            _locals.Add(operand, name);

            return name;
        }

        public string GetExpression(AstOperand operand, ShaderStage stage)
        {
            switch (operand.Type)
            {
                case OperandType.Attribute:
                    return GetAttributeName(operand, stage);

                case OperandType.Constant:
                    return NumberFormatter.FormatInt(operand.Value);

                case OperandType.ConstantBuffer:
                    return GetConstantBufferName(operand.CbufSlot, operand.CbufOffset, stage);

                case OperandType.LocalVariable:
                    return _locals[operand];

                case OperandType.Undefined:
                    return DefaultNames.UndefinedName;
            }

            throw new ArgumentException($"Invalid operand type \"{operand.Type}\".");
        }

        public static string GetConstantBufferName(int slot, int offset, ShaderStage stage)
        {
            string ubName = GetUbName(stage, slot);

            ubName += "[" + (offset >> 2) + "]";

            return ubName + "." + GetSwizzleMask(offset & 3);
        }

        private static string GetVec4Indexed(string vectorName, string indexExpr)
        {
            string result = $"{vectorName}.x";
            for (int i = 1; i < 4; i++)
            {
                result = $"(({indexExpr}) == {i}) ? ({vectorName}.{GetSwizzleMask(i)}) : ({result})";
            }
            return $"({result})";
        }

        public static string GetConstantBufferName(IAstNode slot, string offsetExpr, ShaderStage stage)
        {
            // Non-constant slots are not supported.
            // It is expected that upstream stages are never going to generate non-constant
            // slot access.
            AstOperand operand = (AstOperand)slot;

            string ubName = GetUbName(stage, operand.Value);

            string index0 = "[" + offsetExpr + " >> 2]";

            return GetVec4Indexed(ubName + index0, offsetExpr + " & 3");
        }

        public static string GetOutAttributeName(AstOperand attr, ShaderStage stage)
        {
            return GetAttributeName(attr, stage, isOutAttr: true);
        }

        public static string GetAttributeName(AstOperand attr, ShaderStage stage, bool isOutAttr = false, string indexExpr = "0")
        {
            int value = attr.Value;

            string swzMask = GetSwizzleMask((value >> 2) & 3);

            if (value >= AttributeConsts.UserAttributeBase &&
                value <  AttributeConsts.UserAttributeEnd)
            {
                value -= AttributeConsts.UserAttributeBase;

                string prefix = isOutAttr
                    ? DefaultNames.OAttributePrefix
                    : DefaultNames.IAttributePrefix;

                string name = $"{prefix}{(value >> 4)}";

                if (stage == ShaderStage.Geometry && !isOutAttr)
                {
                    name += $"[{indexExpr}]";
                }

                name += "." + swzMask;

                return name;
            }
            else
            {
                if (value >= AttributeConsts.FragmentOutputColorBase &&
                    value <  AttributeConsts.FragmentOutputColorEnd)
                {
                    value -= AttributeConsts.FragmentOutputColorBase;

                    return $"{DefaultNames.OAttributePrefix}{(value >> 4)}.{swzMask}";
                }
                else if (_builtInAttributes.TryGetValue(value & ~3, out BuiltInAttribute builtInAttr))
                {
                    // TODO: There must be a better way to handle this...
                    if (stage == ShaderStage.Fragment)
                    {
                        switch (value & ~3)
                        {
                            case AttributeConsts.PositionX: return "(gl_FragCoord.x / fp_renderScale[0])";
                            case AttributeConsts.PositionY: return "(gl_FragCoord.y / fp_renderScale[0])";
                            case AttributeConsts.PositionZ: return "gl_FragCoord.z";
                            case AttributeConsts.PositionW: return "gl_FragCoord.w";
                        }
                    }

                    string name = builtInAttr.Name;

                    if (stage == ShaderStage.Geometry && !isOutAttr)
                    {
                        name = $"gl_in[{indexExpr}].{name}";
                    }

                    return name;
                }
            }

            // TODO: Warn about unknown built-in attribute.

            return isOutAttr ? "// bad_attr0x" + value.ToString("X") : "0.0";
        }

        public static string GetUbName(ShaderStage stage, int slot)
        {
            string ubName = GetShaderStagePrefix(stage);

            ubName += "_" + DefaultNames.UniformNamePrefix + slot;

            return ubName + "_" + DefaultNames.UniformNameSuffix;
        }

        public static string GetSamplerName(ShaderStage stage, AstTextureOperation texOp, string indexExpr)
        {
            string suffix;

            if ((texOp.Flags & TextureFlags.Bindless) != 0)
            {
                AstOperand operand = texOp.GetSource(0) as AstOperand;

                suffix = "_cb" + operand.CbufSlot + "_" + operand.CbufOffset;
            }
            else
            {
                suffix = texOp.Handle.ToString("X");

                if ((texOp.Type & SamplerType.Indexed) != 0)
                {
                    suffix += $"a[{indexExpr}]";
                }
            }

            return GetShaderStagePrefix(stage) + "_" + DefaultNames.SamplerNamePrefix + suffix;
        }

        public static string GetImageName(ShaderStage stage, AstTextureOperation texOp, string indexExpr)
        {
            string suffix = texOp.Handle.ToString("X");

            if ((texOp.Type & SamplerType.Indexed) != 0)
            {
                suffix += $"a[{indexExpr}]";
            }

            return GetShaderStagePrefix(stage) + "_" + DefaultNames.ImageNamePrefix + suffix;
        }

        public static string GetShaderStagePrefix(ShaderStage stage)
        {
            int index = (int)stage;

            if ((uint)index >= _stagePrefixes.Length)
            {
                return "invalid";
            }

            return _stagePrefixes[index];
        }

        private static string GetSwizzleMask(int value)
        {
            return "xyzw".Substring(value, 1);
        }

        public static VariableType GetNodeDestType(IAstNode node)
        {
            if (node is AstOperation operation)
            {
                // Load attribute basically just returns the attribute value.
                // Some built-in attributes may have different types, so we need
                // to return the type based on the attribute that is being read.
                if (operation.Inst == Instruction.LoadAttribute)
                {
                    return GetOperandVarType((AstOperand)operation.GetSource(0));
                }
                else if (operation is AstTextureOperation texOp &&
                         (texOp.Inst == Instruction.ImageLoad ||
                          texOp.Inst == Instruction.ImageStore))
                {
                    return texOp.Format.GetComponentType();
                }

                return GetDestVarType(operation.Inst);
            }
            else if (node is AstOperand operand)
            {
                return GetOperandVarType(operand);
            }
            else
            {
                throw new ArgumentException($"Invalid node type \"{node?.GetType().Name ?? "null"}\".");
            }
        }

        private static VariableType GetOperandVarType(AstOperand operand)
        {
            if (operand.Type == OperandType.Attribute)
            {
                if (_builtInAttributes.TryGetValue(operand.Value & ~3, out BuiltInAttribute builtInAttr))
                {
                    return builtInAttr.Type;
                }
            }

            return OperandInfo.GetVarType(operand);
        }
    }
}