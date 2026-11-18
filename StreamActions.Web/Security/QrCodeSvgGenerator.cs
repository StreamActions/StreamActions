/*
 * This file is part of StreamActions.
 * Copyright © 2019-2026 StreamActions Team (streamactions.github.io)
 *
 * StreamActions is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * StreamActions is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with StreamActions.  If not, see <https://www.gnu.org/licenses/>.
 */

using System.Globalization;
using System.Text;

namespace StreamActions.Web.Security;

/// <summary>
/// Generates deterministic, offline ISO/IEC 18004 QR Code inline SVG markup using UTF-8 Byte mode (Versions 1–12, ECC Levels M and L).
/// </summary>
public static class QrCodeSvgGenerator
{
    /// <summary>
    /// Default number of quiet-zone modules surrounding the QR symbol.
    /// </summary>
    public const int DefaultQuietZoneModules = 4;

    /// <summary>
    /// GF(256) primitive polynomial (<c>x^8 + x^4 + x^3 + x^2 + 1</c>) specified by ISO/IEC 18004.
    /// </summary>
    private const int _galoisPrimitivePolynomial = 0x11D;

    /// <summary>
    /// 15-bit format information XOR mask specified by ISO/IEC 18004.
    /// </summary>
    private const int _formatInfoMask = 0x5412;

    /// <summary>
    /// BCH(15,5) generator polynomial for format information.
    /// </summary>
    private const int _formatBchGenerator = 0x537;

    /// <summary>
    /// BCH(18,6) generator polynomial for version information (Versions 7 and above).
    /// </summary>
    private const int _versionBchGenerator = 0x1F25;

    /// <summary>
    /// Maximum UTF-8 byte capacity supported across Versions 1–12 at ECC Level L.
    /// </summary>
    private const int _maxPayloadBytes = 367;

    /// <summary>
    /// GF(256) exponentiation lookup table.
    /// </summary>
    private static readonly byte[] _gfExp = BuildGaloisExpTable();

    /// <summary>
    /// GF(256) logarithm lookup table.
    /// </summary>
    private static readonly byte[] _gfLog = BuildGaloisLogTable(_gfExp);

    /// <summary>
    /// Alignment pattern center coordinates for QR Versions 1–12.
    /// </summary>
    private static readonly int[][] _alignmentCenters =
    [
        [],
        [6, 18],
        [6, 22],
        [6, 26],
        [6, 30],
        [6, 34],
        [6, 22, 38],
        [6, 24, 42],
        [6, 26, 46],
        [6, 28, 50],
        [6, 30, 54],
        [6, 32, 58],
    ];

    /// <summary>
    /// Version and Reed-Solomon block specifications for Versions 1–12 at ECC Level M.
    /// </summary>
    private static readonly VersionSpec[] _specsLevelM =
    [
        new(1, 0, 16, 10, 1, 16, 0, 0, 0),
        new(2, 0, 28, 16, 1, 28, 0, 0, 7),
        new(3, 0, 44, 26, 1, 44, 0, 0, 7),
        new(4, 0, 64, 18, 2, 32, 0, 0, 7),
        new(5, 0, 86, 24, 2, 43, 0, 0, 7),
        new(6, 0, 108, 16, 4, 27, 0, 0, 7),
        new(7, 0, 124, 18, 4, 31, 0, 0, 0),
        new(8, 0, 154, 22, 2, 38, 2, 39, 0),
        new(9, 0, 182, 22, 3, 36, 2, 37, 0),
        new(10, 0, 216, 26, 4, 43, 1, 44, 0),
        new(11, 0, 254, 30, 1, 50, 4, 51, 0),
        new(12, 0, 290, 22, 6, 36, 2, 37, 0),
    ];

    /// <summary>
    /// Version and Reed-Solomon block specifications for Versions 1–12 at ECC Level L.
    /// </summary>
    private static readonly VersionSpec[] _specsLevelL =
    [
        new(1, 1, 19, 7, 1, 19, 0, 0, 0),
        new(2, 1, 34, 10, 1, 34, 0, 0, 7),
        new(3, 1, 55, 15, 1, 55, 0, 0, 7),
        new(4, 1, 80, 20, 1, 80, 0, 0, 7),
        new(5, 1, 108, 26, 1, 108, 0, 0, 7),
        new(6, 1, 136, 18, 2, 68, 0, 0, 7),
        new(7, 1, 156, 20, 2, 78, 0, 0, 0),
        new(8, 1, 194, 24, 2, 97, 0, 0, 0),
        new(9, 1, 232, 30, 2, 116, 0, 0, 0),
        new(10, 1, 274, 18, 2, 68, 2, 69, 0),
        new(11, 1, 324, 20, 4, 81, 0, 0, 0),
        new(12, 1, 370, 24, 2, 92, 2, 93, 0),
    ];

    /// <summary>
    /// Encodes <paramref name="payload"/> as a UTF-8 Byte-mode QR Code and returns a deterministic, CSP-safe inline <c>&lt;svg&gt;</c> string.
    /// </summary>
    /// <param name="payload">The text or URI payload to encode (for example, an <c>otpauth://</c> URI).</param>
    /// <param name="quietZoneModules">The quiet-zone width in modules on each side (defaults to <see cref="DefaultQuietZoneModules"/>).</param>
    /// <returns>A standalone inline <c>&lt;svg&gt;</c> element containing a single <c>&lt;rect&gt;</c> background and <c>&lt;path&gt;</c> for dark modules.</returns>
    /// <exception cref="ArgumentException"><paramref name="payload"/> is null, whitespace, or exceeds the Version 1–12 capacity (367 UTF-8 bytes).</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="quietZoneModules"/> is negative.</exception>
    public static string GenerateSvg(string payload, int quietZoneModules = DefaultQuietZoneModules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        ArgumentOutOfRangeException.ThrowIfNegative(quietZoneModules);

        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
        if (payloadBytes.Length > _maxPayloadBytes)
        {
            throw new ArgumentException(
                $"QR code payload length ({payloadBytes.Length} UTF-8 bytes) exceeds the maximum supported capacity of {_maxPayloadBytes} bytes (Version 12, ECC Level L).",
                nameof(payload));
        }

        VersionSpec spec = SelectVersionSpec(payloadBytes.Length);

        byte[] dataCodewords = EncodeByteModeDataCodewords(payloadBytes, spec);
        byte[] finalCodewords = BuildInterleavedCodewords(dataCodewords, spec);

        int size = 17 + (4 * spec.Version);
        bool[] modules = new bool[size * size];
        bool[] isFunction = new bool[size * size];

        DrawFunctionPatterns(modules, isFunction, spec.Version, size);
        PlaceCodewordBits(modules, isFunction, finalCodewords, size);

        int bestMask = SelectBestMask(modules, isFunction, spec.EccFormatBits, size);
        ApplyMask(modules, isFunction, bestMask, size);
        DrawFormatInfo(modules, isFunction, spec.EccFormatBits, bestMask, size);

        return RenderSvg(modules, size, quietZoneModules);
    }

    /// <summary>
    /// Selects the smallest QR Version (1–12) favoring ECC Level M and falling back to ECC Level L for larger payloads.
    /// </summary>
    /// <param name="byteLength">The UTF-8 encoded payload length in bytes.</param>
    /// <returns>The matching <see cref="VersionSpec"/>.</returns>
    private static VersionSpec SelectVersionSpec(int byteLength)
    {
        foreach (VersionSpec spec in _specsLevelM)
        {
            if (byteLength <= GetByteModeMaxBytes(spec))
            {
                return spec;
            }
        }

        foreach (VersionSpec spec in _specsLevelL)
        {
            if (byteLength <= GetByteModeMaxBytes(spec))
            {
                return spec;
            }
        }

        throw new InvalidOperationException($"No QR code version specification matched payload length {byteLength}.");
    }

    /// <summary>
    /// Calculates the maximum Byte-mode UTF-8 payload byte capacity for <paramref name="spec"/>.
    /// </summary>
    /// <param name="spec">The version specification.</param>
    /// <returns>The maximum number of payload bytes.</returns>
    private static int GetByteModeMaxBytes(VersionSpec spec)
    {
        int countIndicatorBits = spec.Version <= 9 ? 8 : 16;
        int headerBits = 4 + countIndicatorBits;
        return ((spec.TotalDataCodewords * 8) - headerBits) / 8;
    }

    /// <summary>
    /// Encodes <paramref name="payloadBytes"/> into Byte-mode data codewords with terminator and ISO/IEC 18004 alternating pad bytes.
    /// </summary>
    /// <param name="payloadBytes">The UTF-8 payload bytes.</param>
    /// <param name="spec">The selected version specification.</param>
    /// <returns>The padded data codeword array.</returns>
    private static byte[] EncodeByteModeDataCodewords(byte[] payloadBytes, VersionSpec spec)
    {
        int totalDataBits = spec.TotalDataCodewords * 8;
        List<bool> bits = new(totalDataBits);

        AppendBits(bits, 0b0100, 4);
        int countBits = spec.Version <= 9 ? 8 : 16;
        AppendBits(bits, payloadBytes.Length, countBits);

        foreach (byte b in payloadBytes)
        {
            AppendBits(bits, b, 8);
        }

        int terminatorLength = Math.Min(4, totalDataBits - bits.Count);
        AppendBits(bits, 0, terminatorLength);

        while (bits.Count % 8 != 0)
        {
            bits.Add(false);
        }

        byte[] codewords = new byte[spec.TotalDataCodewords];
        int byteCount = bits.Count / 8;
        for (int i = 0; i < byteCount; i++)
        {
            byte value = 0;
            for (int bit = 0; bit < 8; bit++)
            {
                if (bits[(i * 8) + bit])
                {
                    value |= (byte)(1 << (7 - bit));
                }
            }

            codewords[i] = value;
        }

        for (int i = byteCount; i < codewords.Length; i++)
        {
            codewords[i] = ((i - byteCount) % 2 == 0) ? (byte)0xEC : (byte)0x11;
        }

        return codewords;
    }

    /// <summary>
    /// Appends the lowest <paramref name="bitCount"/> bits of <paramref name="value"/> (MSB-first) to <paramref name="bits"/>.
    /// </summary>
    /// <param name="bits">The target bit list.</param>
    /// <param name="value">The integer value to append.</param>
    /// <param name="bitCount">The number of bits to append.</param>
    private static void AppendBits(List<bool> bits, int value, int bitCount)
    {
        for (int i = bitCount - 1; i >= 0; i--)
        {
            bits.Add(((value >> i) & 1) != 0);
        }
    }

    /// <summary>
    /// Splits data codewords into Reed-Solomon blocks, computes GF(256) error-correction codewords, and interleaves all codewords.
    /// </summary>
    /// <param name="dataCodewords">The padded data codewords.</param>
    /// <param name="spec">The version specification.</param>
    /// <returns>The interleaved final codeword stream.</returns>
    private static byte[] BuildInterleavedCodewords(byte[] dataCodewords, VersionSpec spec)
    {
        int totalBlocks = spec.Group1Blocks + spec.Group2Blocks;
        byte[][] dataBlocks = new byte[totalBlocks][];
        byte[][] ecBlocks = new byte[totalBlocks][];

        byte[] generator = ComputeReedSolomonGenerator(spec.EcCodewordsPerBlock);
        int offset = 0;

        for (int i = 0; i < totalBlocks; i++)
        {
            int blockDataLength = i < spec.Group1Blocks ? spec.Group1DataCodewords : spec.Group2DataCodewords;
            byte[] block = new byte[blockDataLength];
            Array.Copy(dataCodewords, offset, block, 0, blockDataLength);
            offset += blockDataLength;

            dataBlocks[i] = block;
            ecBlocks[i] = ComputeReedSolomonRemainder(block, generator, spec.EcCodewordsPerBlock);
        }

        int totalCodewords = spec.TotalDataCodewords + (totalBlocks * spec.EcCodewordsPerBlock);
        byte[] result = new byte[totalCodewords];
        int writeIndex = 0;

        int maxDataLength = Math.Max(spec.Group1DataCodewords, spec.Group2DataCodewords);
        for (int col = 0; col < maxDataLength; col++)
        {
            for (int block = 0; block < totalBlocks; block++)
            {
                if (col < dataBlocks[block].Length)
                {
                    result[writeIndex++] = dataBlocks[block][col];
                }
            }
        }

        for (int col = 0; col < spec.EcCodewordsPerBlock; col++)
        {
            for (int block = 0; block < totalBlocks; block++)
            {
                result[writeIndex++] = ecBlocks[block][col];
            }
        }

        return result;
    }

    /// <summary>
    /// Computes the Reed-Solomon generator polynomial of degree <paramref name="degree"/> over GF(256).
    /// </summary>
    /// <param name="degree">The number of error-correction codewords.</param>
    /// <returns>The generator polynomial coefficients with highest degree first.</returns>
    private static byte[] ComputeReedSolomonGenerator(int degree)
    {
        byte[] poly = [1];
        for (int i = 0; i < degree; i++)
        {
            byte[] next = new byte[poly.Length + 1];
            byte root = _gfExp[i];
            next[0] = poly[0];
            for (int j = 1; j < poly.Length; j++)
            {
                next[j] = (byte)(poly[j] ^ GaloisMultiply(poly[j - 1], root));
            }

            next[poly.Length] = GaloisMultiply(poly[^1], root);
            poly = next;
        }

        return poly;
    }

    /// <summary>
    /// Computes the Reed-Solomon error-correction remainder for <paramref name="data"/> using <paramref name="generator"/>.
    /// </summary>
    /// <param name="data">The block data codewords.</param>
    /// <param name="generator">The generator polynomial coefficients.</param>
    /// <param name="ecCount">The number of error-correction codewords.</param>
    /// <returns>The error-correction codeword array.</returns>
    private static byte[] ComputeReedSolomonRemainder(byte[] data, byte[] generator, int ecCount)
    {
        byte[] buffer = new byte[data.Length + ecCount];
        Array.Copy(data, buffer, data.Length);

        for (int i = 0; i < data.Length; i++)
        {
            byte factor = buffer[i];
            if (factor == 0)
            {
                continue;
            }

            for (int j = 0; j < generator.Length; j++)
            {
                buffer[i + j] ^= GaloisMultiply(generator[j], factor);
            }
        }

        byte[] remainder = new byte[ecCount];
        Array.Copy(buffer, data.Length, remainder, 0, ecCount);
        return remainder;
    }

    /// <summary>
    /// Multiplies two elements in GF(256) modulo <see cref="_galoisPrimitivePolynomial"/>.
    /// </summary>
    /// <param name="a">The first GF(256) element.</param>
    /// <param name="b">The second GF(256) element.</param>
    /// <returns>The GF(256) product.</returns>
    private static byte GaloisMultiply(byte a, byte b) => a == 0 || b == 0 ? (byte)0 : _gfExp[(_gfLog[a] + _gfLog[b]) % 255];

    /// <summary>
    /// Builds the 256-entry GF(256) exponentiation table.
    /// </summary>
    /// <returns>The exponentiation lookup table.</returns>
    private static byte[] BuildGaloisExpTable()
    {
        byte[] table = new byte[256];
        int value = 1;
        for (int i = 0; i < 255; i++)
        {
            table[i] = (byte)value;
            value <<= 1;
            if ((value & 0x100) != 0)
            {
                value ^= _galoisPrimitivePolynomial;
            }
        }

        return table;
    }

    /// <summary>
    /// Builds the 256-entry GF(256) discrete logarithm table from <paramref name="expTable"/>.
    /// </summary>
    /// <param name="expTable">The GF(256) exponentiation table.</param>
    /// <returns>The logarithm lookup table.</returns>
    private static byte[] BuildGaloisLogTable(byte[] expTable)
    {
        byte[] table = new byte[256];
        for (int i = 0; i < 255; i++)
        {
            table[expTable[i]] = (byte)i;
        }

        return table;
    }

    /// <summary>
    /// Draws finder patterns, separators, timing patterns, alignment patterns, dark module, and reserves format/version areas.
    /// </summary>
    /// <param name="modules">The symbol module matrix.</param>
    /// <param name="isFunction">The function-module mask matrix.</param>
    /// <param name="version">The QR code version (1–12).</param>
    /// <param name="size">The matrix dimension in modules.</param>
    private static void DrawFunctionPatterns(bool[] modules, bool[] isFunction, int version, int size)
    {
        DrawFinderPattern(modules, isFunction, 0, 0, size);
        DrawFinderPattern(modules, isFunction, 0, size - 7, size);
        DrawFinderPattern(modules, isFunction, size - 7, 0, size);

        for (int i = 8; i < size - 8; i++)
        {
            bool dark = i % 2 == 0;
            SetFunctionModule(modules, isFunction, size, 6, i, dark);
            SetFunctionModule(modules, isFunction, size, i, 6, dark);
        }

        int[] centers = _alignmentCenters[version - 1];
        foreach (int cy in centers)
        {
            foreach (int cx in centers)
            {
                bool overlapsFinder = (cy <= 8 && cx <= 8)
                    || (cy <= 8 && cx >= size - 9)
                    || (cy >= size - 9 && cx <= 8);

                if (!overlapsFinder)
                {
                    DrawAlignmentPattern(modules, isFunction, size, cy, cx);
                }
            }
        }

        SetFunctionModule(modules, isFunction, size, (4 * version) + 9, 8, true);

        DrawFormatInfo(modules, isFunction, eccFormatBits: 0, mask: 0, size);

        if (version >= 7)
        {
            DrawVersionInfo(modules, isFunction, version, size);
        }
    }

    /// <summary>
    /// Draws a 7x7 finder pattern and its 1-module white separator border.
    /// </summary>
    /// <param name="modules">The symbol module matrix.</param>
    /// <param name="isFunction">The function-module mask matrix.</param>
    /// <param name="topRow">The top row of the 7x7 finder pattern.</param>
    /// <param name="leftCol">The left column of the 7x7 finder pattern.</param>
    /// <param name="size">The matrix dimension in modules.</param>
    private static void DrawFinderPattern(bool[] modules, bool[] isFunction, int topRow, int leftCol, int size)
    {
        for (int dr = -1; dr <= 7; dr++)
        {
            int row = topRow + dr;
            if (row < 0 || row >= size)
            {
                continue;
            }

            for (int dc = -1; dc <= 7; dc++)
            {
                int col = leftCol + dc;
                if (col < 0 || col >= size)
                {
                    continue;
                }

                bool isSeparator = dr == -1 || dr == 7 || dc == -1 || dc == 7;
                if (isSeparator)
                {
                    SetFunctionModule(modules, isFunction, size, row, col, false);
                }
                else
                {
                    int distance = Math.Max(Math.Abs(dr - 3), Math.Abs(dc - 3));
                    SetFunctionModule(modules, isFunction, size, row, col, distance != 2);
                }
            }
        }
    }

    /// <summary>
    /// Draws a 5x5 alignment pattern centered at (<paramref name="centerRow"/>, <paramref name="centerCol"/>).
    /// </summary>
    /// <param name="modules">The symbol module matrix.</param>
    /// <param name="isFunction">The function-module mask matrix.</param>
    /// <param name="size">The matrix dimension in modules.</param>
    /// <param name="centerRow">The center row index.</param>
    /// <param name="centerCol">The center column index.</param>
    private static void DrawAlignmentPattern(bool[] modules, bool[] isFunction, int size, int centerRow, int centerCol)
    {
        for (int dr = -2; dr <= 2; dr++)
        {
            for (int dc = -2; dc <= 2; dc++)
            {
                int distance = Math.Max(Math.Abs(dr), Math.Abs(dc));
                SetFunctionModule(modules, isFunction, size, centerRow + dr, centerCol + dc, distance != 1);
            }
        }
    }

    /// <summary>
    /// Computes and writes the 18-bit BCH(18,6) version information into both version-info regions (Versions 7 and above).
    /// </summary>
    /// <param name="modules">The symbol module matrix.</param>
    /// <param name="isFunction">The function-module mask matrix.</param>
    /// <param name="version">The QR code version (7–12).</param>
    /// <param name="size">The matrix dimension in modules.</param>
    private static void DrawVersionInfo(bool[] modules, bool[] isFunction, int version, int size)
    {
        int rem = version;
        for (int i = 0; i < 12; i++)
        {
            rem = (rem << 1) ^ (((rem >> 11) & 1) * _versionBchGenerator);
        }

        int bits = (version << 12) | rem;
        for (int i = 0; i < 18; i++)
        {
            bool dark = ((bits >> i) & 1) != 0;
            int a = size - 11 + (i % 3);
            int b = i / 3;
            SetFunctionModule(modules, isFunction, size, a, b, dark);
            SetFunctionModule(modules, isFunction, size, b, a, dark);
        }
    }

    /// <summary>
    /// Computes and writes the masked 15-bit BCH(15,5) format information into both format-info regions.
    /// </summary>
    /// <param name="modules">The symbol module matrix.</param>
    /// <param name="isFunction">The function-module mask matrix.</param>
    /// <param name="eccFormatBits">The 2-bit ECC level indicator.</param>
    /// <param name="mask">The 3-bit mask pattern index (0–7).</param>
    /// <param name="size">The matrix dimension in modules.</param>
    private static void DrawFormatInfo(bool[] modules, bool[] isFunction, int eccFormatBits, int mask, int size)
    {
        int data = (eccFormatBits << 3) | mask;
        int rem = data;
        for (int i = 0; i < 10; i++)
        {
            rem = (rem << 1) ^ (((rem >> 9) & 1) * _formatBchGenerator);
        }

        int bits = ((data << 10) | rem) ^ _formatInfoMask;

        for (int i = 0; i <= 5; i++)
        {
            SetFunctionModule(modules, isFunction, size, 8, i, ((bits >> i) & 1) != 0);
        }

        SetFunctionModule(modules, isFunction, size, 8, 7, ((bits >> 6) & 1) != 0);
        SetFunctionModule(modules, isFunction, size, 8, 8, ((bits >> 7) & 1) != 0);
        SetFunctionModule(modules, isFunction, size, 7, 8, ((bits >> 8) & 1) != 0);

        for (int i = 9; i < 15; i++)
        {
            SetFunctionModule(modules, isFunction, size, 14 - i, 8, ((bits >> i) & 1) != 0);
        }

        for (int i = 0; i < 8; i++)
        {
            SetFunctionModule(modules, isFunction, size, size - 1 - i, 8, ((bits >> i) & 1) != 0);
        }

        for (int i = 8; i < 15; i++)
        {
            SetFunctionModule(modules, isFunction, size, 8, size - 15 + i, ((bits >> i) & 1) != 0);
        }
    }

    /// <summary>
    /// Sets a single function module and marks its coordinates as reserved.
    /// </summary>
    /// <param name="modules">The symbol module matrix.</param>
    /// <param name="isFunction">The function-module mask matrix.</param>
    /// <param name="size">The matrix dimension in modules.</param>
    /// <param name="row">The module row.</param>
    /// <param name="col">The module column.</param>
    /// <param name="dark">Whether the module is dark.</param>
    private static void SetFunctionModule(bool[] modules, bool[] isFunction, int size, int row, int col, bool dark)
    {
        int index = (row * size) + col;
        modules[index] = dark;
        isFunction[index] = true;
    }

    /// <summary>
    /// Places interleaved codeword bits into non-function modules using the standard bottom-to-top, top-to-bottom zig-zag scan.
    /// </summary>
    /// <param name="modules">The symbol module matrix.</param>
    /// <param name="isFunction">The function-module mask matrix.</param>
    /// <param name="codewords">The interleaved codeword bytes.</param>
    /// <param name="size">The matrix dimension in modules.</param>
    private static void PlaceCodewordBits(bool[] modules, bool[] isFunction, byte[] codewords, int size)
    {
        int totalBits = codewords.Length * 8;
        int bitIndex = 0;
        bool upward = true;

        for (int rightCol = size - 1; rightCol >= 1; rightCol -= 2)
        {
            if (rightCol == 6)
            {
                rightCol = 5;
            }

            for (int vert = 0; vert < size; vert++)
            {
                int row = upward ? (size - 1 - vert) : vert;
                for (int dx = 0; dx < 2; dx++)
                {
                    int col = rightCol - dx;
                    int index = (row * size) + col;
                    if (isFunction[index])
                    {
                        continue;
                    }

                    bool dark = false;
                    if (bitIndex < totalBits)
                    {
                        dark = ((codewords[bitIndex >> 3] >> (7 - (bitIndex & 7))) & 1) != 0;
                        bitIndex++;
                    }

                    modules[index] = dark;
                }
            }

            upward = !upward;
        }
    }

    /// <summary>
    /// Evaluates all 8 ISO/IEC 18004 mask patterns and returns the mask index with the lowest penalty score.
    /// </summary>
    /// <param name="modules">The unmasked symbol module matrix.</param>
    /// <param name="isFunction">The function-module mask matrix.</param>
    /// <param name="eccFormatBits">The 2-bit ECC level indicator.</param>
    /// <param name="size">The matrix dimension in modules.</param>
    /// <returns>The winning mask index (0–7).</returns>
    private static int SelectBestMask(bool[] modules, bool[] isFunction, int eccFormatBits, int size)
    {
        int bestMask = 0;
        long lowestPenalty = long.MaxValue;
        bool[] candidate = new bool[size * size];

        for (int mask = 0; mask < 8; mask++)
        {
            Array.Copy(modules, candidate, modules.Length);
            ApplyMask(candidate, isFunction, mask, size);
            DrawFormatInfo(candidate, isFunction, eccFormatBits, mask, size);

            long penalty = CalculatePenaltyScore(candidate, size);
            if (penalty < lowestPenalty)
            {
                lowestPenalty = penalty;
                bestMask = mask;
            }
        }

        return bestMask;
    }

    /// <summary>
    /// Applies mask pattern <paramref name="mask"/> to all non-function modules in <paramref name="modules"/>.
    /// </summary>
    /// <param name="modules">The symbol module matrix.</param>
    /// <param name="isFunction">The function-module mask matrix.</param>
    /// <param name="mask">The mask pattern index (0–7).</param>
    /// <param name="size">The matrix dimension in modules.</param>
    private static void ApplyMask(bool[] modules, bool[] isFunction, int mask, int size)
    {
        for (int row = 0; row < size; row++)
        {
            int rowOffset = row * size;
            for (int col = 0; col < size; col++)
            {
                int index = rowOffset + col;
                if (isFunction[index])
                {
                    continue;
                }

                bool invert = mask switch
                {
                    0 => ((row + col) % 2) == 0,
                    1 => (row % 2) == 0,
                    2 => (col % 3) == 0,
                    3 => ((row + col) % 3) == 0,
                    4 => (((row / 2) + (col / 3)) % 2) == 0,
                    5 => ((row * col % 2) + (row * col % 3)) == 0,
                    6 => (((row * col % 2) + (row * col % 3)) % 2) == 0,
                    7 => ((((row + col) % 2) + (row * col % 3)) % 2) == 0,
                    _ => false,
                };

                if (invert)
                {
                    modules[index] = !modules[index];
                }
            }
        }
    }

    /// <summary>
    /// Computes the ISO/IEC 18004 penalty score (Rules N1, N2, N3, and N4) for <paramref name="modules"/>.
    /// </summary>
    /// <param name="modules">The masked symbol module matrix.</param>
    /// <param name="size">The matrix dimension in modules.</param>
    /// <returns>The total penalty score.</returns>
    private static long CalculatePenaltyScore(bool[] modules, int size)
    {
        long penalty = 0;

        // N1: 5+ consecutive same-color modules in rows and columns.
        for (int row = 0; row < size; row++)
        {
            int rowOffset = row * size;
            bool runColor = modules[rowOffset];
            int runLength = 1;
            for (int col = 1; col < size; col++)
            {
                bool current = modules[rowOffset + col];
                if (current == runColor)
                {
                    runLength++;
                }
                else
                {
                    if (runLength >= 5)
                    {
                        penalty += runLength - 2;
                    }

                    runColor = current;
                    runLength = 1;
                }
            }

            if (runLength >= 5)
            {
                penalty += runLength - 2;
            }
        }

        for (int col = 0; col < size; col++)
        {
            bool runColor = modules[col];
            int runLength = 1;
            for (int row = 1; row < size; row++)
            {
                bool current = modules[(row * size) + col];
                if (current == runColor)
                {
                    runLength++;
                }
                else
                {
                    if (runLength >= 5)
                    {
                        penalty += runLength - 2;
                    }

                    runColor = current;
                    runLength = 1;
                }
            }

            if (runLength >= 5)
            {
                penalty += runLength - 2;
            }
        }

        // N2: 2x2 blocks of same-color modules.
        for (int row = 0; row < size - 1; row++)
        {
            int rowOffset = row * size;
            int nextRowOffset = (row + 1) * size;
            for (int col = 0; col < size - 1; col++)
            {
                bool color = modules[rowOffset + col];
                if (modules[rowOffset + col + 1] == color
                    && modules[nextRowOffset + col] == color
                    && modules[nextRowOffset + col + 1] == color)
                {
                    penalty += 3;
                }
            }
        }

        // N3: 1:1:3:1:1 finder-like ratio preceded or followed by 4 light modules.
        for (int row = 0; row < size; row++)
        {
            int rowOffset = row * size;
            int window = 0;
            for (int col = 0; col < size; col++)
            {
                window = ((window << 1) | (modules[rowOffset + col] ? 1 : 0)) & 0x7FF;
                if (col >= 10 && (window == 0b10111010000 || window == 0b00001011101))
                {
                    penalty += 40;
                }
            }
        }

        for (int col = 0; col < size; col++)
        {
            int window = 0;
            for (int row = 0; row < size; row++)
            {
                window = ((window << 1) | (modules[(row * size) + col] ? 1 : 0)) & 0x7FF;
                if (row >= 10 && (window == 0b10111010000 || window == 0b00001011101))
                {
                    penalty += 40;
                }
            }
        }

        // N4: Deviation from 50% dark modules in 5% increments.
        long darkCount = 0;
        for (int i = 0; i < modules.Length; i++)
        {
            if (modules[i])
            {
                darkCount++;
            }
        }

        long totalModules = (long)size * size;
        long k = Math.Abs((darkCount * 20L) - (totalModules * 10L)) / totalModules;
        penalty += k * 10L;

        return penalty;
    }

    /// <summary>
    /// Renders the final module matrix into a compact inline SVG element with a quiet zone.
    /// </summary>
    /// <param name="modules">The final masked symbol module matrix.</param>
    /// <param name="size">The matrix dimension in modules.</param>
    /// <param name="quietZoneModules">The quiet-zone width in modules on each side.</param>
    /// <returns>The inline SVG markup string.</returns>
    private static string RenderSvg(bool[] modules, int size, int quietZoneModules)
    {
        int totalSize = size + (quietZoneModules * 2);
        StringBuilder pathBuilder = new(size * size * 4);

        for (int row = 0; row < size; row++)
        {
            int rowOffset = row * size;
            int col = 0;
            while (col < size)
            {
                if (!modules[rowOffset + col])
                {
                    col++;
                    continue;
                }

                int runStart = col;
                while (col < size && modules[rowOffset + col])
                {
                    col++;
                }

                int runWidth = col - runStart;
                int x = runStart + quietZoneModules;
                int y = row + quietZoneModules;

                _ = pathBuilder
                    .Append('M')
                    .Append(x.ToString(CultureInfo.InvariantCulture))
                    .Append(',')
                    .Append(y.ToString(CultureInfo.InvariantCulture))
                    .Append('h')
                    .Append(runWidth.ToString(CultureInfo.InvariantCulture))
                    .Append("v1h-")
                    .Append(runWidth.ToString(CultureInfo.InvariantCulture))
                    .Append('z');
            }
        }

        string totalSizeStr = totalSize.ToString(CultureInfo.InvariantCulture);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {totalSizeStr} {totalSizeStr}\" width=\"224\" height=\"224\" role=\"img\" aria-label=\"QR Code\" shape-rendering=\"crispEdges\"><rect width=\"{totalSizeStr}\" height=\"{totalSizeStr}\" fill=\"#ffffff\"/><path d=\"{pathBuilder}\" fill=\"#000000\"/></svg>");
    }

    /// <summary>
    /// Represents version, ECC level, and Reed-Solomon block geometry for a single QR symbol configuration.
    /// </summary>
    /// <param name="Version">The QR version number (1–12).</param>
    /// <param name="EccFormatBits">The 2-bit ECC format identifier.</param>
    /// <param name="TotalDataCodewords">Total data codewords across all blocks.</param>
    /// <param name="EcCodewordsPerBlock">Error-correction codewords generated per block.</param>
    /// <param name="Group1Blocks">Number of blocks in Group 1.</param>
    /// <param name="Group1DataCodewords">Data codewords per Group 1 block.</param>
    /// <param name="Group2Blocks">Number of blocks in Group 2.</param>
    /// <param name="Group2DataCodewords">Data codewords per Group 2 block.</param>
    /// <param name="RemainderBits">Number of remainder bits required after all codewords.</param>
    private sealed record VersionSpec(
        int Version,
        int EccFormatBits,
        int TotalDataCodewords,
        int EcCodewordsPerBlock,
        int Group1Blocks,
        int Group1DataCodewords,
        int Group2Blocks,
        int Group2DataCodewords,
        int RemainderBits);
}
