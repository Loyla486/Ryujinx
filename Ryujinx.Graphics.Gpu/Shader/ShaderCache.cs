using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Gpu.Shader.Cache;
using Ryujinx.Graphics.Gpu.Shader.Cache.Definition;
using Ryujinx.Graphics.Gpu.State;
using Ryujinx.Graphics.Shader;
using Ryujinx.Graphics.Shader.Translation;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Ryujinx.Graphics.Gpu.Shader
{
    /// <summary>
    /// Memory cache of shader code.
    /// </summary>
    class ShaderCache : IDisposable
    {
        private const TranslationFlags DefaultFlags = TranslationFlags.DebugMode;

        private readonly GpuContext _context;

        private readonly ShaderDumper _dumper;

        private readonly Dictionary<ulong, List<ShaderBundle>> _cpPrograms;
        private readonly Dictionary<ShaderAddresses, List<ShaderBundle>> _gpPrograms;

        private CacheManager _cacheManager;

        private Dictionary<Hash128, ShaderBundle> _gpProgramsDiskCache;
        private Dictionary<Hash128, ShaderBundle> _cpProgramsDiskCache;

        /// <summary>
        /// Version of the codegen (to be changed when codegen or guest format change).
        /// </summary>
        private const ulong ShaderCodeGenVersion = 1759;

        /// <summary>
        /// Creates a new instance of the shader cache.
        /// </summary>
        /// <param name="context">GPU context that the shader cache belongs to</param>
        public ShaderCache(GpuContext context)
        {
            _context = context;

            _dumper = new ShaderDumper();

            _cpPrograms = new Dictionary<ulong, List<ShaderBundle>>();
            _gpPrograms = new Dictionary<ShaderAddresses, List<ShaderBundle>>();
            _gpProgramsDiskCache = new Dictionary<Hash128, ShaderBundle>();
            _cpProgramsDiskCache = new Dictionary<Hash128, ShaderBundle>();
        }

        /// <summary>
        /// Initialize the cache.
        /// </summary>
        internal void Initialize()
        {
            if (GraphicsConfig.EnableShaderCache && GraphicsConfig.TitleId != null)
            {
                _cacheManager = new CacheManager(CacheGraphicsApi.OpenGL, CacheHashType.XxHash128, "glsl", GraphicsConfig.TitleId, ShaderCodeGenVersion);

                bool isReadOnly = _cacheManager.IsReadOnly;

                HashSet<Hash128> invalidEntries = null;

                if (isReadOnly)
                {
                    Logger.Warning?.Print(LogClass.Gpu, "Loading shader cache in read-only mode (cache in use by another program!)");
                }
                else
                {
                    invalidEntries = new HashSet<Hash128>();
                }

                ReadOnlySpan<Hash128> guestProgramList = _cacheManager.GetGuestProgramList();

                for (int programIndex = 0; programIndex < guestProgramList.Length; programIndex++)
                {
                    Hash128 key = guestProgramList[programIndex];

                    Logger.Info?.Print(LogClass.Gpu, $"Compiling shader {key} ({programIndex + 1} / {guestProgramList.Length})");

                    byte[] hostProgramBinary = _cacheManager.GetHostProgramByHash(ref key);
                    bool hasHostCache = hostProgramBinary != null;

                    IProgram hostProgram = null;

                    // If the program sources aren't in the cache, compile from saved guest program.
                    byte[] guestProgram = _cacheManager.GetGuestProgramByHash(ref key);

                    if (guestProgram == null)
                    {
                        Logger.Error?.Print(LogClass.Gpu, $"Ignoring orphan shader hash {key} in cache (is the cache incomplete?)");

                        // Should not happen, but if someone messed with the cache it's better to catch it.
                        invalidEntries?.Add(key);

                        continue;
                    }

                    ReadOnlySpan<byte> guestProgramReadOnlySpan = guestProgram;

                    ReadOnlySpan<GuestShaderCacheEntry> cachedShaderEntries = GuestShaderCacheEntry.Parse(ref guestProgramReadOnlySpan, out GuestShaderCacheHeader fileHeader);

                    if (cachedShaderEntries[0].Header.Stage == ShaderStage.Compute)
                    {
                        Debug.Assert(cachedShaderEntries.Length == 1);

                        GuestShaderCacheEntry entry = cachedShaderEntries[0];

                        HostShaderCacheEntry[] hostShaderEntries = null;

                        // Try loading host shader binary.
                        if (hasHostCache)
                        {
                            hostShaderEntries = HostShaderCacheEntry.Parse(hostProgramBinary, out ReadOnlySpan<byte> hostProgramBinarySpan);
                            hostProgramBinary = hostProgramBinarySpan.ToArray();
                            hostProgram = _context.Renderer.LoadProgramBinary(hostProgramBinary);
                        }

                        bool isHostProgramValid = hostProgram != null;

                        ShaderProgram program;
                        ShaderProgramInfo shaderProgramInfo;

                        // Reconstruct code holder.
                        if (isHostProgramValid)
                        {
                            program = new ShaderProgram(entry.Header.Stage, "", entry.Header.Size, entry.Header.SizeA);
                            shaderProgramInfo = hostShaderEntries[0].ToShaderProgramInfo();
                        }
                        else
                        {
                            IGpuAccessor gpuAccessor = new CachedGpuAccessor(_context, entry.Code, entry.Header.GpuAccessorHeader, entry.TextureDescriptors);

                            program = Translator.CreateContext(0, gpuAccessor, DefaultFlags | TranslationFlags.Compute).Translate(out shaderProgramInfo);
                        }

                        ShaderCodeHolder shader = new ShaderCodeHolder(program, shaderProgramInfo, entry.Code);

                        // If the host program was rejected by the gpu driver or isn't in cache, try to build from program sources again.
                        if (hostProgram == null)
                        {
                            Logger.Info?.Print(LogClass.Gpu, $"Host shader {key} got invalidated, rebuilding from guest...");

                            // Compile shader and create program as the shader program binary got invalidated.
                            shader.HostShader = _context.Renderer.CompileShader(ShaderStage.Compute, shader.Program.Code);
                            hostProgram = _context.Renderer.CreateProgram(new IShader[] { shader.HostShader }, null);

                            // As the host program was invalidated, save the new entry in the cache.
                            hostProgramBinary = HostShaderCacheEntry.Create(hostProgram.GetBinary(), new ShaderCodeHolder[] { shader });

                            if (!isReadOnly)
                            {
                                if (hasHostCache)
                                {
                                    _cacheManager.ReplaceHostProgram(ref key, hostProgramBinary);
                                }
                                else
                                {
                                    Logger.Warning?.Print(LogClass.Gpu, $"Add missing host shader {key} in cache (is the cache incomplete?)");

                                    _cacheManager.AddHostProgram(ref key, hostProgramBinary);
                                }
                            }
                        }

                        _cpProgramsDiskCache.Add(key, new ShaderBundle(hostProgram, shader));
                    }
                    else
                    {
                        Debug.Assert(cachedShaderEntries.Length == Constants.ShaderStages);

                        ShaderCodeHolder[] shaders = new ShaderCodeHolder[cachedShaderEntries.Length];
                        List<ShaderProgram> shaderPrograms = new List<ShaderProgram>();

                        TransformFeedbackDescriptor[] tfd = CacheHelper.ReadTransformationFeedbackInformations(ref guestProgramReadOnlySpan, fileHeader);

                        TranslationFlags flags = DefaultFlags;

                        if (tfd != null)
                        {
                            flags = TranslationFlags.Feedback;
                        }

                        TranslationCounts counts = new TranslationCounts();

                        HostShaderCacheEntry[] hostShaderEntries = null;

                        // Try loading host shader binary.
                        if (hasHostCache)
                        {
                            hostShaderEntries = HostShaderCacheEntry.Parse(hostProgramBinary, out ReadOnlySpan<byte> hostProgramBinarySpan);
                            hostProgramBinary = hostProgramBinarySpan.ToArray();
                            hostProgram = _context.Renderer.LoadProgramBinary(hostProgramBinary);
                        }

                        bool isHostProgramValid = hostProgram != null;

                        // Reconstruct code holder.
                        for (int i = 0; i < cachedShaderEntries.Length; i++)
                        {
                            GuestShaderCacheEntry entry = cachedShaderEntries[i];

                            if (entry == null)
                            {
                                continue;
                            }

                            ShaderProgram program;

                            if (entry.Header.SizeA != 0)
                            {
                                ShaderProgramInfo shaderProgramInfo;

                                if (isHostProgramValid)
                                {
                                    program = new ShaderProgram(entry.Header.Stage, "", entry.Header.Size, entry.Header.SizeA);
                                    shaderProgramInfo = hostShaderEntries[i].ToShaderProgramInfo();
                                }
                                else
                                {
                                    IGpuAccessor gpuAccessor = new CachedGpuAccessor(_context, entry.Code, entry.Header.GpuAccessorHeader, entry.TextureDescriptors);

                                    program = Translator.CreateContext((ulong)entry.Header.Size, 0, gpuAccessor, flags, counts).Translate(out shaderProgramInfo);
                                }

                                // NOTE: Vertex B comes first in the shader cache.
                                byte[] code = entry.Code.AsSpan().Slice(0, entry.Header.Size).ToArray();
                                byte[] code2 = entry.Code.AsSpan().Slice(entry.Header.Size, entry.Header.SizeA).ToArray();

                                shaders[i] = new ShaderCodeHolder(program, shaderProgramInfo, code, code2);
                            }
                            else
                            {
                                ShaderProgramInfo shaderProgramInfo;

                                if (isHostProgramValid)
                                {
                                    program = new ShaderProgram(entry.Header.Stage, "", entry.Header.Size, entry.Header.SizeA);
                                    shaderProgramInfo = hostShaderEntries[i].ToShaderProgramInfo();
                                }
                                else
                                {
                                    IGpuAccessor gpuAccessor = new CachedGpuAccessor(_context, entry.Code, entry.Header.GpuAccessorHeader, entry.TextureDescriptors);

                                    program = Translator.CreateContext(0, gpuAccessor, flags, counts).Translate(out shaderProgramInfo);
                                }

                                shaders[i] = new ShaderCodeHolder(program, shaderProgramInfo, entry.Code);
                            }

                            shaderPrograms.Add(program);
                        }

                        // If the host program was rejected by the gpu driver or isn't in cache, try to build from program sources again.
                        if (!isHostProgramValid)
                        {
                            Logger.Info?.Print(LogClass.Gpu, $"Host shader {key} got invalidated, rebuilding from guest...");

                            List<IShader> hostShaders = new List<IShader>();

                            // Compile shaders and create program as the shader program binary got invalidated.
                            for (int stage = 0; stage < Constants.ShaderStages; stage++)
                            {
                                ShaderProgram program = shaders[stage]?.Program;

                                if (program == null)
                                {
                                    continue;
                                }

                                IShader hostShader = _context.Renderer.CompileShader(program.Stage, program.Code);

                                shaders[stage].HostShader = hostShader;

                                hostShaders.Add(hostShader);
                            }

                            hostProgram = _context.Renderer.CreateProgram(hostShaders.ToArray(), tfd);

                            // As the host program was invalidated, save the new entry in the cache.
                            hostProgramBinary = HostShaderCacheEntry.Create(hostProgram.GetBinary(), shaders);

                            if (!isReadOnly)
                            {
                                if (hasHostCache)
                                {
                                    _cacheManager.ReplaceHostProgram(ref key, hostProgramBinary);
                                }
                                else
                                {
                                    Logger.Warning?.Print(LogClass.Gpu, $"Add missing host shader {key} in cache (is the cache incomplete?)");

                                    _cacheManager.AddHostProgram(ref key, hostProgramBinary);
                                }
                            }
                        }

                        _gpProgramsDiskCache.Add(key, new ShaderBundle(hostProgram, shaders));
                    }
                }

                if (!isReadOnly)
                {
                    // Remove entries that are broken in the cache
                    _cacheManager.RemoveManifestEntries(invalidEntries);
                    _cacheManager.FlushToArchive();
                    _cacheManager.Synchronize();
                }

                Logger.Info?.Print(LogClass.Gpu, "Shader cache loaded.");
            }
        }

        /// <summary>
        /// Gets a compute shader from the cache.
        /// </summary>
        /// <remarks>
        /// This automatically translates, compiles and adds the code to the cache if not present.
        /// </remarks>
        /// <param name="state">Current GPU state</param>
        /// <param name="gpuVa">GPU virtual address of the binary shader code</param>
        /// <param name="localSizeX">Local group size X of the computer shader</param>
        /// <param name="localSizeY">Local group size Y of the computer shader</param>
        /// <param name="localSizeZ">Local group size Z of the computer shader</param>
        /// <param name="localMemorySize">Local memory size of the compute shader</param>
        /// <param name="sharedMemorySize">Shared memory size of the compute shader</param>
        /// <returns>Compiled compute shader code</returns>
        public ShaderBundle GetComputeShader(
            GpuState state,
            ulong gpuVa,
            int localSizeX,
            int localSizeY,
            int localSizeZ,
            int localMemorySize,
            int sharedMemorySize)
        {
            bool isCached = _cpPrograms.TryGetValue(gpuVa, out List<ShaderBundle> list);

            if (isCached)
            {
                foreach (ShaderBundle cachedCpShader in list)
                {
                    if (IsShaderEqual(cachedCpShader, gpuVa))
                    {
                        return cachedCpShader;
                    }
                }
            }

            TranslatorContext[] shaderContexts = new TranslatorContext[1];

            shaderContexts[0] = DecodeComputeShader(
                state,
                gpuVa,
                localSizeX,
                localSizeY,
                localSizeZ,
                localMemorySize,
                sharedMemorySize);

            bool isShaderCacheEnabled = _cacheManager != null;
            bool isShaderCacheReadOnly = false;

            Hash128 programCodeHash = default;
            GuestShaderCacheEntry[] shaderCacheEntries = null;

            if (isShaderCacheEnabled)
            {
                isShaderCacheReadOnly = _cacheManager.IsReadOnly;

                // Compute hash and prepare data for shader disk cache comparison.
                shaderCacheEntries = CacheHelper.CreateShaderCacheEntries(_context.MemoryManager, shaderContexts);
                programCodeHash = CacheHelper.ComputeGuestHashFromCache(shaderCacheEntries);
            }

            ShaderBundle cpShader;

            // Search for the program hash in loaded shaders.
            if (!isShaderCacheEnabled || !_cpProgramsDiskCache.TryGetValue(programCodeHash, out cpShader))
            {
                if (isShaderCacheEnabled)
                {
                    Logger.Debug?.Print(LogClass.Gpu, $"Shader {programCodeHash} not in cache, compiling!");
                }

                // The shader isn't currently cached, translate it and compile it.
                ShaderCodeHolder shader = TranslateShader(shaderContexts[0]);

                shader.HostShader = _context.Renderer.CompileShader(ShaderStage.Compute, shader.Program.Code);

                IProgram hostProgram = _context.Renderer.CreateProgram(new IShader[] { shader.HostShader }, null);

                byte[] hostProgramBinary = HostShaderCacheEntry.Create(hostProgram.GetBinary(), new ShaderCodeHolder[] { shader });

                cpShader = new ShaderBundle(hostProgram, shader);

                if (isShaderCacheEnabled)
                {
                    _cpProgramsDiskCache.Add(programCodeHash, cpShader);

                    if (!isShaderCacheReadOnly)
                    {
                        _cacheManager.SaveProgram(ref programCodeHash, CacheHelper.CreateGuestProgramDump(shaderCacheEntries), hostProgramBinary);
                    }
                }
            }

            if (!isCached)
            {
                list = new List<ShaderBundle>();

                _cpPrograms.Add(gpuVa, list);
            }

            list.Add(cpShader);

            return cpShader;
        }

        /// <summary>
        /// Gets a graphics shader program from the shader cache.
        /// This includes all the specified shader stages.
        /// </summary>
        /// <remarks>
        /// This automatically translates, compiles and adds the code to the cache if not present.
        /// </remarks>
        /// <param name="state">Current GPU state</param>
        /// <param name="addresses">Addresses of the shaders for each stage</param>
        /// <returns>Compiled graphics shader code</returns>
        public ShaderBundle GetGraphicsShader(GpuState state, ShaderAddresses addresses)
        {
            bool isCached = _gpPrograms.TryGetValue(addresses, out List<ShaderBundle> list);

            if (isCached)
            {
                foreach (ShaderBundle cachedGpShaders in list)
                {
                    if (IsShaderEqual(cachedGpShaders, addresses))
                    {
                        return cachedGpShaders;
                    }
                }
            }

            TranslatorContext[] shaderContexts = new TranslatorContext[Constants.ShaderStages];

            TransformFeedbackDescriptor[] tfd = GetTransformFeedbackDescriptors(state);

            TranslationFlags flags = DefaultFlags;

            if (tfd != null)
            {
                flags |= TranslationFlags.Feedback;
            }

            TranslationCounts counts = new TranslationCounts();

            if (addresses.VertexA != 0)
            {
                shaderContexts[0] = DecodeGraphicsShader(state, counts, flags, ShaderStage.Vertex, addresses.Vertex, addresses.VertexA);
            }
            else
            {
                shaderContexts[0] = DecodeGraphicsShader(state, counts, flags, ShaderStage.Vertex, addresses.Vertex);
            }

            shaderContexts[1] = DecodeGraphicsShader(state, counts, flags, ShaderStage.TessellationControl, addresses.TessControl);
            shaderContexts[2] = DecodeGraphicsShader(state, counts, flags, ShaderStage.TessellationEvaluation, addresses.TessEvaluation);
            shaderContexts[3] = DecodeGraphicsShader(state, counts, flags, ShaderStage.Geometry, addresses.Geometry);
            shaderContexts[4] = DecodeGraphicsShader(state, counts, flags, ShaderStage.Fragment, addresses.Fragment);

            bool isShaderCacheEnabled = _cacheManager != null;
            bool isShaderCacheReadOnly = false;

            Hash128 programCodeHash = default;
            GuestShaderCacheEntry[] shaderCacheEntries = null;

            if (isShaderCacheEnabled)
            {
                isShaderCacheReadOnly = _cacheManager.IsReadOnly;

                // Compute hash and prepare data for shader disk cache comparison.
                shaderCacheEntries = CacheHelper.CreateShaderCacheEntries(_context.MemoryManager, shaderContexts);
                programCodeHash = CacheHelper.ComputeGuestHashFromCache(shaderCacheEntries, tfd);
            }

            ShaderBundle gpShaders;

            // Search for the program hash in loaded shaders.
            if (!isShaderCacheEnabled || !_gpProgramsDiskCache.TryGetValue(programCodeHash, out gpShaders))
            {
                if (isShaderCacheEnabled)
                {
                    Logger.Debug?.Print(LogClass.Gpu, $"Shader {programCodeHash} not in cache, compiling!");
                }

                // The shader isn't currently cached, translate it and compile it.
                ShaderCodeHolder[] shaders = new ShaderCodeHolder[Constants.ShaderStages];

                shaders[0] = TranslateShader(shaderContexts[0]);
                shaders[1] = TranslateShader(shaderContexts[1]);
                shaders[2] = TranslateShader(shaderContexts[2]);
                shaders[3] = TranslateShader(shaderContexts[3]);
                shaders[4] = TranslateShader(shaderContexts[4]);

                List<IShader> hostShaders = new List<IShader>();

                for (int stage = 0; stage < Constants.ShaderStages; stage++)
                {
                    ShaderProgram program = shaders[stage]?.Program;

                    if (program == null)
                    {
                        continue;
                    }

                    IShader hostShader = _context.Renderer.CompileShader(program.Stage, program.Code);

                    shaders[stage].HostShader = hostShader;

                    hostShaders.Add(hostShader);
                }

                IProgram hostProgram = _context.Renderer.CreateProgram(hostShaders.ToArray(), tfd);

                byte[] hostProgramBinary = HostShaderCacheEntry.Create(hostProgram.GetBinary(), shaders);

                gpShaders = new ShaderBundle(hostProgram, shaders);

                if (isShaderCacheEnabled)
                {
                    _gpProgramsDiskCache.Add(programCodeHash, gpShaders);

                    if (!isShaderCacheReadOnly)
                    {
                        _cacheManager.SaveProgram(ref programCodeHash, CacheHelper.CreateGuestProgramDump(shaderCacheEntries, tfd), hostProgramBinary);
                    }
                }
            }

            if (!isCached)
            {
                list = new List<ShaderBundle>();

                _gpPrograms.Add(addresses, list);
            }

            list.Add(gpShaders);

            return gpShaders;
        }

        /// <summary>
        /// Gets transform feedback state from the current GPU state.
        /// </summary>
        /// <param name="state">Current GPU state</param>
        /// <returns>Four transform feedback descriptors for the enabled TFBs, or null if TFB is disabled</returns>
        private TransformFeedbackDescriptor[] GetTransformFeedbackDescriptors(GpuState state)
        {
            bool tfEnable = state.Get<Boolean32>(MethodOffset.TfEnable);

            if (!tfEnable)
            {
                return null;
            }

            TransformFeedbackDescriptor[] descs = new TransformFeedbackDescriptor[Constants.TotalTransformFeedbackBuffers];

            for (int i = 0; i < Constants.TotalTransformFeedbackBuffers; i++)
            {
                var tf = state.Get<TfState>(MethodOffset.TfState, i);

                int length = (int)Math.Min((uint)tf.VaryingsCount, 0x80);

                var varyingLocations = state.GetSpan(MethodOffset.TfVaryingLocations + i * 0x80, length).ToArray();

                descs[i] = new TransformFeedbackDescriptor(tf.BufferIndex, tf.Stride, varyingLocations);
            }

            return descs;
        }

        /// <summary>
        /// Checks if compute shader code in memory is equal to the cached shader.
        /// </summary>
        /// <param name="cpShader">Cached compute shader</param>
        /// <param name="gpuVa">GPU virtual address of the shader code in memory</param>
        /// <returns>True if the code is different, false otherwise</returns>
        private bool IsShaderEqual(ShaderBundle cpShader, ulong gpuVa)
        {
            return IsShaderEqual(cpShader.Shaders[0], gpuVa);
        }

        /// <summary>
        /// Checks if graphics shader code from all stages in memory are equal to the cached shaders.
        /// </summary>
        /// <param name="gpShaders">Cached graphics shaders</param>
        /// <param name="addresses">GPU virtual addresses of all enabled shader stages</param>
        /// <returns>True if the code is different, false otherwise</returns>
        private bool IsShaderEqual(ShaderBundle gpShaders, ShaderAddresses addresses)
        {
            for (int stage = 0; stage < gpShaders.Shaders.Length; stage++)
            {
                ShaderCodeHolder shader = gpShaders.Shaders[stage];

                ulong gpuVa = 0;

                switch (stage)
                {
                    case 0: gpuVa = addresses.Vertex;         break;
                    case 1: gpuVa = addresses.TessControl;    break;
                    case 2: gpuVa = addresses.TessEvaluation; break;
                    case 3: gpuVa = addresses.Geometry;       break;
                    case 4: gpuVa = addresses.Fragment;       break;
                }

                if (!IsShaderEqual(shader, gpuVa, addresses.VertexA))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if the code of the specified cached shader is different from the code in memory.
        /// </summary>
        /// <param name="shader">Cached shader to compare with</param>
        /// <param name="gpuVa">GPU virtual address of the binary shader code</param>
        /// <param name="gpuVaA">Optional GPU virtual address of the "Vertex A" binary shader code</param>
        /// <returns>True if the code is different, false otherwise</returns>
        private bool IsShaderEqual(ShaderCodeHolder shader, ulong gpuVa, ulong gpuVaA = 0)
        {
            if (shader == null)
            {
                return true;
            }

            ReadOnlySpan<byte> memoryCode = _context.MemoryManager.GetSpan(gpuVa, shader.Code.Length);

            bool equals = memoryCode.SequenceEqual(shader.Code);

            if (equals && shader.Code2 != null)
            {
                memoryCode = _context.MemoryManager.GetSpan(gpuVaA, shader.Code2.Length);

                equals = memoryCode.SequenceEqual(shader.Code2);
            }

            return equals;
        }

        /// <summary>
        /// Decode the binary Maxwell shader code to a translator context.
        /// </summary>
        /// <param name="state">Current GPU state</param>
        /// <param name="gpuVa">GPU virtual address of the binary shader code</param>
        /// <param name="localSizeX">Local group size X of the computer shader</param>
        /// <param name="localSizeY">Local group size Y of the computer shader</param>
        /// <param name="localSizeZ">Local group size Z of the computer shader</param>
        /// <param name="localMemorySize">Local memory size of the compute shader</param>
        /// <param name="sharedMemorySize">Shared memory size of the compute shader</param>
        /// <returns>The generated translator context</returns>
        private TranslatorContext DecodeComputeShader(
            GpuState state,
            ulong gpuVa,
            int localSizeX,
            int localSizeY,
            int localSizeZ,
            int localMemorySize,
            int sharedMemorySize)
        {
            if (gpuVa == 0)
            {
                return null;
            }

            GpuAccessor gpuAccessor = new GpuAccessor(_context, state, localSizeX, localSizeY, localSizeZ, localMemorySize, sharedMemorySize);

            return Translator.CreateContext(gpuVa, gpuAccessor, DefaultFlags | TranslationFlags.Compute);
        }

        /// <summary>
        /// Decode the binary Maxwell shader code to a translator context.
        /// </summary>
        /// <remarks>
        /// This will combine the "Vertex A" and "Vertex B" shader stages, if specified, into one shader.
        /// </remarks>
        /// <param name="state">Current GPU state</param>
        /// <param name="counts">Cumulative shader resource counts</param>
        /// <param name="flags">Flags that controls shader translation</param>
        /// <param name="stage">Shader stage</param>
        /// <param name="gpuVa">GPU virtual address of the shader code</param>
        /// <param name="gpuVaA">Optional GPU virtual address of the "Vertex A" shader code</param>
        /// <returns>The generated translator context</returns>
        private TranslatorContext DecodeGraphicsShader(
            GpuState state,
            TranslationCounts counts,
            TranslationFlags flags,
            ShaderStage stage,
            ulong gpuVa,
            ulong gpuVaA = 0)
        {
            if (gpuVa == 0)
            {
                return null;
            }

            GpuAccessor gpuAccessor = new GpuAccessor(_context, state, (int)stage - 1);

            if (gpuVaA != 0)
            {
                return Translator.CreateContext(gpuVaA, gpuVa, gpuAccessor, flags, counts);
            }
            else
            {
                return Translator.CreateContext(gpuVa, gpuAccessor, flags, counts);
            }
        }

        /// <summary>
        /// Translates a previously generated translator context to something that the host API accepts.
        /// </summary>
        /// <param name="translatorContext">Current translator context to translate</param>
        /// <returns>Compiled graphics shader code</returns>
        private ShaderCodeHolder TranslateShader(TranslatorContext translatorContext)
        {
            if (translatorContext == null)
            {
                return null;
            }

            if (translatorContext.AddressA != 0)
            {
                byte[] codeA = _context.MemoryManager.GetSpan(translatorContext.AddressA, translatorContext.SizeA).ToArray();
                byte[] codeB = _context.MemoryManager.GetSpan(translatorContext.Address, translatorContext.Size).ToArray();

                _dumper.Dump(codeA, compute: false, out string fullPathA, out string codePathA);
                _dumper.Dump(codeB, compute: false, out string fullPathB, out string codePathB);

                ShaderProgram program = translatorContext.Translate(out ShaderProgramInfo shaderProgramInfo);

                if (fullPathA != null && fullPathB != null && codePathA != null && codePathB != null)
                {
                    program.Prepend("// " + codePathB);
                    program.Prepend("// " + fullPathB);
                    program.Prepend("// " + codePathA);
                    program.Prepend("// " + fullPathA);
                }

                return new ShaderCodeHolder(program, shaderProgramInfo, codeB, codeA);
            }
            else
            {
                byte[] code = _context.MemoryManager.GetSpan(translatorContext.Address, translatorContext.Size).ToArray();

                _dumper.Dump(code, compute: false, out string fullPath, out string codePath);

                ShaderProgram program = translatorContext.Translate(out ShaderProgramInfo shaderProgramInfo);

                if (fullPath != null && codePath != null)
                {
                    program.Prepend("// " + codePath);
                    program.Prepend("// " + fullPath);
                }

                return new ShaderCodeHolder(program, shaderProgramInfo, code);
            }
        }

        /// <summary>
        /// Disposes the shader cache, deleting all the cached shaders.
        /// It's an error to use the shader cache after disposal.
        /// </summary>
        public void Dispose()
        {
            foreach (List<ShaderBundle> list in _cpPrograms.Values)
            {
                foreach (ShaderBundle bundle in list)
                {
                    bundle.Dispose();
                }
            }

            foreach (List<ShaderBundle> list in _gpPrograms.Values)
            {
                foreach (ShaderBundle bundle in list)
                {
                    bundle.Dispose();
                }
            }

            _cacheManager?.Dispose();
        }
    }
}