using System.Text.RegularExpressions;

namespace PhoneStore.Business.Management;

public sealed class LocalProductImageStorage : IProductImageStorage
{
    private const long MaxFileBytes = 10 * 1024 * 1024;
    private const string UrlPrefix = "/uploads/products/";
    private readonly string directory;
    private static readonly Regex ManagedUrl = new(
        @"^/uploads/products/([0-9a-f]{32}\.(?:jpg|jpeg|png|webp))$", RegexOptions.CultureInvariant);

    public LocalProductImageStorage(string uploadsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadsDirectory);
        directory = Path.GetFullPath(uploadsDirectory);
    }

    public async Task<string> StoreAsync(string sourceFile, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(sourceFile).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            throw new ArgumentException("Chỉ hỗ trợ ảnh JPG, JPEG, PNG hoặc WebP.");
        await using var source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read,
            FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (source.Length is <= 0 or > MaxFileBytes)
            throw new InvalidDataException("Ảnh phải có dung lượng lớn hơn 0 và không vượt quá 10 MB.");
        var header = new byte[16];
        var read = await source.ReadAsync(header, cancellationToken);
        if (!MatchesSignature(extension, header.AsSpan(0, read)))
            throw new InvalidDataException("Nội dung tệp không khớp với định dạng ảnh đã chọn.");
        source.Position = 0;

        EnsureSafeDirectory();
        Directory.CreateDirectory(directory);
        EnsureSafeDirectory();
        var name = Guid.NewGuid().ToString("N") + extension;
        var target = Path.Combine(directory, name);
        var temporary = target + ".tmp";
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, FileOptions.Asynchronous))
            {
                var buffer = new byte[81920];
                long total = 0;
                int count;
                while ((count = await source.ReadAsync(buffer, cancellationToken)) > 0)
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
        catch
        {
            TryDeleteCreatedFile(temporary);
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
        ".webp" => bytes.Length >= 16 && bytes[..4].SequenceEqual("RIFF"u8)
            && bytes.Slice(8, 4).SequenceEqual("WEBP"u8)
            && (bytes.Slice(12, 4).SequenceEqual("VP8 "u8) || bytes.Slice(12, 4).SequenceEqual("VP8L"u8)
                || bytes.Slice(12, 4).SequenceEqual("VP8X"u8)),
        _ => false
    };
}
