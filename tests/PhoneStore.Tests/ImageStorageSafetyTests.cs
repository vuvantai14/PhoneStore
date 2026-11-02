using Microsoft.AspNetCore.StaticFiles;
using PhoneStore.Business.Management;

namespace PhoneStore.Tests;

public sealed class ImageStorageSafetyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Png_bad_crc_or_animation_control_is_rejected_before_decoder(bool animation)
    {
        var root = Path.Combine(Path.GetTempPath(), "PhoneStore-D2B-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); var path = Path.Combine(root, "invalid.png");
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAYAAACp8Z5+AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAAQSURBVBhXY/jPAEIUAAYGAILBAf8lQkk6AAAAAElFTkSuQmCC");
        // Replace the ancillary sRGB chunk type with acTL, or damage only its CRC.
        if (animation) "acTL"u8.CopyTo(bytes.AsSpan(37, 4)); else bytes[42] ^= 1;
        try
        {
            await File.WriteAllBytesAsync(path, bytes); var decoder = new RejectingDecoder();
            await Assert.ThrowsAsync<InvalidDataException>(() => new LocalProductImageStorage(Path.Combine(root, "uploads"), decoder).StoreAsync(path));
            Assert.False(decoder.Called); Assert.False(Directory.Exists(Path.Combine(root, "uploads")));
        }
        finally { File.Delete(path); Directory.Delete(root); }
    }

    [Theory]
    [InlineData(".jpg", "image/jpeg")]
    [InlineData(".jpeg", "image/jpeg")]
    [InlineData(".png", "image/png")]
    public void Web_static_files_serve_accepted_formats_with_expected_mime(string extension, string mime)
    {
        Assert.True(new FileExtensionContentTypeProvider().TryGetContentType("/uploads/products/image" + extension, out var actual));
        Assert.Equal(mime, actual);
    }

    [Fact]
    public async Task Decoder_failure_does_not_create_or_publish_output()
    {
        var root = Path.Combine(Path.GetTempPath(), "PhoneStore-D2B-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); var input = Path.Combine(root, "input.jpg"); var output = Path.Combine(root, "uploads");
        try
        {
            await File.WriteAllBytesAsync(input, [255, 216, 255, 1, 255, 217]);
            var decoder = new RejectingDecoder();
            await Assert.ThrowsAsync<InvalidDataException>(() => new LocalProductImageStorage(output, decoder).StoreAsync(input));
            Assert.True(decoder.Called); Assert.False(Directory.Exists(output));
        }
        finally { File.Delete(input); Directory.Delete(root); }
    }

    [Fact]
    public async Task Webp_is_explicitly_rejected_before_decoder_or_output()
    {
        var root = Path.Combine(Path.GetTempPath(), "PhoneStore-D2B-" + Guid.NewGuid().ToString("N"));
        var decoder = new RejectingDecoder();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => new LocalProductImageStorage(root, decoder).StoreAsync("valid.webp"));
        Assert.Contains("WebP", ex.Message); Assert.False(decoder.Called); Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void Missing_decoder_is_never_a_signature_only_fallback()
    {
        Assert.Throws<ArgumentNullException>(() => new LocalProductImageStorage(Path.GetTempPath(), null!));
    }

    private sealed class RejectingDecoder : IProductImageContentValidator
    {
        public bool Called;
        public void Validate(Stream content, string extension, CancellationToken ct = default)
        { Called = true; throw new InvalidDataException("Expected decoder rejection"); }
    }
}
