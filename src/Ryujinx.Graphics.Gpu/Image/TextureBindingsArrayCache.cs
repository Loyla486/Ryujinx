using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Gpu.Engine.Types;
using Ryujinx.Graphics.Gpu.Memory;
using Ryujinx.Graphics.Shader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Ryujinx.Graphics.Gpu.Image
{
    /// <summary>
    /// Texture bindings array cache.
    /// </summary>
    class TextureBindingsArrayCache
    {
        /// <summary>
        /// Minimum timestamp delta until texture array can be removed from the cache.
        /// </summary>
        private const int MinDeltaForRemoval = 20000;

        private readonly GpuContext _context;
        private readonly GpuChannel _channel;
        private readonly bool _isCompute;

        /// <summary>
        /// Array cache entry key.
        /// </summary>
        private readonly struct CacheEntryKey : IEquatable<CacheEntryKey>
        {
            /// <summary>
            /// Whether the entry is for an image.
            /// </summary>
            public readonly bool IsImage;

            /// <summary>
            /// Texture or image target type.
            /// </summary>
            public readonly Target Target;

            /// <summary>
            /// Word offset of the first handle on the constant buffer.
            /// </summary>
            public readonly int HandleIndex;

            /// <summary>
            /// Number of entries of the array.
            /// </summary>
            public readonly int ArrayLength;

            private readonly TexturePool _texturePool;
            private readonly SamplerPool _samplerPool;

            private readonly BufferBounds _textureBufferBounds;

            /// <summary>
            /// Creates a new array cache entry.
            /// </summary>
            /// <param name="isImage">Whether the entry is for an image</param>
            /// <param name="bindingInfo">Binding information for the array</param>
            /// <param name="texturePool">Texture pool where the array textures are located</param>
            /// <param name="samplerPool">Sampler pool where the array samplers are located</param>
            /// <param name="textureBufferBounds">Constant buffer bounds with the texture handles</param>
            public CacheEntryKey(
                bool isImage,
                TextureBindingInfo bindingInfo,
                TexturePool texturePool,
                SamplerPool samplerPool,
                ref BufferBounds textureBufferBounds)
            {
                IsImage = isImage;
                Target = bindingInfo.Target;
                HandleIndex = bindingInfo.Handle;
                ArrayLength = bindingInfo.ArrayLength;

                _texturePool = texturePool;
                _samplerPool = samplerPool;

                _textureBufferBounds = textureBufferBounds;
            }

            /// <summary>
            /// Checks if the texture and sampler pools matches the cached pools.
            /// </summary>
            /// <param name="texturePool">Texture pool instance</param>
            /// <param name="samplerPool">Sampler pool instance</param>
            /// <returns>True if the pools match, false otherwise</returns>
            private bool MatchesPools(TexturePool texturePool, SamplerPool samplerPool)
            {
                return _texturePool == texturePool && _samplerPool == samplerPool;
            }

            /// <summary>
            /// Checks if the cached constant buffer address and size matches.
            /// </summary>
            /// <param name="textureBufferBounds">New buffer address and size</param>
            /// <returns>True if the address and size matches, false otherwise</returns>
            private bool MatchesBufferBounds(BufferBounds textureBufferBounds)
            {
                return _textureBufferBounds.Equals(textureBufferBounds);
            }

            public bool Equals(CacheEntryKey other)
            {
                return IsImage == other.IsImage &&
                    Target == other.Target &&
                    HandleIndex == other.HandleIndex &&
                    ArrayLength == other.ArrayLength &&
                    MatchesPools(other._texturePool, other._samplerPool) &&
                    MatchesBufferBounds(other._textureBufferBounds);
            }

            public override bool Equals(object obj)
            {
                return obj is CacheEntryKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return _textureBufferBounds.Range.GetHashCode();
            }
        }

        /// <summary>
        /// Array cache entry.
        /// </summary>
        private class CacheEntry
        {
            /// <summary>
            /// Key for this entry on the cache.
            /// </summary>
            public readonly CacheEntryKey Key;

            /// <summary>
            /// Linked list node used on the texture bindings array cache.
            /// </summary>
            public LinkedListNode<CacheEntry> CacheNode;

            /// <summary>
            /// Timestamp set on the last use of the array by the cache.
            /// </summary>
            public int CacheTimestamp;

            /// <summary>
            /// All cached textures, along with their invalidated sequence number as value.
            /// </summary>
            public readonly Dictionary<Texture, int> Textures;

            /// <summary>
            /// All pool texture IDs along with their textures.
            /// </summary>
            public readonly Dictionary<int, Texture> TextureIds;

            /// <summary>
            /// All pool sampler IDs along with their samplers.
            /// </summary>
            public readonly Dictionary<int, Sampler> SamplerIds;

            /// <summary>
            /// Backend texture array if the entry is for a texture, otherwise null.
            /// </summary>
            public readonly ITextureArray TextureArray;

            /// <summary>
            /// Backend image array if the entry is for an image, otherwise null.
            /// </summary>
            public readonly IImageArray ImageArray;

            private readonly TexturePool _texturePool;
            private readonly SamplerPool _samplerPool;

            private int _texturePoolSequence;
            private int _samplerPoolSequence;

            private int[] _cachedTextureBuffer;
            private int[] _cachedSamplerBuffer;

            private int _lastSequenceNumber;

            /// <summary>
            /// Creates a new array cache entry.
            /// </summary>
            /// <param name="key">Key for this entry on the cache</param>
            /// <param name="texturePool">Texture pool where the array textures are located</param>
            /// <param name="samplerPool">Sampler pool where the array samplers are located</param>
            private CacheEntry(ref CacheEntryKey key, TexturePool texturePool, SamplerPool samplerPool)
            {
                Key = key;
                Textures = new Dictionary<Texture, int>();
                TextureIds = new Dictionary<int, Texture>();
                SamplerIds = new Dictionary<int, Sampler>();

                _texturePool = texturePool;
                _samplerPool = samplerPool;

                _lastSequenceNumber = -1;
            }

            /// <summary>
            /// Creates a new array cache entry.
            /// </summary>
            /// <param name="key">Key for this entry on the cache</param>
            /// <param name="array">Backend texture array</param>
            /// <param name="texturePool">Texture pool where the array textures are located</param>
            /// <param name="samplerPool">Sampler pool where the array samplers are located</param>
            public CacheEntry(ref CacheEntryKey key, ITextureArray array, TexturePool texturePool, SamplerPool samplerPool) : this(ref key, texturePool, samplerPool)
            {
                TextureArray = array;
            }

            /// <summary>
            /// Creates a new array cache entry.
            /// </summary>
            /// <param name="key">Key for this entry on the cache</param>
            /// <param name="array">Backend image array</param>
            /// <param name="texturePool">Texture pool where the array textures are located</param>
            /// <param name="samplerPool">Sampler pool where the array samplers are located</param>
            public CacheEntry(ref CacheEntryKey key, IImageArray array, TexturePool texturePool, SamplerPool samplerPool) : this(ref key, texturePool, samplerPool)
            {
                ImageArray = array;
            }

            /// <summary>
            /// Synchronizes memory for all textures in the array.
            /// </summary>
            /// <param name="isStore">Indicates if the texture may be modified by the access</param>
            /// <param name="blacklistScale">Indicates if the texture should be blacklisted for scaling</param>
            public void SynchronizeMemory(bool isStore, bool blacklistScale)
            {
                foreach (Texture texture in Textures.Keys)
                {
                    texture.SynchronizeMemory();

                    if (isStore)
                    {
                        texture.SignalModified();
                    }

                    if (blacklistScale && texture.ScaleMode != TextureScaleMode.Blacklisted)
                    {
                        // Scaling textures used on arrays is currently not supported.

                        texture.BlacklistScale();
                    }
                }
            }

            /// <summary>
            /// Clears all cached texture instances.
            /// </summary>
            public void Reset()
            {
                Textures.Clear();
                TextureIds.Clear();
                SamplerIds.Clear();
            }

            /// <summary>
            /// Updates the cached constant buffer data.
            /// </summary>
            /// <param name="cachedTextureBuffer">Constant buffer data with the texture handles (and sampler handles, if they are combined)</param>
            /// <param name="cachedSamplerBuffer">Constant buffer data with the sampler handles</param>
            /// <param name="separateSamplerBuffer">Whether <paramref name="cachedTextureBuffer"/> and <paramref name="cachedSamplerBuffer"/> comes from different buffers</param>
            public void UpdateData(ReadOnlySpan<int> cachedTextureBuffer, ReadOnlySpan<int> cachedSamplerBuffer, bool separateSamplerBuffer)
            {
                _cachedTextureBuffer = cachedTextureBuffer.ToArray();
                _cachedSamplerBuffer = separateSamplerBuffer ? cachedSamplerBuffer.ToArray() : _cachedTextureBuffer;
            }

            /// <summary>
            /// Checks if any texture has been deleted since the last call to this method.
            /// </summary>
            /// <returns>True if one or more textures have been deleted, false otherwise</returns>
            public bool ValidateTextures()
            {
                foreach ((Texture texture, int invalidatedSequence) in Textures)
                {
                    if (texture.InvalidatedSequence != invalidatedSequence)
                    {
                        return false;
                    }
                }

                return true;
            }

            /// <summary>
            /// Checks if the cached texture or sampler pool has been modified since the last call to this method.
            /// </summary>
            /// <returns>True if any used entries of the pools might have been modified, false otherwise</returns>
            public bool PoolsModified()
            {
                bool texturePoolModified = _texturePool.WasModified(ref _texturePoolSequence);
                bool samplerPoolModified = _samplerPool.WasModified(ref _samplerPoolSequence);

                // If both pools were not modified since the last check, we have nothing else to check.
                if (!texturePoolModified && !samplerPoolModified)
                {
                    return false;
                }

                // If the pools were modified, let's check if any of the entries we care about changed.

                // Check if any of our cached textures changed on the pool.
                foreach ((int textureId, Texture texture) in TextureIds)
                {
                    if (_texturePool.GetCachedItem(textureId) != texture)
                    {
                        return true;
                    }
                }

                // Check if any of our cached samplers changed on the pool.
                foreach ((int samplerId, Sampler sampler) in SamplerIds)
                {
                    if (_samplerPool.GetCachedItem(samplerId) != sampler)
                    {
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// Checks if the sequence number matches the one used on the last call to this method.
            /// </summary>
            /// <param name="currentSequenceNumber">Current sequence number</param>
            /// <returns>True if the sequence numbers match, false otherwise</returns>
            public bool MatchesSequenceNumber(int currentSequenceNumber)
            {
                if (_lastSequenceNumber == currentSequenceNumber)
                {
                    return true;
                }

                _lastSequenceNumber = currentSequenceNumber;

                return false;
            }

            /// <summary>
            /// Checks if the buffer data matches the cached data.
            /// </summary>
            /// <param name="cachedTextureBuffer">New texture buffer data</param>
            /// <param name="cachedSamplerBuffer">New sampler buffer data</param>
            /// <param name="separateSamplerBuffer">Whether <paramref name="cachedTextureBuffer"/> and <paramref name="cachedSamplerBuffer"/> comes from different buffers</param>
            /// <param name="samplerWordOffset">Word offset of the sampler constant buffer handle that is used</param>
            /// <returns>True if the data matches, false otherwise</returns>
            public bool MatchesBufferData(
                ReadOnlySpan<int> cachedTextureBuffer,
                ReadOnlySpan<int> cachedSamplerBuffer,
                bool separateSamplerBuffer,
                int samplerWordOffset)
            {
                if (_cachedTextureBuffer != null && cachedTextureBuffer.Length > _cachedTextureBuffer.Length)
                {
                    cachedTextureBuffer = cachedTextureBuffer[.._cachedTextureBuffer.Length];
                }

                if (!_cachedTextureBuffer.AsSpan().SequenceEqual(cachedTextureBuffer))
                {
                    return false;
                }

                if (separateSamplerBuffer)
                {
                    if (_cachedSamplerBuffer == null ||
                        _cachedSamplerBuffer.Length <= samplerWordOffset ||
                        cachedSamplerBuffer.Length <= samplerWordOffset)
                    {
                        return false;
                    }

                    int oldValue = _cachedSamplerBuffer[samplerWordOffset];
                    int newValue = cachedSamplerBuffer[samplerWordOffset];

                    return oldValue == newValue;
                }

                return true;
            }
        }

        private readonly Dictionary<CacheEntryKey, CacheEntry> _cache;
        private readonly LinkedList<CacheEntry> _lruCache;

        private int _currentTimestamp;

        /// <summary>
        /// Creates a new instance of the texture bindings array cache.
        /// </summary>
        /// <param name="context">GPU context</param>
        /// <param name="channel">GPU channel</param>
        /// <param name="isCompute">Whether the bindings will be used for compute or graphics pipelines</param>
        public TextureBindingsArrayCache(GpuContext context, GpuChannel channel, bool isCompute)
        {
            _context = context;
            _channel = channel;
            _isCompute = isCompute;
            _cache = new Dictionary<CacheEntryKey, CacheEntry>();
            _lruCache = new LinkedList<CacheEntry>();
        }

        /// <summary>
        /// Updates a texture array bindings and textures.
        /// </summary>
        /// <param name="texturePool">Texture pool</param>
        /// <param name="samplerPool">Sampler pool</param>
        /// <param name="stage">Shader stage where the array is used</param>
        /// <param name="stageIndex">Shader stage index where the array is used</param>
        /// <param name="textureBufferIndex">Texture constant buffer index</param>
        /// <param name="samplerIndex">Sampler handles source</param>
        /// <param name="bindingInfo">Array binding information</param>
        public void UpdateTextureArray(
            TexturePool texturePool,
            SamplerPool samplerPool,
            ShaderStage stage,
            int stageIndex,
            int textureBufferIndex,
            SamplerIndex samplerIndex,
            TextureBindingInfo bindingInfo)
        {
            Update(texturePool, samplerPool, stage, stageIndex, textureBufferIndex, isImage: false, samplerIndex, bindingInfo);
        }

        /// <summary>
        /// Updates a image array bindings and textures.
        /// </summary>
        /// <param name="texturePool">Texture pool</param>
        /// <param name="stage">Shader stage where the array is used</param>
        /// <param name="stageIndex">Shader stage index where the array is used</param>
        /// <param name="textureBufferIndex">Texture constant buffer index</param>
        /// <param name="bindingInfo">Array binding information</param>
        public void UpdateImageArray(TexturePool texturePool, ShaderStage stage, int stageIndex, int textureBufferIndex, TextureBindingInfo bindingInfo)
        {
            Update(texturePool, null, stage, stageIndex, textureBufferIndex, isImage: true, SamplerIndex.ViaHeaderIndex, bindingInfo);
        }

        /// <summary>
        /// Updates a texture or image array bindings and textures.
        /// </summary>
        /// <param name="texturePool">Texture pool</param>
        /// <param name="samplerPool">Sampler pool</param>
        /// <param name="stage">Shader stage where the array is used</param>
        /// <param name="stageIndex">Shader stage index where the array is used</param>
        /// <param name="textureBufferIndex">Texture constant buffer index</param>
        /// <param name="isImage">Whether the array is a image or texture array</param>
        /// <param name="samplerIndex">Sampler handles source</param>
        /// <param name="bindingInfo">Array binding information</param>
        private void Update(
            TexturePool texturePool,
            SamplerPool samplerPool,
            ShaderStage stage,
            int stageIndex,
            int textureBufferIndex,
            bool isImage,
            SamplerIndex samplerIndex,
            TextureBindingInfo bindingInfo)
        {
            (textureBufferIndex, int samplerBufferIndex) = TextureHandle.UnpackSlots(bindingInfo.CbufSlot, textureBufferIndex);

            bool separateSamplerBuffer = textureBufferIndex != samplerBufferIndex;

            ref BufferBounds textureBufferBounds = ref _channel.BufferManager.GetUniformBufferBounds(_isCompute, stageIndex, textureBufferIndex);
            ref BufferBounds samplerBufferBounds = ref _channel.BufferManager.GetUniformBufferBounds(_isCompute, stageIndex, samplerBufferIndex);

            CacheEntry entry = GetOrAddEntry(
                texturePool,
                samplerPool,
                bindingInfo,
                isImage,
                ref textureBufferBounds,
                out bool isNewEntry);

            bool poolsModified = entry.PoolsModified();
            bool isStore = bindingInfo.Flags.HasFlag(TextureUsageFlags.ImageStore);
            bool resScaleUnsupported = bindingInfo.Flags.HasFlag(TextureUsageFlags.ResScaleUnsupported);

            ReadOnlySpan<int> cachedTextureBuffer;
            ReadOnlySpan<int> cachedSamplerBuffer;

            if (!poolsModified && !isNewEntry && entry.ValidateTextures())
            {
                if (entry.MatchesSequenceNumber(_context.SequenceNumber))
                {
                    entry.SynchronizeMemory(isStore, resScaleUnsupported);

                    if (isImage)
                    {
                        _context.Renderer.Pipeline.SetImageArray(stage, bindingInfo.Binding, entry.ImageArray);
                    }
                    else
                    {
                        _context.Renderer.Pipeline.SetTextureArray(stage, bindingInfo.Binding, entry.TextureArray);
                    }

                    return;
                }

                cachedTextureBuffer = MemoryMarshal.Cast<byte, int>(_channel.MemoryManager.Physical.GetSpan(textureBufferBounds.Range));

                if (separateSamplerBuffer)
                {
                    cachedSamplerBuffer = MemoryMarshal.Cast<byte, int>(_channel.MemoryManager.Physical.GetSpan(samplerBufferBounds.Range));
                }
                else
                {
                    cachedSamplerBuffer = cachedTextureBuffer;
                }

                (_, int samplerWordOffset, _) = TextureHandle.UnpackOffsets(bindingInfo.Handle);

                if (entry.MatchesBufferData(cachedTextureBuffer, cachedSamplerBuffer, separateSamplerBuffer, samplerWordOffset))
                {
                    entry.SynchronizeMemory(isStore, resScaleUnsupported);

                    if (isImage)
                    {
                        _context.Renderer.Pipeline.SetImageArray(stage, bindingInfo.Binding, entry.ImageArray);
                    }
                    else
                    {
                        _context.Renderer.Pipeline.SetTextureArray(stage, bindingInfo.Binding, entry.TextureArray);
                    }

                    return;
                }
            }
            else
            {
                cachedTextureBuffer = MemoryMarshal.Cast<byte, int>(_channel.MemoryManager.Physical.GetSpan(textureBufferBounds.Range));

                if (separateSamplerBuffer)
                {
                    cachedSamplerBuffer = MemoryMarshal.Cast<byte, int>(_channel.MemoryManager.Physical.GetSpan(samplerBufferBounds.Range));
                }
                else
                {
                    cachedSamplerBuffer = cachedTextureBuffer;
                }
            }

            if (!isNewEntry)
            {
                entry.Reset();
            }

            entry.UpdateData(cachedTextureBuffer, cachedSamplerBuffer, separateSamplerBuffer);

            Format[] formats = isImage ? new Format[bindingInfo.ArrayLength] : null;
            ISampler[] samplers = isImage ? null : new ISampler[bindingInfo.ArrayLength];
            ITexture[] textures = new ITexture[bindingInfo.ArrayLength];

            for (int index = 0; index < bindingInfo.ArrayLength; index++)
            {
                int handleIndex = bindingInfo.Handle + index * (Constants.TextureHandleSizeInBytes / sizeof(int));
                int packedId = TextureHandle.ReadPackedId(handleIndex, cachedTextureBuffer, cachedSamplerBuffer);
                int textureId = TextureHandle.UnpackTextureId(packedId);
                int samplerId;

                if (samplerIndex == SamplerIndex.ViaHeaderIndex)
                {
                    samplerId = textureId;
                }
                else
                {
                    samplerId = TextureHandle.UnpackSamplerId(packedId);
                }

                ref readonly TextureDescriptor descriptor = ref texturePool.GetForBinding(textureId, out Texture texture);

                if (texture != null)
                {
                    entry.Textures[texture] = texture.InvalidatedSequence;

                    if (isStore)
                    {
                        texture.SignalModified();
                    }

                    if (resScaleUnsupported && texture.ScaleMode != TextureScaleMode.Blacklisted)
                    {
                        // Scaling textures used on arrays is currently not supported.

                        texture.BlacklistScale();
                    }
                }

                Sampler sampler = samplerPool?.Get(samplerId);

                entry.TextureIds[textureId] = texture;
                entry.SamplerIds[samplerId] = sampler;

                ITexture hostTexture = texture?.GetTargetTexture(bindingInfo.Target);
                ISampler hostSampler = sampler?.GetHostSampler(texture);

                Format format = bindingInfo.Format;

                if (hostTexture != null && texture.Target == Target.TextureBuffer)
                {
                    // Ensure that the buffer texture is using the correct buffer as storage.
                    // Buffers are frequently re-created to accommodate larger data, so we need to re-bind
                    // to ensure we're not using a old buffer that was already deleted.
                    if (isImage)
                    {
                        if (format == 0 && texture != null)
                        {
                            format = texture.Format;
                        }

                        _channel.BufferManager.SetBufferTextureStorage(entry.ImageArray, hostTexture, texture.Range, bindingInfo, index, format);
                    }
                    else
                    {
                        _channel.BufferManager.SetBufferTextureStorage(entry.TextureArray, hostTexture, texture.Range, bindingInfo, index, format);
                    }
                }
                else if (isImage)
                {
                    if (format == 0 && texture != null)
                    {
                        format = texture.Format;
                    }

                    formats[index] = format;
                    textures[index] = hostTexture;
                }
                else
                {
                    samplers[index] = hostSampler;
                    textures[index] = hostTexture;
                }
            }

            if (isImage)
            {
                entry.ImageArray.SetFormats(0, formats);
                entry.ImageArray.SetImages(0, textures);

                _context.Renderer.Pipeline.SetImageArray(stage, bindingInfo.Binding, entry.ImageArray);
            }
            else
            {
                entry.TextureArray.SetSamplers(0, samplers);
                entry.TextureArray.SetTextures(0, textures);

                _context.Renderer.Pipeline.SetTextureArray(stage, bindingInfo.Binding, entry.TextureArray);
            }
        }

        /// <summary>
        /// Gets a cached texture entry, or creates a new one if not found.
        /// </summary>
        /// <param name="texturePool">Texture pool</param>
        /// <param name="samplerPool">Sampler pool</param>
        /// <param name="bindingInfo">Array binding information</param>
        /// <param name="isImage">Whether the array is a image or texture array</param>
        /// <param name="textureBufferBounds">Constant buffer bounds with the texture handles</param>
        /// <param name="isNew">Whether a new entry was created, or an existing one was returned</param>
        /// <returns>Cache entry</returns>
        private CacheEntry GetOrAddEntry(
            TexturePool texturePool,
            SamplerPool samplerPool,
            TextureBindingInfo bindingInfo,
            bool isImage,
            ref BufferBounds textureBufferBounds,
            out bool isNew)
        {
            CacheEntryKey key = new CacheEntryKey(
                isImage,
                bindingInfo,
                texturePool,
                samplerPool,
                ref textureBufferBounds);

            isNew = !_cache.TryGetValue(key, out CacheEntry entry);

            if (isNew)
            {
                int arrayLength = bindingInfo.ArrayLength;

                if (isImage)
                {
                    IImageArray array = _context.Renderer.CreateImageArray(arrayLength, bindingInfo.Target == Target.TextureBuffer);

                    _cache.Add(key, entry = new CacheEntry(ref key, array, texturePool, samplerPool));
                }
                else
                {
                    ITextureArray array = _context.Renderer.CreateTextureArray(arrayLength, bindingInfo.Target == Target.TextureBuffer);

                    _cache.Add(key, entry = new CacheEntry(ref key, array, texturePool, samplerPool));
                }
            }

            if (entry.CacheNode != null)
            {
                _lruCache.Remove(entry.CacheNode);
                _lruCache.AddLast(entry.CacheNode);
            }
            else
            {
                entry.CacheNode = _lruCache.AddLast(entry);
            }

            entry.CacheTimestamp = ++_currentTimestamp;

            RemoveLeastUsedEntries();

            return entry;
        }

        /// <summary>
        /// Remove entries from the cache that have not been used for some time.
        /// </summary>
        private void RemoveLeastUsedEntries()
        {
            LinkedListNode<CacheEntry> nextNode = _lruCache.First;

            while (nextNode != null && _currentTimestamp - nextNode.Value.CacheTimestamp >= MinDeltaForRemoval)
            {
                LinkedListNode<CacheEntry> toRemove = nextNode;
                nextNode = nextNode.Next;
                _cache.Remove(toRemove.Value.Key);
                _lruCache.Remove(toRemove);
            }
        }
    }
}
