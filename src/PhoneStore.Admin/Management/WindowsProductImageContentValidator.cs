using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using PhoneStore.Business.Management;

namespace PhoneStore.Admin.Management;

public sealed class WindowsProductImageContentValidator : IProductImageContentValidator
{
    public const int MaxDimension = 4096;
    public const long MaxPixels = 16_000_000;

    public void Validate(Stream content, string extension, CancellationToken cancellationToken = default)
    {
        try
        {
            content.Position = 0;
            var decoder = BitmapDecoder.Create(content,
                BitmapCreateOptions.DelayCreation | BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile,
                BitmapCacheOption.None);
            if ((extension is ".jpg" or ".jpeg" && decoder is not JpegBitmapDecoder)
                || (extension == ".png" && decoder is not PngBitmapDecoder)
                || extension is not (".jpg" or ".jpeg" or ".png"))
                throw new InvalidDataException("Nội dung ảnh không khớp định dạng JPEG/PNG đã chọn.");
            if (decoder.Frames.Count != 1) throw new InvalidDataException("Chỉ chấp nhận ảnh tĩnh một khung hình.");
            var frame = decoder.Frames[0];
            if (frame.PixelWidth <= 0 || frame.PixelHeight <= 0 || frame.PixelWidth > MaxDimension
                || frame.PixelHeight > MaxDimension || (long)frame.PixelWidth * frame.PixelHeight > MaxPixels
                || frame.Format.BitsPerPixel is <= 0 or > 64)
                throw new InvalidDataException("Ảnh tối đa 4096×4096 và 16 triệu điểm ảnh.");
            // Force the decoder to read every pixel; metadata/Identify alone is insufficient.
            var stride = checked((frame.PixelWidth * frame.Format.BitsPerPixel + 7) / 8);
            var buffer = new byte[checked(stride * Math.Min(32, frame.PixelHeight))];
            for (var y = 0; y < frame.PixelHeight; y += 32)
            {
                cancellationToken.ThrowIfCancellationRequested();
                frame.CopyPixels(new Int32Rect(0, y, frame.PixelWidth, Math.Min(32, frame.PixelHeight - y)), buffer, stride, 0);
            }
        }
        catch (Exception ex) when (ex is FileFormatException or NotSupportedException or ArgumentException or COMException)
        {
            throw new InvalidDataException("Ảnh hỏng, bị cắt cụt hoặc không giải mã được.", ex);
        }
    }
}
