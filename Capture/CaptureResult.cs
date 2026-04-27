using System;
using System.Drawing;

namespace PointyPal.Capture;

public class CaptureResult : IDisposable
{
    public Bitmap Image { get; set; } = null!;
    public int OriginalWidth { get; set; }
    public int OriginalHeight { get; set; }
    public System.Windows.Rect MonitorBounds { get; set; }
    public System.Windows.Point CursorScreenPosition { get; set; }
    public System.Windows.Point CursorImagePosition { get; set; }
    public DateTime CaptureTimestamp { get; set; }
    public string ImagePath { get; set; } = string.Empty;

    public byte[] GetJpegBytes(int quality = 70)
    {
        using var ms = new System.IO.MemoryStream();
        System.Drawing.Imaging.ImageCodecInfo? jpegCodec = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.MimeType == "image/jpeg");
        if (jpegCodec != null)
        {
            var encoderParams = new System.Drawing.Imaging.EncoderParameters(1);
            encoderParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
            Image.Save(ms, jpegCodec, encoderParams);
        }
        else
        {
            Image.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
        }
        return ms.ToArray();
    }

    public void Dispose()
    {
        Image?.Dispose();
    }
}
