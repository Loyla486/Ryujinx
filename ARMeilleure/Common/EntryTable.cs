﻿using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ARMeilleure.Common
{
    /// <summary>
    /// Represents an expandable table of the type <typeparamref name="TEntry"/>, whose entries will remain at the same
    /// address through out the table's lifetime.
    /// </summary>
    /// <typeparam name="TEntry">Type of the entry in the table</typeparam>
    class EntryTable<TEntry> where TEntry : unmanaged
    {
        private bool _disposed;
        private int _freeHint;
        private readonly int _pageCapacity; // Number of entries per page.
        private readonly int _pageLogCapacity;
        private readonly Dictionary<int, TEntry[]> _pages;
        private readonly BitMap _allocated;

        /// <summary>
        /// Initializes a new instance of the <see cref="EntryTable{TEntry}"/> class with the desired page size.
        /// </summary>
        /// <param name="pageSize">Desired page size</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> is less than 0</exception>
        /// <exception cref="ArgumentException"><typeparamref name="TEntry"/>'s size is zero</exception>
        /// <remarks>
        /// The actual page size may be smaller or larger depending on the size of <typeparamref name="TEntry"/>.
        /// </remarks>
        public unsafe EntryTable(int pageSize = 4096)
        {
            if (pageSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size cannot be negative.");
            }

            if (sizeof(TEntry) == 0)
            {
                throw new ArgumentException("Size of TEntry cannot be zero.");
            }

            _allocated = new BitMap();
            _pages = new Dictionary<int, TEntry[]>();
            _pageLogCapacity = BitOperations.Log2((uint)(pageSize / sizeof(TEntry)));
            _pageCapacity = 1 << _pageLogCapacity;
        }

        /// <summary>
        /// Allocates an entry in the <see cref="EntryTable{TEntry}"/>.
        /// </summary>
        /// <returns>Index of entry allocated in the table</returns>
        /// <exception cref="ObjectDisposedException"><see cref="EntryTable{TEntry}"/> instance was disposed</exception>
        public int Allocate()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(null);
            }

            lock (_allocated)
            {
                if (_allocated.IsSet(_freeHint))
                {
                    _freeHint = _allocated.FindFirstUnset();
                }

                int index = _freeHint++;
                var page = GetPage(index);

                _allocated.Set(index);

                GetValue(page, index) = default;

                return index;
            }
        }

        /// <summary>
        /// Frees the entry at the specified <paramref name="index"/>.
        /// </summary>
        /// <param name="index">Index of entry to free</param>
        /// <exception cref="ObjectDisposedException"><see cref="EntryTable{TEntry}"/> instance was disposed</exception>
        public void Free(int index)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(null);
            }

            lock (_allocated)
            {
                if (_allocated.IsSet(index))
                {
                    _allocated.Clear(index);

                    _freeHint = index;
                }
            }
        }

        /// <summary>
        /// Gets a reference to the entry at the specified allocated <paramref name="index"/>.
        /// </summary>
        /// <param name="index">Index of the entry</param>
        /// <returns>Reference to the entry at the specified <paramref name="index"/></returns>
        /// <exception cref="ObjectDisposedException"><see cref="EntryTable{TEntry}"/> instance was disposed</exception>
        /// <exception cref="ArgumentException">Entry at <paramref name="index"/> is not allocated</exception>
        public ref TEntry GetValue(int index)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(null);
            }

            Span<TEntry> page;

            lock (_allocated)
            {
                if (!_allocated.IsSet(index))
                {
                    throw new ArgumentException("Entry at the specified index was not allocated", nameof(index));
                }

                page = GetPage(index);
            }

            return ref GetValue(page, index);
        }

        /// <summary>
        /// Gets a reference to the entry at using the specified <paramref name="index"/> from the specified
        /// <paramref name="page"/>.
        /// </summary>
        /// <param name="page">Page to use</param>
        /// <param name="index">Index to use</param>
        /// <returns>Reference to the entry</returns>
        private ref TEntry GetValue(Span<TEntry> page, int index)
        {
            return ref page[index & (_pageCapacity - 1)];
        }

        /// <summary>
        /// Gets the page for the specified <see cref="index"/>.
        /// </summary>
        /// <param name="index">Index to use</param>
        /// <returns>Page for the specified <see cref="index"/></returns>
        private unsafe Span<TEntry> GetPage(int index)
        {
            var pageIndex = (int)((uint)(index & ~(_pageCapacity - 1)) >> _pageLogCapacity);

            if (!_pages.TryGetValue(pageIndex, out TEntry[] page))
            {
                page = GC.AllocateUninitializedArray<TEntry>(_pageCapacity, pinned: true);

                _pages.Add(pageIndex, page);
            }

            return page;
        }
    }
}
