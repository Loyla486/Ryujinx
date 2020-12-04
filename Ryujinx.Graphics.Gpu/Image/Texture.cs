using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.Cpu.Tracking;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Texture;
using Ryujinx.Graphics.Texture.Astc;
using Ryujinx.Memory.Range;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Ryujinx.Graphics.Gpu.Image
{
    /// <summary>
    /// Represents a cached GPU texture.
    /// </summary>
    class Texture : IRange, IDisposable
    {
        // How many updates we need before switching to the byte-by-byte comparison
        // modification check method.
        // This method uses much more memory so we want to avoid it if possible.
        private const int ByteComparisonSwitchThreshold = 4;

        private GpuContext _context;

        private SizeInfo _sizeInfo;

        /// <summary>
        /// Texture format.
        /// </summary>
        public Format Format => Info.FormatInfo.Format;

        /// <summary>
        /// Texture target.
        /// </summary>
        public Target Target { get; private set; }

        /// <summary>
        /// Texture information.
        /// </summary>
        public TextureInfo Info { get; private set; }

        /// <summary>
        /// Host scale factor.
        /// </summary>
        public float ScaleFactor { get; private set; }

        /// <summary>
        /// Upscaling mode. Informs if a texture is scaled, or is eligible for scaling.
        /// </summary>
        public TextureScaleMode ScaleMode { get; private set; }

        /// <summary>
        /// Set when a texture has been modified by the Host GPU since it was last flushed.
        /// </summary>
        public bool IsModified { get; internal set; }

        /// <summary>
        /// Set when a texture has been changed size. This indicates that it may need to be
        /// changed again when obtained as a sampler.
        /// </summary>
        public bool ChangedSize { get; internal set; }

        private int _depth;
        private int _layers;
        private int _firstLayer;
        private int _firstLevel;

        private bool _hasData;
        private int _updateCount;
        private byte[] _currentData;

        private ITexture _arrayViewTexture;
        private Target   _arrayViewTarget;

        private ITexture _flushHostTexture;

        private Texture _viewStorage;

        private List<Texture> _views;

        /// <summary>
        /// Host texture.
        /// </summary>
        public ITexture HostTexture { get; private set; }

        /// <summary>
        /// Intrusive linked list node used on the auto deletion texture cache.
        /// </summary>
        public LinkedListNode<Texture> CacheNode { get; set; }

        /// <summary>
        /// Event to fire when texture data is disposed.
        /// </summary>
        public event Action<Texture> Disposed;

        /// <summary>
        /// Start address of the texture in guest memory.
        /// </summary>
        public ulong Address => Info.Address;

        /// <summary>
        /// End address of the texture in guest memory.
        /// </summary>
        public ulong EndAddress => Info.Address + Size;

        /// <summary>
        /// Texture size in bytes.
        /// </summary>
        public ulong Size => (ulong)_sizeInfo.TotalSize;

        private CpuRegionHandle _memoryTracking;

        private int _referenceCount;

        /// <summary>
        /// Constructs a new instance of the cached GPU texture.
        /// </summary>
        /// <param name="context">GPU context that the texture belongs to</param>
        /// <param name="info">Texture information</param>
        /// <param name="sizeInfo">Size information of the texture</param>
        /// <param name="firstLayer">The first layer of the texture, or 0 if the texture has no parent</param>
        /// <param name="firstLevel">The first mipmap level of the texture, or 0 if the texture has no parent</param>
        /// <param name="scaleFactor">The floating point scale factor to initialize with</param>
        /// <param name="scaleMode">The scale mode to initialize with</param>
        private Texture(
            GpuContext       context,
            TextureInfo      info,
            SizeInfo         sizeInfo,
            int              firstLayer,
            int              firstLevel,
            float            scaleFactor,
            TextureScaleMode scaleMode)
        {
            InitializeTexture(context, info, sizeInfo);

            _firstLayer = firstLayer;
            _firstLevel = firstLevel;

            ScaleFactor = scaleFactor;
            ScaleMode = scaleMode;

            InitializeData(true);
        }

        /// <summary>
        /// Constructs a new instance of the cached GPU texture.
        /// </summary>
        /// <param name="context">GPU context that the texture belongs to</param>
        /// <param name="info">Texture information</param>
        /// <param name="sizeInfo">Size information of the texture</param>
        /// <param name="scaleMode">The scale mode to initialize with. If scaled, the texture's data is loaded immediately and scaled up</param>
        public Texture(GpuContext context, TextureInfo info, SizeInfo sizeInfo, TextureScaleMode scaleMode)
        {
            ScaleFactor = 1f; // Texture is first loaded at scale 1x.
            ScaleMode = scaleMode;

            InitializeTexture(context, info, sizeInfo);
        }

        /// <summary>
        /// Common texture initialization method.
        /// This sets the context, info and sizeInfo fields.
        /// Other fields are initialized with their default values.
        /// </summary>
        /// <param name="context">GPU context that the texture belongs to</param>
        /// <param name="info">Texture information</param>
        /// <param name="sizeInfo">Size information of the texture</param>
        private void InitializeTexture(GpuContext context, TextureInfo info, SizeInfo sizeInfo)
        {
            _context  = context;
            _sizeInfo = sizeInfo;

            SetInfo(info);

            _viewStorage = this;

            _views = new List<Texture>();
        }

        /// <summary>
        /// Initializes the data for a texture. Can optionally initialize the texture with or without data.
        /// If the texture is a view, it will initialize memory tracking to be non-dirty.
        /// </summary>
        /// <param name="isView">True if the texture is a view, false otherwise</param>
        /// <param name="withData">True if the texture is to be initialized with data</param>
        public void InitializeData(bool isView, bool withData = false)
        {
            _memoryTracking = _context.PhysicalMemory.BeginTracking(Address, Size);

            if (withData)
            {
                Debug.Assert(!isView);

                TextureCreateInfo createInfo = TextureManager.GetCreateInfo(Info, _context.Capabilities, ScaleFactor);
                HostTexture = _context.Renderer.CreateTexture(createInfo, ScaleFactor);

                SynchronizeMemory(); // Load the data.
                if (ScaleMode == TextureScaleMode.Scaled)
                {
                    SetScale(GraphicsConfig.ResScale); // Scale the data up.
                }
            }
            else
            {
                // Don't update this texture the next time we synchronize.
                ConsumeModified();
                _hasData = true;

                if (!isView)
                {
                    if (ScaleMode == TextureScaleMode.Scaled)
                    {
                        // Don't need to start at 1x as there is no data to scale, just go straight to the target scale.
                        ScaleFactor = GraphicsConfig.ResScale;
                    }

                    TextureCreateInfo createInfo = TextureManager.GetCreateInfo(Info, _context.Capabilities, ScaleFactor);
                    HostTexture = _context.Renderer.CreateTexture(createInfo, ScaleFactor);
                }
            }
        }

        /// <summary>
        /// Create a texture view from this texture.
        /// A texture view is defined as a child texture, from a sub-range of their parent texture.
        /// For example, the initial layer and mipmap level of the view can be defined, so the texture
        /// will start at the given layer/level of the parent texture.
        /// </summary>
        /// <param name="info">Child texture information</param>
        /// <param name="sizeInfo">Child texture size information</param>
        /// <param name="firstLayer">Start layer of the child texture on the parent texture</param>
        /// <param name="firstLevel">Start mipmap level of the child texture on the parent texture</param>
        /// <returns>The child texture</returns>
        public Texture CreateView(TextureInfo info, SizeInfo sizeInfo, int firstLayer, int firstLevel)
        {
            Texture texture = new Texture(
                _context,
                info,
                sizeInfo,
                _firstLayer + firstLayer,
                _firstLevel + firstLevel,
                ScaleFactor,
                ScaleMode);

            TextureCreateInfo createInfo = TextureManager.GetCreateInfo(info, _context.Capabilities, ScaleFactor);
            texture.HostTexture = HostTexture.CreateView(createInfo, firstLayer, firstLevel);

            _viewStorage.AddView(texture);

            return texture;
        }

        /// <summary>
        /// Adds a child texture to this texture.
        /// </summary>
        /// <param name="texture">The child texture</param>
        private void AddView(Texture texture)
        {
            DisableMemoryTracking();

            _views.Add(texture);

            texture._viewStorage = this;
        }

        /// <summary>
        /// Removes a child texture from this texture.
        /// </summary>
        /// <param name="texture">The child texture</param>
        private void RemoveView(Texture texture)
        {
            _views.Remove(texture);

            texture._viewStorage = texture;

            DeleteIfNotUsed();
        }

        /// <summary>
        /// Changes the texture size.
        /// </summary>
        /// <remarks>
        /// This operation may also change the size of all mipmap levels, including from the parent
        /// and other possible child textures, to ensure that all sizes are consistent.
        /// </remarks>
        /// <param name="width">The new texture width</param>
        /// <param name="height">The new texture height</param>
        /// <param name="depthOrLayers">The new texture depth (for 3D textures) or layers (for layered textures)</param>
        public void ChangeSize(int width, int height, int depthOrLayers)
        {
            int blockWidth = Info.FormatInfo.BlockWidth;
            int blockHeight = Info.FormatInfo.BlockHeight;

            width  <<= _firstLevel;
            height <<= _firstLevel;

            if (Target == Target.Texture3D)
            {
                depthOrLayers <<= _firstLevel;
            }
            else
            {
                depthOrLayers = _viewStorage.Info.DepthOrLayers;
            }

            _viewStorage.RecreateStorageOrView(width, height, blockWidth, blockHeight, depthOrLayers);

            foreach (Texture view in _viewStorage._views)
            {
                int viewWidth  = Math.Max(1, width  >> view._firstLevel);
                int viewHeight = Math.Max(1, height >> view._firstLevel);

                int viewDepthOrLayers;

                if (view.Info.Target == Target.Texture3D)
                {
                    viewDepthOrLayers = Math.Max(1, depthOrLayers >> view._firstLevel);
                }
                else
                {
                    viewDepthOrLayers = view.Info.DepthOrLayers;
                }

                view.RecreateStorageOrView(viewWidth, viewHeight, blockWidth, blockHeight, viewDepthOrLayers);
            }
        }

        /// <summary>
        /// Disables memory tracking on this texture. Currently used for view containers, as we assume their views are covering all memory regions.
        /// Textures with disabled memory tracking also cannot flush in most circumstances.
        /// </summary>
        public void DisableMemoryTracking()
        {
            _memoryTracking?.Dispose();
            _memoryTracking = null;
        }

        /// <summary>
        /// Recreates the texture storage (or view, in the case of child textures) of this texture.
        /// This allows recreating the texture with a new size.
        /// A copy is automatically performed from the old to the new texture.
        /// </summary>
        /// <param name="width">The new texture width</param>
        /// <param name="height">The new texture height</param>
        /// <param name="width">The block width related to the given width</param>
        /// <param name="height">The block height related to the given height</param>
        /// <param name="depthOrLayers">The new texture depth (for 3D textures) or layers (for layered textures)</param>
        private void RecreateStorageOrView(int width, int height, int blockWidth, int blockHeight, int depthOrLayers)
        {
            RecreateStorageOrView(
                BitUtils.DivRoundUp(width * Info.FormatInfo.BlockWidth, blockWidth),
                BitUtils.DivRoundUp(height * Info.FormatInfo.BlockHeight, blockHeight),
                depthOrLayers);
        }

        /// <summary>
        /// Recreates the texture storage (or view, in the case of child textures) of this texture.
        /// This allows recreating the texture with a new size.
        /// A copy is automatically performed from the old to the new texture.
        /// </summary>
        /// <param name="width">The new texture width</param>
        /// <param name="height">The new texture height</param>
        /// <param name="depthOrLayers">The new texture depth (for 3D textures) or layers (for layered textures)</param>
        private void RecreateStorageOrView(int width, int height, int depthOrLayers)
        {
            ChangedSize = true;

            SetInfo(new TextureInfo(
                Info.Address,
                width,
                height,
                depthOrLayers,
                Info.Levels,
                Info.SamplesInX,
                Info.SamplesInY,
                Info.Stride,
                Info.IsLinear,
                Info.GobBlocksInY,
                Info.GobBlocksInZ,
                Info.GobBlocksInTileX,
                Info.Target,
                Info.FormatInfo,
                Info.DepthStencilMode,
                Info.SwizzleR,
                Info.SwizzleG,
                Info.SwizzleB,
                Info.SwizzleA));

            TextureCreateInfo createInfo = TextureManager.GetCreateInfo(Info, _context.Capabilities, ScaleFactor);

            if (_viewStorage != this)
            {
                ReplaceStorage(_viewStorage.HostTexture.CreateView(createInfo, _firstLayer, _firstLevel));
            }
            else
            {
                ITexture newStorage = _context.Renderer.CreateTexture(createInfo, ScaleFactor);

                HostTexture.CopyTo(newStorage, 0, 0);

                ReplaceStorage(newStorage);
            }
        }

        /// <summary>
        /// Blacklists this texture from being scaled. Resets its scale to 1 if needed.
        /// </summary>
        public void BlacklistScale()
        {
            ScaleMode = TextureScaleMode.Blacklisted;
            SetScale(1f);
        }

        /// <summary>
        /// Propagates the scale between this texture and another to ensure they have the same scale.
        /// If one texture is blacklisted from scaling, the other will become blacklisted too.
        /// </summary>
        /// <param name="other">The other texture</param>
        public void PropagateScale(Texture other)
        {
            if (other.ScaleMode == TextureScaleMode.Blacklisted || ScaleMode == TextureScaleMode.Blacklisted)
            {
                BlacklistScale();
                other.BlacklistScale();
            }
            else
            {
                // Prefer the configured scale if present. If not, prefer the max.
                float targetScale = GraphicsConfig.ResScale;
                float sharedScale = (ScaleFactor == targetScale || other.ScaleFactor == targetScale) ? targetScale : Math.Max(ScaleFactor, other.ScaleFactor);

                SetScale(sharedScale);
                other.SetScale(sharedScale);
            }
        }

        /// <summary>
        /// Copy the host texture to a scaled one. If a texture is not provided, create it with the given scale.
        /// </summary>
        /// <param name="scale">Scale factor</param>
        /// <param name="storage">Texture to use instead of creating one</param>
        /// <returns>A host texture containing a scaled version of this texture</returns>
        private ITexture GetScaledHostTexture(float scale, ITexture storage = null)
        {
            if (storage == null)
            {
                TextureCreateInfo createInfo = TextureManager.GetCreateInfo(Info, _context.Capabilities, scale);
                storage = _context.Renderer.CreateTexture(createInfo, scale);
            }

            HostTexture.CopyTo(storage, new Extents2D(0, 0, HostTexture.Width, HostTexture.Height), new Extents2D(0, 0, storage.Width, storage.Height), true);

            return storage;
        }

        /// <summary>
        /// Sets the Scale Factor on this texture, and immediately recreates it at the correct size.
        /// When a texture is resized, a scaled copy is performed from the old texture to the new one, to ensure no data is lost.
        /// If scale is equivalent, this only propagates the blacklisted/scaled mode.
        /// If called on a view, its storage is resized instead.
        /// When resizing storage, all texture views are recreated.
        /// </summary>
        /// <param name="scale">The new scale factor for this texture</param>
        public void SetScale(float scale)
        {
            TextureScaleMode newScaleMode = ScaleMode == TextureScaleMode.Blacklisted ? ScaleMode : TextureScaleMode.Scaled;

            if (_viewStorage != this)
            {
                _viewStorage.ScaleMode = newScaleMode;
                _viewStorage.SetScale(scale);
                return;
            }

            if (ScaleFactor != scale)
            {
                Logger.Debug?.Print(LogClass.Gpu, $"Rescaling {Info.Width}x{Info.Height} {Info.FormatInfo.Format.ToString()} to ({ScaleFactor} to {scale}). ");

                ScaleFactor = scale;

                ITexture newStorage = GetScaledHostTexture(ScaleFactor);

                Logger.Debug?.Print(LogClass.Gpu, $"  Copy performed: {HostTexture.Width}x{HostTexture.Height} to {newStorage.Width}x{newStorage.Height}");

                ReplaceStorage(newStorage);

                // All views must be recreated against the new storage.

                foreach (var view in _views)
                {
                    Logger.Debug?.Print(LogClass.Gpu, $"  Recreating view {Info.Width}x{Info.Height} {Info.FormatInfo.Format.ToString()}.");
                    view.ScaleFactor = scale;

                    TextureCreateInfo viewCreateInfo = TextureManager.GetCreateInfo(view.Info, _context.Capabilities, scale);
                    ITexture newView = HostTexture.CreateView(viewCreateInfo, view._firstLayer - _firstLayer, view._firstLevel - _firstLevel);

                    view.ReplaceStorage(newView);
                    view.ScaleMode = newScaleMode;
                }
            }

            if (ScaleMode != newScaleMode)
            {
                ScaleMode = newScaleMode;

                foreach (var view in _views)
                {
                    view.ScaleMode = newScaleMode;
                }
            }
        }

        /// <summary>
        /// Checks if the memory for this texture was modified, and returns true if it was.
        /// The modified flags are consumed as a result.
        /// </summary>
        /// <remarks>
        /// If there is no memory tracking for this texture, it will always report as modified.
        /// </remarks>
        /// <returns>True if the texture was modified, false otherwise.</returns>
        public bool ConsumeModified()
        {
            bool wasDirty = _memoryTracking?.Dirty ?? true;

            _memoryTracking?.Reprotect();

            return wasDirty;
        }

        /// <summary>
        /// Synchronizes guest and host memory.
        /// This will overwrite the texture data with the texture data on the guest memory, if a CPU
        /// modification is detected.
        /// Be aware that this can cause texture data written by the GPU to be lost, this is just a
        /// one way copy (from CPU owned to GPU owned memory).
        /// </summary>
        public void SynchronizeMemory()
        {
            if (Target == Target.TextureBuffer)
            {
                return;
            }

            if (_hasData)
            {
                if (_memoryTracking?.Dirty != true)
                {
                    return;
                }

                BlacklistScale();
            }

            _memoryTracking?.Reprotect();

            ReadOnlySpan<byte> data = _context.PhysicalMemory.GetSpan(Address, (int)Size);

            IsModified = false;

            // If the host does not support ASTC compression, we need to do the decompression.
            // The decompression is slow, so we want to avoid it as much as possible.
            // This does a byte-by-byte check and skips the update if the data is equal in this case.
            // This improves the speed on applications that overwrites ASTC data without changing anything.
            if (Info.FormatInfo.Format.IsAstc() && !_context.Capabilities.SupportsAstcCompression)
            {
                if (_updateCount < ByteComparisonSwitchThreshold)
                {
                    _updateCount++;
                }
                else
                {
                    bool dataMatches = _currentData != null && data.SequenceEqual(_currentData);
                    _currentData = data.ToArray();
                    if (dataMatches)
                    {
                        return;
                    }
                }
            }

            data = ConvertToHostCompatibleFormat(data);

            HostTexture.SetData(data);

            _hasData = true;
        }

        public void SetData(ReadOnlySpan<byte> data)
        {
            BlacklistScale();

            _memoryTracking?.Reprotect();

            IsModified = false;

            HostTexture.SetData(data);

            _hasData = true;
        }

        /// <summary>
        /// Converts texture data to a format and layout that is supported by the host GPU.
        /// </summary>
        /// <param name="data">Data to be converted</param>
        /// <returns>Converted data</returns>
        private ReadOnlySpan<byte> ConvertToHostCompatibleFormat(ReadOnlySpan<byte> data)
        {
            if (Info.IsLinear)
            {
                data = LayoutConverter.ConvertLinearStridedToLinear(
                    Info.Width,
                    Info.Height,
                    Info.FormatInfo.BlockWidth,
                    Info.FormatInfo.BlockHeight,
                    Info.Stride,
                    Info.FormatInfo.BytesPerPixel,
                    data);
            }
            else
            {
                data = LayoutConverter.ConvertBlockLinearToLinear(
                    Info.Width,
                    Info.Height,
                    _depth,
                    Info.Levels,
                    _layers,
                    Info.FormatInfo.BlockWidth,
                    Info.FormatInfo.BlockHeight,
                    Info.FormatInfo.BytesPerPixel,
                    Info.GobBlocksInY,
                    Info.GobBlocksInZ,
                    Info.GobBlocksInTileX,
                    _sizeInfo,
                    data);
            }

            // Handle compressed cases not supported by the host:
            // - ASTC is usually not supported on desktop cards.
            // - BC4/BC5 is not supported on 3D textures.
            if (!_context.Capabilities.SupportsAstcCompression && Info.FormatInfo.Format.IsAstc())
            {
                if (!AstcDecoder.TryDecodeToRgba8(
                    data.ToArray(),
                    Info.FormatInfo.BlockWidth,
                    Info.FormatInfo.BlockHeight,
                    Info.Width,
                    Info.Height,
                    _depth,
                    Info.Levels,
                    _layers,
                    out Span<byte> decoded))
                {
                    string texInfo = $"{Info.Target} {Info.FormatInfo.Format} {Info.Width}x{Info.Height}x{Info.DepthOrLayers} levels {Info.Levels}";

                    Logger.Debug?.Print(LogClass.Gpu, $"Invalid ASTC texture at 0x{Info.Address:X} ({texInfo}).");
                }

                data = decoded;
            }
            else if (Target == Target.Texture3D && Info.FormatInfo.Format.IsBc4())
            {
                data = BCnDecoder.DecodeBC4(data, Info.Width, Info.Height, _depth, Info.Levels, _layers, Info.FormatInfo.Format == Format.Bc4Snorm);
            }
            else if (Target == Target.Texture3D && Info.FormatInfo.Format.IsBc5())
            {
                data = BCnDecoder.DecodeBC5(data, Info.Width, Info.Height, _depth, Info.Levels, _layers, Info.FormatInfo.Format == Format.Bc5Snorm);
            }

            return data;
        }

        /// <summary>
        /// Flushes the texture data.
        /// This causes the texture data to be written back to guest memory.
        /// If the texture was written by the GPU, this includes all modification made by the GPU
        /// up to this point.
        /// Be aware that this is an expensive operation, avoid calling it unless strictly needed.
        /// This may cause data corruption if the memory is already being used for something else on the CPU side.
        /// </summary>
        /// <param name="tracked">Whether or not the flush triggers write tracking. If it doesn't, the texture will not be blacklisted for scaling either.</param>
        public void Flush(bool tracked = true)
        {
            IsModified = false;
            if (TextureCompatibility.IsFormatHostIncompatible(Info, _context.Capabilities))
            {
                return; // Flushing this format is not supported, as it may have been converted to another host format.
            }

            if (tracked)
            {
                _context.PhysicalMemory.Write(Address, GetTextureDataFromGpu(tracked));
            }
            else
            {
                _context.PhysicalMemory.WriteUntracked(Address, GetTextureDataFromGpu(tracked));
            }
        }


        /// <summary>
        /// Flushes the texture data, to be called from an external thread.
        /// The host backend must ensure that we have shared access to the resource from this thread.
        /// This is used when flushing from memory access handlers.
        /// </summary>
        public void ExternalFlush(ulong address, ulong size)
        {
            if (!IsModified || _memoryTracking == null)
            {
                return;
            }

            _context.Renderer.BackgroundContextAction(() =>
            {
                IsModified = false;
                if (TextureCompatibility.IsFormatHostIncompatible(Info, _context.Capabilities))
                {
                    return; // Flushing this format is not supported, as it may have been converted to another host format.
                }

                ITexture texture = HostTexture;
                if (ScaleFactor != 1f)
                {
                    // If needed, create a texture to flush back to host at 1x scale.
                    texture = _flushHostTexture = GetScaledHostTexture(1f, _flushHostTexture);
                }

                _context.PhysicalMemory.WriteUntracked(Address, GetTextureDataFromGpu(false, texture));
            });
        }

        /// <summary>
        /// Gets data from the host GPU.
        /// </summary>
        /// <remarks>
        /// This method should be used to retrieve data that was modified by the host GPU.
        /// This is not cheap, avoid doing that unless strictly needed.
        /// </remarks>
        /// <returns>Host texture data</returns>
        private Span<byte> GetTextureDataFromGpu(bool blacklist, ITexture texture = null)
        {
            Span<byte> data;

            if (texture != null)
            {
                data = texture.GetData();
            }
            else
            {
                if (blacklist)
                {
                    BlacklistScale();
                    data = HostTexture.GetData();
                }
                else if (ScaleFactor != 1f)
                {
                    float scale = ScaleFactor;
                    SetScale(1f);
                    data = HostTexture.GetData();
                    SetScale(scale);
                }
                else
                {
                    data = HostTexture.GetData();
                }
            }

            if (Target != Target.TextureBuffer)
            {
                if (Info.IsLinear)
                {
                    data = LayoutConverter.ConvertLinearToLinearStrided(
                        Info.Width,
                        Info.Height,
                        Info.FormatInfo.BlockWidth,
                        Info.FormatInfo.BlockHeight,
                        Info.Stride,
                        Info.FormatInfo.BytesPerPixel,
                        data);
                }
                else
                {
                    data = LayoutConverter.ConvertLinearToBlockLinear(
                        Info.Width,
                        Info.Height,
                        _depth,
                        Info.Levels,
                        _layers,
                        Info.FormatInfo.BlockWidth,
                        Info.FormatInfo.BlockHeight,
                        Info.FormatInfo.BytesPerPixel,
                        Info.GobBlocksInY,
                        Info.GobBlocksInZ,
                        Info.GobBlocksInTileX,
                        _sizeInfo,
                        data);
                }
            }

            return data;
        }

        /// <summary>
        /// This performs a strict comparison, used to check if this texture is equal to the one supplied.
        /// </summary>
        /// <param name="info">Texture information to compare against</param>
        /// <param name="flags">Comparison flags</param>
        /// <returns>A value indicating how well this texture matches the given info</returns>
        public TextureMatchQuality IsExactMatch(TextureInfo info, TextureSearchFlags flags)
        {
            TextureMatchQuality matchQuality = TextureCompatibility.FormatMatches(Info, info, (flags & TextureSearchFlags.ForSampler) != 0, (flags & TextureSearchFlags.ForCopy) != 0);

            if (matchQuality == TextureMatchQuality.NoMatch)
            {
                return matchQuality;
            }

            if (!TextureCompatibility.LayoutMatches(Info, info))
            {
                return TextureMatchQuality.NoMatch;
            }

            if (!TextureCompatibility.SizeMatches(Info, info, (flags & TextureSearchFlags.Strict) == 0))
            {
                return TextureMatchQuality.NoMatch;
            }

            if ((flags & TextureSearchFlags.ForSampler) != 0 || (flags & TextureSearchFlags.Strict) != 0)
            {
                if (!TextureCompatibility.SamplerParamsMatches(Info, info))
                {
                    return TextureMatchQuality.NoMatch;
                }
            }

            if ((flags & TextureSearchFlags.ForCopy) != 0)
            {
                bool msTargetCompatible = Info.Target == Target.Texture2DMultisample && info.Target == Target.Texture2D;

                if (!msTargetCompatible && !TextureCompatibility.TargetAndSamplesCompatible(Info, info))
                {
                    return TextureMatchQuality.NoMatch;
                }
            }
            else if (!TextureCompatibility.TargetAndSamplesCompatible(Info, info))
            {
                return TextureMatchQuality.NoMatch;
            }

            return Info.Address == info.Address && Info.Levels == info.Levels ? matchQuality : TextureMatchQuality.NoMatch;
        }

        /// <summary>
        /// Check if it's possible to create a view, with the given parameters, from this texture.
        /// </summary>
        /// <param name="info">Texture view information</param>
        /// <param name="size">Texture view size</param>
        /// <param name="firstLayer">Texture view initial layer on this texture</param>
        /// <param name="firstLevel">Texture view first mipmap level on this texture</param>
        /// <returns>The level of compatiblilty a view with the given parameters created from this texture has</returns>
        public TextureViewCompatibility IsViewCompatible(
            TextureInfo info,
            ulong       size,
            out int     firstLayer,
            out int     firstLevel)
        {
            // Out of range.
            if (info.Address < Address || info.Address + size > EndAddress)
            {
                firstLayer = 0;
                firstLevel = 0;

                return TextureViewCompatibility.Incompatible;
            }

            int offset = (int)(info.Address - Address);

            if (!_sizeInfo.FindView(offset, (int)size, out firstLayer, out firstLevel))
            {
                return TextureViewCompatibility.Incompatible;
            }

            if (!TextureCompatibility.ViewLayoutCompatible(Info, info, firstLevel))
            {
                return TextureViewCompatibility.Incompatible;
            }

            if (!TextureCompatibility.ViewFormatCompatible(Info, info))
            {
                return TextureViewCompatibility.Incompatible;
            }

            TextureViewCompatibility result = TextureViewCompatibility.Full;

            result = TextureCompatibility.PropagateViewCompatibility(result, TextureCompatibility.ViewSizeMatches(Info, info, firstLevel));
            result = TextureCompatibility.PropagateViewCompatibility(result, TextureCompatibility.ViewTargetCompatible(Info, info));

            return (Info.SamplesInX == info.SamplesInX &&
                    Info.SamplesInY == info.SamplesInY) ? result : TextureViewCompatibility.Incompatible;
        }

        /// <summary>
        /// Gets a texture of the specified target type from this texture.
        /// This can be used to get an array texture from a non-array texture and vice-versa.
        /// If this texture and the requested targets are equal, then this texture Host texture is returned directly.
        /// </summary>
        /// <param name="target">The desired target type</param>
        /// <returns>A view of this texture with the requested target, or null if the target is invalid for this texture</returns>
        public ITexture GetTargetTexture(Target target)
        {
            if (target == Target)
            {
                return HostTexture;
            }

            if (_arrayViewTexture == null && IsSameDimensionsTarget(target))
            {
                TextureCreateInfo createInfo = new TextureCreateInfo(
                    Info.Width,
                    Info.Height,
                    target == Target.CubemapArray ? 6 : 1,
                    Info.Levels,
                    Info.Samples,
                    Info.FormatInfo.BlockWidth,
                    Info.FormatInfo.BlockHeight,
                    Info.FormatInfo.BytesPerPixel,
                    Info.FormatInfo.Format,
                    Info.DepthStencilMode,
                    target,
                    Info.SwizzleR,
                    Info.SwizzleG,
                    Info.SwizzleB,
                    Info.SwizzleA);

                ITexture viewTexture = HostTexture.CreateView(createInfo, 0, 0);

                _arrayViewTexture = viewTexture;
                _arrayViewTarget  = target;

                return viewTexture;
            }
            else if (_arrayViewTarget == target)
            {
                return _arrayViewTexture;
            }

            return null;
        }

        /// <summary>
        /// Check if this texture and the specified target have the same number of dimensions.
        /// For the purposes of this comparison, 2D and 2D Multisample textures are not considered to have
        /// the same number of dimensions. Same for Cubemap and 3D textures.
        /// </summary>
        /// <param name="target">The target to compare with</param>
        /// <returns>True if both targets have the same number of dimensions, false otherwise</returns>
        private bool IsSameDimensionsTarget(Target target)
        {
            switch (Info.Target)
            {
                case Target.Texture1D:
                case Target.Texture1DArray:
                    return target == Target.Texture1D ||
                           target == Target.Texture1DArray;

                case Target.Texture2D:
                case Target.Texture2DArray:
                    return target == Target.Texture2D ||
                           target == Target.Texture2DArray;

                case Target.Cubemap:
                case Target.CubemapArray:
                    return target == Target.Cubemap ||
                           target == Target.CubemapArray;

                case Target.Texture2DMultisample:
                case Target.Texture2DMultisampleArray:
                    return target == Target.Texture2DMultisample ||
                           target == Target.Texture2DMultisampleArray;

                case Target.Texture3D:
                    return target == Target.Texture3D;
            }

            return false;
        }

        /// <summary>
        /// Replaces view texture information.
        /// This should only be used for child textures with a parent.
        /// </summary>
        /// <param name="parent">The parent texture</param>
        /// <param name="info">The new view texture information</param>
        /// <param name="hostTexture">The new host texture</param>
        /// <param name="firstLayer">The first layer of the view</param>
        /// <param name="firstLevel">The first level of the view</param>
        public void ReplaceView(Texture parent, TextureInfo info, ITexture hostTexture, int firstLayer, int firstLevel)
        {
            parent._viewStorage.SynchronizeMemory();
            ReplaceStorage(hostTexture);

            _firstLayer = parent._firstLayer + firstLayer;
            _firstLevel = parent._firstLevel + firstLevel;
            parent._viewStorage.AddView(this);

            SetInfo(info);
        }

        /// <summary>
        /// Sets the internal texture information structure.
        /// </summary>
        /// <param name="info">The new texture information</param>
        private void SetInfo(TextureInfo info)
        {
            Info = info;
            Target = info.Target;

            _depth  = info.GetDepth();
            _layers = info.GetLayers();
        }

        /// <summary>
        /// Signals that the texture has been modified.
        /// </summary>
        public void SignalModified()
        {
            IsModified = true;

            if (_viewStorage != this)
            {
                _viewStorage.SignalModified();
            }

            _memoryTracking?.RegisterAction(ExternalFlush);
        }

        /// <summary>
        /// Replaces the host texture, while disposing of the old one if needed.
        /// </summary>
        /// <param name="hostTexture">The new host texture</param>
        private void ReplaceStorage(ITexture hostTexture)
        {
            DisposeTextures();

            HostTexture = hostTexture;
        }

        /// <summary>
        /// Checks if the texture overlaps with a memory range.
        /// </summary>
        /// <param name="address">Start address of the range</param>
        /// <param name="size">Size of the range</param>
        /// <returns>True if the texture overlaps with the range, false otherwise</returns>
        public bool OverlapsWith(ulong address, ulong size)
        {
            return Address < address + size && address < EndAddress;
        }

        /// <summary>
        /// Determine if any of our child textures are compaible as views of the given texture.
        /// </summary>
        /// <param name="texture">The texture to check against</param>
        /// <returns>True if any child is view compatible, false otherwise</returns>
        public bool HasViewCompatibleChild(Texture texture)
        {
            if (_viewStorage != this || _views.Count == 0)
            {
                return false;
            }

            foreach (Texture view in _views)
            {
                if (texture.IsViewCompatible(view.Info, view.Size, out int _, out int _) != TextureViewCompatibility.Incompatible)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Increments the texture reference count.
        /// </summary>
        public void IncrementReferenceCount()
        {
            _referenceCount++;
        }

        /// <summary>
        /// Decrements the texture reference count.
        /// When the reference count hits zero, the texture may be deleted and can't be used anymore.
        /// </summary>
        /// <returns>True if the texture is now referenceless, false otherwise</returns>
        public bool DecrementReferenceCount()
        {
            int newRefCount = --_referenceCount;

            if (newRefCount == 0)
            {
                if (_viewStorage != this)
                {
                    _viewStorage.RemoveView(this);
                }

                _context.Methods.TextureManager.RemoveTextureFromCache(this);
            }

            Debug.Assert(newRefCount >= 0);

            DeleteIfNotUsed();

            return newRefCount <= 0;
        }

        /// <summary>
        /// Delete the texture if it is not used anymore.
        /// The texture is considered unused when the reference count is zero,
        /// and it has no child views.
        /// </summary>
        private void DeleteIfNotUsed()
        {
            // We can delete the texture as long it is not being used
            // in any cache (the reference count is 0 in this case), and
            // also all views that may be created from this texture were
            // already deleted (views count is 0).
            if (_referenceCount == 0 && _views.Count == 0)
            {
                Dispose();
            }
        }

        /// <summary>
        /// Performs texture disposal, deleting the texture.
        /// </summary>
        private void DisposeTextures()
        {
            _currentData = null;
            HostTexture.Release();

            _arrayViewTexture?.Release();
            _arrayViewTexture = null;

            _flushHostTexture?.Release();
            _flushHostTexture = null;
        }

        /// <summary>
        /// Called when the memory for this texture has been unmapped.
        /// Calls are from non-gpu threads.
        /// </summary>
        public void Unmapped()
        {
            IsModified = false; // We shouldn't flush this texture, as its memory is no longer mapped.

            CpuRegionHandle tracking = _memoryTracking;
            tracking?.Reprotect();
            tracking?.RegisterAction(null);
        }

        /// <summary>
        /// Performs texture disposal, deleting the texture.
        /// </summary>
        public void Dispose()
        {
            DisposeTextures();

            Disposed?.Invoke(this);
            _memoryTracking?.Dispose();
        }
    }
}