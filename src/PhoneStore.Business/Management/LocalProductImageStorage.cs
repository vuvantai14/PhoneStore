using System.Text.RegularExpressions;
using System.Buffers.Binary;

namespace PhoneStore.Business.Management;

public sealed class LocalProductImageStorage : IProductImageStorage
{
    private const long MaxFileBytes = 10 * 1024 * 1024;
    private const string UrlPrefix = "/uploads/products/";
    private readonly string directory;
    private readonly IProductImageContentValidator validator;
    private static readonly Regex ManagedUrl = new(
        @"^/uploads/products/([0-9a-f]{32}\.(?:jpg|jpeg|png|webp))$", RegexOptions.CultureInvariant);

    public LocalProductImageStorage(string uploadsDirectory, IProductImageContentValidator validator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadsDirectory);
        directory = Path.GetFullPath(uploadsDirectory);
        this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public async Task<string> StoreAsync(string sourceFile, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(sourceFile).ToLowerInvariant();
        if (extension == ".webp") throw new ArgumentException("Chưa hỗ trợ tải lên WebP. Vui lòng chuyển ảnh sang JPEG hoặc PNG.");
        if (extension is not (".jpg" or ".jpeg" or ".png"))
            throw new ArgumentException("Chỉ hỗ trợ tải lên ảnh JPG, JPEG hoặc PNG.");
        await using var source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read,
            FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (source.Length is <= 0 or > MaxFileBytes)
            throw new InvalidDataException("Ảnh phải có dung lượng lớn hơn 0 và không vượt quá 10 MB.");
        using var snapshot = new MemoryStream();
        var inputBuffer = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(inputBuffer, cancellationToken)) > 0)
        {
            if (snapshot.Length + read > MaxFileBytes) throw new InvalidDataException("Ảnh không được vượt quá 10 MB.");
            snapshot.Write(inputBuffer, 0, read);
        }
        var bytes = snapshot.ToArray();
        if (!MatchesSignature(extension, bytes) || !HasCompleteEnding(extension, bytes))
            throw new InvalidDataException("Nội dung tệp không khớp với định dạng ảnh đã chọn.");
        snapshot.Position = 0;
        validator.Validate(snapshot, extension, cancellationToken);
        snapshot.Position = 0;
        cancellationToken.ThrowIfCancellationRequested();

        EnsureSafeDirectory();
        Directory.CreateDirectory(directory);
        EnsureSafeDirectory();
        var name = Guid.NewGuid().ToString("N") + extension;
        var target = Path.Combine(directory, name);
        var temporary = target + ".tmp";
        var createdTemporary = false;
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, FileOptions.Asynchronous))
            {
                createdTemporary = true;
                var buffer = new byte[81920];
                long total = 0;
                int count;
                while ((count = await snapshot.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += count;
                    if (total > MaxFileBytes)
                        throw new InvalidDataException("Ảnh không được vượt quá 10 MB.");
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                }
                await output.FlushAsync(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            EnsureSafeDirectory();
            File.Move(temporary, target, overwrite: false);
            return UrlPrefix + name;
        }
        catch (Exception ex)
        {
            if (createdTemporary && !TryDeleteCreatedFile(temporary))
                throw new IOException("Lưu ảnh thất bại; có thể còn tệp .tmp cần kiểm tra thủ công: " + Path.GetFileName(temporary), ex);
            throw;
        }
    }

    public bool TryDelete(string imageUrl)
    {
        var match = ManagedUrl.Match(imageUrl ?? "");
        if (!match.Success) return false;
        return TryDeleteCreatedFile(Path.Combine(directory, match.Groups[1].Value));
    }

    private bool TryDeleteCreatedFile(string path)
    {
        try
        {
            EnsureSafeDirectory();
            var absolute = Path.GetFullPath(path);
            if (!absolute.StartsWith(Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)) return false;
            if (!File.Exists(absolute)) return true;
            if ((File.GetAttributes(absolute) & FileAttributes.ReparsePoint) != 0) return false;
            File.Delete(absolute);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private void EnsureSafeDirectory()
    {
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Không thể sử dụng thư mục ảnh qua liên kết hệ thống tệp.");
        }
    }

    private static bool MatchesSignature(string extension, ReadOnlySpan<byte> bytes) => extension switch
    {
        ".jpg" or ".jpeg" => bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff,
        ".png" => bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        _ => false
    };

    // Strict terminal checks supplement decoding: do not let a tolerant codec salvage truncated containers.
    private static bool HasCompleteEnding(string extension, ReadOnlySpan<byte> bytes) => extension switch
    {
        ".jpg" or ".jpeg" => bytes.Length >= 5 && bytes[^2] == 0xff && bytes[^1] == 0xd9,
        ".png" => PngContainerIsComplete(bytes),
        _ => false
    };

    private static readonly uint[] CrcTable = CreateCrcTable();
    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            var value = i;
            for (var bit = 0; bit < 8; bit++) value = (value >> 1) ^ ((value & 1) == 0 ? 0 : 0xedb88320u);
            table[i] = value;
        }
        return table;
    }

    // Container bounds/checksums only; pixel validation still requires the injected decoder.
    // Reject APNG control chunks: WIC may expose just its first frame while browsers animate it.
    private static bool PngContainerIsComplete(ReadOnlySpan<byte> bytes)
    {
        var offset = 8;
        while (offset <= bytes.Length - 12)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4));
            if (length > (uint)(bytes.Length - offset - 12)) return false;
            var type = bytes.Slice(offset + 4, 4);
            if (type.SequenceEqual("acTL"u8) || type.SequenceEqual("fcTL"u8) || type.SequenceEqual("fdAT"u8)) return false;
            uint crc = 0xffffffff;
            foreach (var value in bytes.Slice(offset + 4, (int)length + 4)) crc = CrcTable[(crc ^ value) & 255] ^ (crc >> 8);
            if (~crc != BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset + 8 + (int)length, 4))) return false;
            offset += (int)length + 12;
            if (type.SequenceEqual("IEND"u8)) return length == 0 && offset == bytes.Length;
        }
        return false;
    }
}
