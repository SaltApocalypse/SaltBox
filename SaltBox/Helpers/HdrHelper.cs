using Microsoft.UI.Dispatching;
using Serilog;
using System.Runtime.InteropServices;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;

namespace SaltBox.Helpers;

public static class HdrHelper
{
    private static readonly ILogger Log = Serilog.Log.ForContext(typeof(HdrHelper));

    private static bool? _cachedIsHdr;
    private static DateTime _cacheTime;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);

    public static async Task<bool> IsDisplayHdrAsync(DispatcherQueue dispatcher)
    {
        if (_cachedIsHdr.HasValue && DateTime.UtcNow - _cacheTime < CacheDuration)
            return _cachedIsHdr.Value;

        var tcs = new TaskCompletionSource<DisplayInformation?>();
        dispatcher.TryEnqueue(() =>
        {
            try { tcs.TrySetResult(DisplayInformation.GetForCurrentView()); }
            catch { tcs.TrySetResult(null); }
        });

        var displayInfo = await tcs.Task;
        if (displayInfo == null) return false;

        try
        {
            var info = displayInfo.GetAdvancedColorInfo();
            _cachedIsHdr = info.CurrentAdvancedColorKind == AdvancedColorKind.HighDynamicRange;
            _cacheTime = DateTime.UtcNow;
            Log.Information("HDR display detection: {IsHdr} (max nits: {Max}, SDR white: {SdrWhite})",
                _cachedIsHdr.Value, info.MaxLuminanceInNits, info.SdrWhiteLevelInNits);
            return _cachedIsHdr.Value;
        }
        catch (Exception ex)
        {
            Log.Warning("HDR detection failed: {Ex}", ex.Message);
            return false;
        }
    }

    public static unsafe byte[] ConvertFp16ToSdrPixels(SoftwareBitmap hdrBitmap, int width, int height)
    {
        using var buffer = hdrBitmap.LockBuffer(BitmapBufferAccessMode.Read);
        using var reference = buffer.CreateReference();

        var byteAccess = (IMemoryBufferByteAccess)reference;
        byteAccess.GetBuffer(out IntPtr dataPtr, out _);

        int pixelCount = width * height;
        var result = new byte[pixelCount * 4];

        var halfPtr = (ushort*)dataPtr;

        for (int i = 0; i < pixelCount; i++)
        {
            int off = i * 4;

            float r = HalfToFloat(halfPtr[off]);
            float g = HalfToFloat(halfPtr[off + 1]);
            float b = HalfToFloat(halfPtr[off + 2]);

            r = AcesFilmic(r);
            g = AcesFilmic(g);
            b = AcesFilmic(b);

            r = LinearToSrgb(r);
            g = LinearToSrgb(g);
            b = LinearToSrgb(b);

            int dst = i * 4;
            result[dst] = (byte)Math.Clamp(255f * b, 0, 255);
            result[dst + 1] = (byte)Math.Clamp(255f * g, 0, 255);
            result[dst + 2] = (byte)Math.Clamp(255f * r, 0, 255);
            result[dst + 3] = 255;
        }

        return result;
    }

    private static float AcesFilmic(float x)
    {
        if (x <= 0f) return 0f;
        return (x * (2.51f * x + 0.03f)) / (x * (2.43f * x + 0.59f) + 0.14f);
    }

    private static float LinearToSrgb(float x)
    {
        if (x <= 0.0031308f)
            return 12.92f * x;
        return 1.055f * MathF.Pow(x, 1f / 2.4f) - 0.055f;
    }

    private static float HalfToFloat(ushort half)
    {
        int sign = (half >> 15) & 1;
        int exp = (half >> 10) & 0x1F;
        int mant = half & 0x3FF;

        if (exp == 0)
        {
            if (mant == 0) return sign == 0 ? 0f : -0f;
            float val = (mant / 1024f) * MathF.Pow(2f, -14f);
            return sign == 1 ? -val : val;
        }

        if (exp == 31)
        {
            if (mant == 0) return float.PositiveInfinity;
            return float.NaN;
        }

        uint fp32 = (uint)(sign << 31) | (uint)((exp - 15 + 127) << 23) | (uint)(mant << 13);
        return BitConverter.UInt32BitsToSingle(fp32);
    }

    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMemoryBufferByteAccess
    {
        void GetBuffer(out IntPtr buffer, out uint capacity);
    }
}
