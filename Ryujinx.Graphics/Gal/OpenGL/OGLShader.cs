using OpenTK.Graphics.OpenGL;
using Ryujinx.Graphics.Gal.Shader;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Ryujinx.Graphics.Gal.OpenGL
{
    public class OGLShader : IGalShader
    {
        private class ShaderStage : IDisposable
        {
            public int Handle { get; private set; }

            public bool IsCompiled { get; private set; }

            public GalShaderType Type { get; private set; }

            public string Code { get; private set; }

            public IEnumerable<ShaderDeclInfo> TextureUsage { get; private set; }
            public IEnumerable<ShaderDeclInfo> UniformUsage { get; private set; }

            public ShaderStage(
                GalShaderType               Type,
                string                      Code,
                IEnumerable<ShaderDeclInfo> TextureUsage,
                IEnumerable<ShaderDeclInfo> UniformUsage)
            {
                this.Type         = Type;
                this.Code         = Code;
                this.TextureUsage = TextureUsage;
                this.UniformUsage = UniformUsage;
            }

            public void Compile()
            {
                if (Handle == 0)
                {
                    Handle = GL.CreateShader(OGLEnumConverter.GetShaderType(Type));

                    CompileAndCheck(Handle, Code);
                }
            }

            public void Dispose()
            {
                Dispose(true);
            }

            protected virtual void Dispose(bool Disposing)
            {
                if (Disposing && Handle != 0)
                {
                    GL.DeleteShader(Handle);

                    Handle = 0;
                }
            }
        }

        private struct ShaderProgram
        {
            public ShaderStage Vertex;
            public ShaderStage TessControl;
            public ShaderStage TessEvaluation;
            public ShaderStage Geometry;
            public ShaderStage Fragment;
        }

        private ShaderProgram Current;

        private ConcurrentDictionary<long, ShaderStage> Stages;

        private Dictionary<ShaderProgram, int> Programs;

        public int CurrentProgramHandle { get; private set; }

        public OGLShader()
        {
            Stages = new ConcurrentDictionary<long, ShaderStage>();

            Programs = new Dictionary<ShaderProgram, int>();
        }

        public void Create(IGalMemory Memory, long Tag, GalShaderType Type)
        {
            Stages.GetOrAdd(Tag, (Key) => ShaderStageFactory(Memory, Tag, Type));
        }

        private ShaderStage ShaderStageFactory(IGalMemory Memory, long Position, GalShaderType Type)
        {
            GlslProgram Program = GetGlslProgram(Memory, Position, Type);

            return new ShaderStage(
                Type,
                Program.Code,
                Program.Textures,
                Program.Uniforms);
        }

        private GlslProgram GetGlslProgram(IGalMemory Memory, long Position, GalShaderType Type)
        {
            GlslDecompiler Decompiler = new GlslDecompiler();

            return Decompiler.Decompile(Memory, Position + 0x50, Type);
        }

        public IEnumerable<ShaderDeclInfo> GetTextureUsage(long Tag)
        {
            if (Stages.TryGetValue(Tag, out ShaderStage Stage))
            {
                return Stage.TextureUsage;
            }

            return Enumerable.Empty<ShaderDeclInfo>();
        }

        public void SetConstBuffer(long Tag, int Cbuf, byte[] Data)
        {
            BindProgram();

            if (Stages.TryGetValue(Tag, out ShaderStage Stage))
            {
                foreach (ShaderDeclInfo DeclInfo in Stage.UniformUsage.Where(x => x.Cbuf == Cbuf))
                {
                    int Location = GL.GetUniformLocation(CurrentProgramHandle, DeclInfo.Name);

                    int Count = Data.Length >> 2;

                    //The Index is the index of the last element,
                    //so we can add 1 to get the uniform array size.
                    Count = Math.Min(Count, DeclInfo.Index + 1);

                    unsafe
                    {
                        fixed (byte* Ptr = Data)
                        {
                            GL.Uniform1(Location, Count, (float*)Ptr);
                        }
                    }
                }
            }
        }

        public void SetUniform1(string UniformName, int Value)
        {
            BindProgram();

            int Location = GL.GetUniformLocation(CurrentProgramHandle, UniformName);

            GL.Uniform1(Location, Value);
        }

        public void SetUniform2F(string UniformName, float X, float Y)
        {
            BindProgram();

            int Location = GL.GetUniformLocation(CurrentProgramHandle, UniformName);

            GL.Uniform2(Location, X, Y);
        }

        public void Bind(long Tag)
        {
            if (Stages.TryGetValue(Tag, out ShaderStage Stage))
            {
                Bind(Stage);
            }
        }

        private void Bind(ShaderStage Stage)
        {
            switch (Stage.Type)
            {
                case GalShaderType.Vertex:         Current.Vertex         = Stage; break;
                case GalShaderType.TessControl:    Current.TessControl    = Stage; break;
                case GalShaderType.TessEvaluation: Current.TessEvaluation = Stage; break;
                case GalShaderType.Geometry:       Current.Geometry       = Stage; break;
                case GalShaderType.Fragment:       Current.Fragment       = Stage; break;
            }
        }

        public void BindProgram()
        {
            if (Current.Vertex   == null ||
                Current.Fragment == null)
            {
                return;
            }

            if (!Programs.TryGetValue(Current, out int Handle))
            {
                Handle = GL.CreateProgram();

                AttachIfNotNull(Handle, Current.Vertex);
                AttachIfNotNull(Handle, Current.TessControl);
                AttachIfNotNull(Handle, Current.TessEvaluation);
                AttachIfNotNull(Handle, Current.Geometry);
                AttachIfNotNull(Handle, Current.Fragment);

                GL.LinkProgram(Handle);

                CheckProgramLink(Handle);

                Programs.Add(Current, Handle);
            }

            GL.UseProgram(Handle);

            CurrentProgramHandle = Handle;
        }

        private void AttachIfNotNull(int ProgramHandle, ShaderStage Stage)
        {
            if (Stage != null)
            {
                Stage.Compile();

                GL.AttachShader(ProgramHandle, Stage.Handle);
            }
        }

        public static void CompileAndCheck(int Handle, string Code)
        {
            GL.ShaderSource(Handle, Code);
            GL.CompileShader(Handle);

            CheckCompilation(Handle);
        }

        private static void CheckCompilation(int Handle)
        {
            int Status = 0;

            GL.GetShader(Handle, ShaderParameter.CompileStatus, out Status);

            if (Status == 0)
            {
                throw new ShaderException(GL.GetShaderInfoLog(Handle));
            }
        }

        private static void CheckProgramLink(int Handle)
        {
            int Status = 0;

            GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out Status);

            if (Status == 0)
            {
                throw new ShaderException(GL.GetProgramInfoLog(Handle));
            }
        }
    }
}