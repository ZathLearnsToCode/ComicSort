using ComicSort.Engine.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ComicSort.Engine.Services
{
    /// <summary>
    /// Immutable-ish snapshot arrays + parallel search blobs.
    /// ViewModels/services can rebuild or append, but consumers only read snapshots.
    /// </summary>
    public sealed class LibraryIndex
    {
        private ComicBook[] _books = Array.Empty<ComicBook>();
        private string[] _nameBlobsLower = Array.Empty<string>();

        public ComicBook[] Books => _books;
        public string[] NameBlobsLower => _nameBlobsLower;

        /// <summary>How many books have been indexed (useful for append-from-library scenarios).</summary>
        public int IndexedCount { get; private set; }

        public void Rebuild(IEnumerable<ComicBook> books)
        {
            if (books is null) throw new ArgumentNullException(nameof(books));

            _books = books.ToArray();
            _nameBlobsLower = new string[_books.Length];

            for (int i = 0; i < _books.Length; i++)
            {
                var b = _books[i];
                _nameBlobsLower[i] = (b.FileName ?? string.Empty).ToLowerInvariant();
            }

            IndexedCount = _books.Length;
        }

        /// <summary>
        /// Append newly discovered books to the snapshot/index.
        /// Caller is responsible for only passing new items.
        /// </summary>
        public void Append(IEnumerable<ComicBook> newBooks)
        {
            if (newBooks is null) throw new ArgumentNullException(nameof(newBooks));

            // materialize once
            var added = newBooks as ComicBook[] ?? newBooks.ToArray();
            if (added.Length == 0) return;

            int oldLen = _books.Length;
            int newLen = oldLen + added.Length;

            var mergedBooks = new ComicBook[newLen];
            Array.Copy(_books, mergedBooks, oldLen);
            Array.Copy(added, 0, mergedBooks, oldLen, added.Length);

            var mergedBlobs = new string[newLen];
            Array.Copy(_nameBlobsLower, mergedBlobs, oldLen);

            for (int i = 0; i < added.Length; i++)
            {
                var b = added[i];
                mergedBlobs[oldLen + i] = (b.FileName ?? string.Empty).ToLowerInvariant();
            }

            _books = mergedBooks;
            _nameBlobsLower = mergedBlobs;
            IndexedCount = newLen;
        }
    }
}
