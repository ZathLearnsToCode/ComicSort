using ComicSort.Engine.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace ComicSort.Engine.Services
{
    public sealed class SearchEngine
    {
        public ComicBook[] Search(
            ComicBook[] books,
            string[] nameBlobsLower,     // filename-only, lower; parallel to books
            string rawQuery,
            string extensionFilter,      // "All" or ".cbz"
            string sortMode,             // "Name" / "AddedOn" / "Size"
            bool sortDescending,
            CancellationToken ct)
        {
            rawQuery ??= "";
            var raw = rawQuery.Trim();

            var tokens = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // ---- operators ----
            long? minSize = null;
            long? maxSize = null;
            bool hasSizeFilter = false;

            DateTime? minAddedUtc = null;
            DateTime? maxAddedUtc = null;
            bool hasAddedFilter = false;

            string? extFromQuery = null;

            var normalTokens = new List<string>(tokens.Length);

            foreach (var t in tokens)
            {
                if (t.StartsWith("ext:", StringComparison.OrdinalIgnoreCase))
                {
                    var v = t.Substring(4).Trim();
                    if (v.StartsWith('.')) v = v[1..];
                    if (!string.IsNullOrWhiteSpace(v))
                        extFromQuery = "." + v.ToLowerInvariant();
                    continue;
                }

                if (t.StartsWith("size", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseSizeToken(t, out var min, out var max))
                    {
                        if (min.HasValue) minSize = minSize.HasValue ? Math.Max(minSize.Value, min.Value) : min.Value;
                        if (max.HasValue) maxSize = maxSize.HasValue ? Math.Min(maxSize.Value, max.Value) : max.Value;
                        hasSizeFilter = true;
                        continue;
                    }
                }

                if (t.StartsWith("added", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseAddedTokenUtc(t, out var min, out var max))
                    {
                        if (min.HasValue) minAddedUtc = minAddedUtc.HasValue ? (minAddedUtc.Value > min.Value ? minAddedUtc : min) : min;
                        if (max.HasValue) maxAddedUtc = maxAddedUtc.HasValue ? (maxAddedUtc.Value < max.Value ? maxAddedUtc : max) : max;
                        hasAddedFilter = true;
                        continue;
                    }
                }

                normalTokens.Add(t);
            }

            var tokensLower = normalTokens.Select(t => t.ToLowerInvariant()).ToArray();
            bool hasQuery = tokensLower.Length > 0;

            bool looksLikePath = normalTokens.Any(t => t.Contains('\\') || t.Contains(':') || t.Contains('/'));

            var extFilterEffective = extFromQuery ?? extensionFilter;
            bool filterByExt = !string.Equals(extFilterEffective, "All", StringComparison.OrdinalIgnoreCase);

            var list = new List<ComicBook>(Math.Min(5000, books.Length));

            for (int i = 0; i < books.Length; i++)
            {
                ct.ThrowIfCancellationRequested();

                var b = books[i];

                if (filterByExt &&
                    !string.Equals(b.Extension, extFilterEffective, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (hasSizeFilter)
                {
                    var sz = b.FileSize;
                    if (minSize.HasValue && sz < minSize.Value) continue;
                    if (maxSize.HasValue && sz > maxSize.Value) continue;
                }

                if (hasAddedFilter)
                {
                    var added = b.AddedOn; // UTC
                    if (minAddedUtc.HasValue && added < minAddedUtc.Value) continue;
                    if (maxAddedUtc.HasValue && added > maxAddedUtc.Value) continue;
                }

                if (hasQuery)
                {
                    if (!looksLikePath)
                    {
                        var blob = nameBlobsLower[i];

                        bool match = true;
                        for (int ti = 0; ti < tokensLower.Length; ti++)
                        {
                            if (!blob.Contains(tokensLower[ti]))
                            {
                                match = false;
                                break;
                            }
                        }

                        if (!match)
                            continue;
                    }
                    else
                    {
                        bool match = true;
                        for (int ti = 0; ti < normalTokens.Count; ti++)
                        {
                            var tok = normalTokens[ti];

                            bool inName = b.FileName?.Contains(tok, StringComparison.OrdinalIgnoreCase) ?? false;
                            bool inPath = b.FilePath?.Contains(tok, StringComparison.OrdinalIgnoreCase) ?? false;

                            if (!inName && !inPath)
                            {
                                match = false;
                                break;
                            }
                        }

                        if (!match)
                            continue;
                    }
                }

                list.Add(b);
            }

            IEnumerable<ComicBook> sorted = sortMode switch
            {
                "AddedOn" => sortDescending ? list.OrderByDescending(b => b.AddedOn) : list.OrderBy(b => b.AddedOn),
                "Size" => sortDescending ? list.OrderByDescending(b => b.FileSize) : list.OrderBy(b => b.FileSize),
                _ => sortDescending
                    ? list.OrderByDescending(b => b.FileName, StringComparer.OrdinalIgnoreCase)
                    : list.OrderBy(b => b.FileName, StringComparer.OrdinalIgnoreCase),
            };

            return sorted.ToArray();
        }

        // ======== Helpers: size / added operators ========

        private static bool TryParseSizeToken(string token, out long? min, out long? max)
        {
            min = null;
            max = null;

            token = token.Trim();
            if (!token.StartsWith("size", StringComparison.OrdinalIgnoreCase))
                return false;

            var rest = token.Substring(4).Trim();

            if (rest.StartsWith(":", StringComparison.Ordinal))
            {
                rest = rest.Substring(1).Trim();

                var dash = rest.IndexOf('-');
                if (dash >= 0)
                {
                    var a = rest.Substring(0, dash).Trim();
                    var b = rest.Substring(dash + 1).Trim();

                    if (!TryParseSize(a, out var amin)) return false;
                    if (!TryParseSize(b, out var bmax)) return false;

                    min = Math.Min(amin, bmax);
                    max = Math.Max(amin, bmax);
                    return true;
                }

                if (!TryParseSize(rest, out var single)) return false;
                min = single; // treat as minimum
                return true;
            }

            string op;
            if (rest.StartsWith(">=", StringComparison.Ordinal)) op = ">=";
            else if (rest.StartsWith("<=", StringComparison.Ordinal)) op = "<=";
            else if (rest.StartsWith(">", StringComparison.Ordinal)) op = ">";
            else if (rest.StartsWith("<", StringComparison.Ordinal)) op = "<";
            else return false;

            var numberPart = rest.Substring(op.Length).Trim();
            if (!TryParseSize(numberPart, out var bytes)) return false;

            switch (op)
            {
                case ">": min = bytes + 1; break;
                case ">=": min = bytes; break;
                case "<": max = bytes - 1; break;
                case "<=": max = bytes; break;
            }

            return true;
        }

        private static bool TryParseSize(string text, out long bytes)
        {
            bytes = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            text = text.Trim().ToLowerInvariant();

            int i = 0;
            while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.' || text[i] == ','))
                i++;

            var numPart = text.Substring(0, i).Replace(',', '.').Trim();
            var unitPart = text.Substring(i).Trim();

            if (!double.TryParse(numPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return false;

            long mul = unitPart switch
            {
                "" or "b" => 1L,
                "kb" => 1024L,
                "mb" => 1024L * 1024L,
                "gb" => 1024L * 1024L * 1024L,
                _ => 0L
            };

            if (mul == 0L) return false;

            var result = value * mul;
            if (result < 0 || result > long.MaxValue) return false;

            bytes = (long)result;
            return true;
        }

        private static bool TryParseAddedTokenUtc(string token, out DateTime? minUtc, out DateTime? maxUtc)
        {
            minUtc = null;
            maxUtc = null;

            token = token.Trim();
            if (!token.StartsWith("added", StringComparison.OrdinalIgnoreCase))
                return false;

            var rest = token.Substring(5).Trim();

            if (rest.StartsWith(":", StringComparison.Ordinal))
            {
                rest = rest.Substring(1).Trim();

                if (rest.Equals("today", StringComparison.OrdinalIgnoreCase))
                {
                    var start = DateTime.UtcNow.Date;
                    minUtc = start;
                    maxUtc = start.AddDays(1).AddTicks(-1);
                    return true;
                }

                if (rest.Equals("yesterday", StringComparison.OrdinalIgnoreCase))
                {
                    var start = DateTime.UtcNow.Date.AddDays(-1);
                    minUtc = start;
                    maxUtc = start.AddDays(1).AddTicks(-1);
                    return true;
                }

                if (rest.EndsWith("d", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(rest.Substring(0, rest.Length - 1), out var days) && days > 0)
                {
                    minUtc = DateTime.UtcNow.AddDays(-days);
                    maxUtc = DateTime.UtcNow;
                    return true;
                }

                var dots = rest.IndexOf("..", StringComparison.Ordinal);
                if (dots >= 0)
                {
                    var a = rest.Substring(0, dots).Trim();
                    var b = rest.Substring(dots + 2).Trim();

                    if (!TryParseIsoDateUtcStart(a, out var start)) return false;
                    if (!TryParseIsoDateUtcEndInclusive(b, out var end)) return false;

                    minUtc = start;
                    maxUtc = end;
                    return true;
                }

                if (TryParseIsoDateUtcStart(rest, out var dayStart))
                {
                    minUtc = dayStart;
                    maxUtc = dayStart.AddDays(1).AddTicks(-1);
                    return true;
                }

                return false;
            }

            string op;
            if (rest.StartsWith(">=", StringComparison.Ordinal)) op = ">=";
            else if (rest.StartsWith("<=", StringComparison.Ordinal)) op = "<=";
            else if (rest.StartsWith(">", StringComparison.Ordinal)) op = ">";
            else if (rest.StartsWith("<", StringComparison.Ordinal)) op = "<";
            else return false;

            var datePart = rest.Substring(op.Length).Trim();
            if (!TryParseIsoDateUtcStart(datePart, out var dtStart))
                return false;

            if (op is "<" or "<=")
            {
                if (!TryParseIsoDateUtcEndInclusive(datePart, out var dtEnd))
                    return false;

                maxUtc = op == "<" ? dtEnd.AddTicks(-1) : dtEnd;
                return true;
            }

            minUtc = op == ">" ? dtStart.AddTicks(1) : dtStart;
            return true;
        }

        private static bool TryParseIsoDateUtcStart(string s, out DateTime utcStart)
        {
            utcStart = default;
            if (!DateTime.TryParseExact(
                    s,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var dt))
                return false;

            utcStart = dt.Date;
            return true;
        }

        private static bool TryParseIsoDateUtcEndInclusive(string s, out DateTime utcEndInclusive)
        {
            utcEndInclusive = default;
            if (!TryParseIsoDateUtcStart(s, out var start))
                return false;

            utcEndInclusive = start.AddDays(1).AddTicks(-1);
            return true;
        }
    }
}
