using System.Buffers.Binary;
using System.IO.Compression;

namespace PhoneStore.Business.Management;

public sealed class CrossPlatformProductImageContentValidator : IProductImageContentValidator
{
    public const int MaxDimension = 4096;
    public const long MaxPixels = 16_000_000;

    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly int[] Adam7StartX = [0, 4, 0, 2, 0, 1, 0];
    private static readonly int[] Adam7StartY = [0, 0, 4, 0, 2, 0, 1];
    private static readonly int[] Adam7StepX = [8, 8, 4, 4, 2, 2, 1];
    private static readonly int[] Adam7StepY = [8, 8, 8, 4, 4, 2, 2];

    public void Validate(Stream content, string extension, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();

        content.Position = 0;
        byte[] bytes;
        if (content is MemoryStream ms && ms.TryGetBuffer(out var segment) && segment.Offset == 0 && segment.Count == ms.Length)
        {
            bytes = segment.Array!;
            ValidateBytes(bytes.AsSpan(0, segment.Count), extension, cancellationToken);
            return;
        }

        using var copy = new MemoryStream();
        content.CopyTo(copy);
        bytes = copy.ToArray();
        ValidateBytes(bytes, extension, cancellationToken);
    }

    private static void ValidateBytes(ReadOnlySpan<byte> bytes, string extension, CancellationToken ct)
    {
        var ext = (extension ?? string.Empty).ToLowerInvariant();
        if (ext == ".png")
        {
            ValidatePng(bytes, ct);
        }
        else if (ext is ".jpg" or ".jpeg")
        {
            ValidateJpeg(bytes, ct);
        }
        else
        {
            throw new InvalidDataException("Nội dung ảnh không khớp định dạng JPEG/PNG đã chọn.");
        }
    }

    private static void ValidatePng(ReadOnlySpan<byte> bytes, CancellationToken ct)
    {
        if (bytes.Length < 33 || !bytes[..8].SequenceEqual(PngSignature))
            throw new InvalidDataException("Nội dung ảnh không khớp định dạng JPEG/PNG đã chọn.");

        var offset = 8;
        var sawIhdr = false;
        var sawPlte = false;
        var sawIend = false;
        var width = 0;
        var height = 0;
        byte bitDepth = 0;
        byte colorType = 0;
        byte interlace = 0;
        using var idatStream = new MemoryStream();

        while (offset <= bytes.Length - 12)
        {
            ct.ThrowIfCancellationRequested();
            var length = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4));
            if (length > (uint)(bytes.Length - offset - 12))
                throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");

            var type = bytes.Slice(offset + 4, 4);
            var data = bytes.Slice(offset + 8, (int)length);

            if (type.SequenceEqual("acTL"u8) || type.SequenceEqual("fcTL"u8) || type.SequenceEqual("fdAT"u8))
                throw new InvalidDataException("Chỉ chấp nhận ảnh tĩnh một khung hình.");

            if (!sawIhdr)
            {
                if (!type.SequenceEqual("IHDR"u8) || length != 13)
                    throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");

                width = BinaryPrimitives.ReadInt32BigEndian(data[..4]);
                height = BinaryPrimitives.ReadInt32BigEndian(data.Slice(4, 4));
                bitDepth = data[8];
                colorType = data[9];
                var compression = data[10];
                var filter = data[11];
                interlace = data[12];

                if (width <= 0 || height <= 0 || width > MaxDimension || height > MaxDimension ||
                    (long)width * height > MaxPixels)
                    throw new InvalidDataException("Ảnh tối đa 4096×4096 và 16 triệu điểm ảnh.");

                if (compression != 0 || filter != 0 || interlace > 1 || !IsValidPngColorFormat(bitDepth, colorType))
                    throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");

                sawIhdr = true;
            }
            else if (type.SequenceEqual("IHDR"u8))
            {
                throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");
            }
            else if (type.SequenceEqual("PLTE"u8))
            {
                if (length == 0 || length > 256 * 3 || length % 3 != 0)
                    throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");
                sawPlte = true;
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                if (length > 0)
                    idatStream.Write(data);
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                if (length != 0 || offset + 12 != bytes.Length)
                    throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");
                sawIend = true;
                break;
            }

            offset += (int)length + 12;
        }

        if (!sawIhdr || !sawIend || idatStream.Length == 0 || (colorType == 3 && !sawPlte))
            throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");

        var bitsPerPixel = colorType switch
        {
            0 => bitDepth,
            2 => bitDepth * 3,
            3 => bitDepth,
            4 => bitDepth * 2,
            6 => bitDepth * 4,
            _ => throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.")
        };

        idatStream.Position = 0;
        try
        {
            using var zlib = new ZLibStream(idatStream, CompressionMode.Decompress, leaveOpen: true);
            if (interlace == 0)
            {
                VerifyPngPassScanlines(zlib, width, height, bitsPerPixel, ct);
            }
            else
            {
                for (var pass = 0; pass < 7; pass++)
                {
                    var passWidth = (width - Adam7StartX[pass] + Adam7StepX[pass] - 1) / Adam7StepX[pass];
                    var passHeight = (height - Adam7StartY[pass] + Adam7StepY[pass] - 1) / Adam7StepY[pass];
                    if (passWidth > 0 && passHeight > 0)
                        VerifyPngPassScanlines(zlib, passWidth, passHeight, bitsPerPixel, ct);
                }
            }

            var trailing = zlib.ReadByte();
            if (trailing != -1)
                throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not InvalidDataException)
        {
            throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.", ex);
        }
    }

    private static void VerifyPngPassScanlines(Stream zlib, int width, int height, int bitsPerPixel, CancellationToken ct)
    {
        var rowBytes = checked((int)(((long)width * bitsPerPixel + 7) / 8));
        var scanline = new byte[rowBytes + 1];
        for (var y = 0; y < height; y++)
        {
            if ((y & 31) == 0) ct.ThrowIfCancellationRequested();
            ReadExact(zlib, scanline);
            if (scanline[0] > 4)
                throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");
        }
    }

    private static void ReadExact(Stream stream, Span<byte> buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer[total..]);
            if (read <= 0)
                throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");
            total += read;
        }
    }

    private static bool IsValidPngColorFormat(byte bitDepth, byte colorType) => colorType switch
    {
        0 => bitDepth is 1 or 2 or 4 or 8 or 16,
        2 => bitDepth is 8 or 16,
        3 => bitDepth is 1 or 2 or 4 or 8,
        4 => bitDepth is 8 or 16,
        6 => bitDepth is 8 or 16,
        _ => false
    };

    private static void ValidateJpeg(ReadOnlySpan<byte> bytes, CancellationToken ct)
    {
        if (bytes.Length < 20 || bytes[0] != 0xFF || bytes[1] != 0xD8 || bytes[^2] != 0xFF || bytes[^1] != 0xD9)
            throw new InvalidDataException("Nội dung ảnh không khớp định dạng JPEG/PNG đã chọn.");

        var offset = 2;
        var sawSof = false;
        var sawDqt = false;
        var sawSos = false;
        var frameComponents = 0;

        while (offset < bytes.Length)
        {
            ct.ThrowIfCancellationRequested();
            if (bytes[offset] != 0xFF)
                throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");

            while (offset < bytes.Length && bytes[offset] == 0xFF)
                offset++;

            if (offset >= bytes.Length)
                throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");

            var marker = bytes[offset++];
            if (marker == 0xD9)
            {
                if (offset != bytes.Length)
                    throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");
                break;
            }

            if (marker is 0x00 or 0xD8 or (>= 0xD0 and <= 0xD7))
                throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");

            if (offset + 2 > bytes.Length)
                throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");

            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset, 2));
            if (segmentLength < 2 || offset + segmentLength > bytes.Length)
                throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");

            var payload = bytes.Slice(offset + 2, segmentLength - 2);
            offset += segmentLength;

            if (marker == 0xDB)
            {
                if (payload.Length < 65)
                    throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");
                sawDqt = true;
            }
            else if (marker is 0xC0 or 0xC1 or 0xC2)
            {
                if (sawSof || payload.Length < 6)
                    throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");

                var precision = payload[0];
                var height = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(1, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(3, 2));
                frameComponents = payload[5];

                if (precision != 8 || frameComponents is not (1 or 3 or 4) || payload.Length != 6 + frameComponents * 3)
                    throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");

                if (width <= 0 || height <= 0 || width > MaxDimension || height > MaxDimension ||
                    (long)width * height > MaxPixels)
                    throw new InvalidDataException("Ảnh tối đa 4096×4096 và 16 triệu điểm ảnh.");

                for (var i = 0; i < frameComponents; i++)
                {
                    var sampling = payload[6 + i * 3 + 1];
                    var h = sampling >> 4;
                    var v = sampling & 0x0F;
                    if (h is < 1 or > 4 || v is < 1 or > 4)
                        throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");
                }
                sawSof = true;
            }
            else if (marker is >= 0xC3 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");
            }
            else if (marker == 0xDA)
            {
                if (!sawSof || !sawDqt || payload.Length < 4)
                    throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");

                var scanComponents = payload[0];
                if (scanComponents < 1 || scanComponents > frameComponents || payload.Length != 1 + scanComponents * 2 + 3)
                    throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");

                // Walk entropy-coded scan segment until next non-stuffed, non-RST marker
                var entropyBytes = 0;
                while (offset < bytes.Length)
                {
                    var b = bytes[offset++];
                    if (b != 0xFF)
                    {
                        entropyBytes++;
                        continue;
                    }

                    while (offset < bytes.Length && bytes[offset] == 0xFF)
                        offset++;

                    if (offset >= bytes.Length)
                        throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");

                    var nextMarker = bytes[offset++];
                    if (nextMarker == 0x00 || nextMarker is >= 0xD0 and <= 0xD7)
                    {
                        entropyBytes++;
                        continue;
                    }

                    if (entropyBytes == 0)
                        throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");

                    sawSos = true;
                    offset -= 2;
                    break;
                }

                if (!sawSos)
                    throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");
            }
        }

        if (!sawSof || !sawDqt || !sawSos)
            throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.");
    }
}
