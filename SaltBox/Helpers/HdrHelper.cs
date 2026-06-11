using Serilog;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using WinRT;

namespace SaltBox.Helpers;

public static class HdrHelper
{
    private static readonly ILogger Log = Serilog.Log.ForContext(typeof(HdrHelper));

    public static bool IsDisplayHdr(IntPtr hmonitor)
    {
        try
        {
            var featureLevels = new[] { FeatureLevel.Level_11_0, FeatureLevel.Level_10_1 };
            var hr = D3D11.D3D11CreateDevice(
                null,
                DriverType.Hardware,
                DeviceCreationFlags.BgraSupport,
                featureLevels,
                out ID3D11Device? d3dDevice,
                out _,
                out ID3D11DeviceContext? context);
            context?.Dispose();

            if (hr.Failure)
            {
                Log.Warning("[HDR] D3D11CreateDevice failed: {Result}", hr);
                return false;
            }

            using var d3d = d3dDevice;
            var dxgiDevice = d3d.QueryInterface<IDXGIDevice>();
            if (dxgiDevice is null) return false;

            using (dxgiDevice)
            {
                var adapter = dxgiDevice.GetAdapter();
                if (adapter is null) return false;

                using (adapter)
                {
                    for (int outputIdx = 0; ; outputIdx++)
                    {
                        var enumHr = adapter.EnumOutputs(outputIdx, out IDXGIOutput? output);
                        if (enumHr.Failure || output is null)
                            break;

                        using (output)
                        {
                            var desc = output.Description;
                            if (desc.Monitor != hmonitor)
                                continue;

                            var output6 = output.QueryInterfaceOrNull<IDXGIOutput6>();
                            if (output6 is null)
                            {
                                Log.Warning("[HDR] IDXGIOutput6 not available on this system");
                                return false;
                            }

                            using (output6)
                            {
                                var desc1 = output6.Description1;
                                var isHdr = desc1.ColorSpace is ColorSpaceType.RgbFullG2084NoneP2020 or ColorSpaceType.RgbFullG10NoneP709;

                                Log.Debug("[HDR] DXGI ColorSpace={ColorSpace} ({Value}), IsHdr={IsHdr}",
                                    desc1.ColorSpace, (int)desc1.ColorSpace, isHdr);

                                if (isHdr)
                                    Log.Information("[HDR] HDR display: ColorSpace={ColorSpace}, " +
                                        "MaxLuminance={MaxLum} nits, SdrWhiteLevel={Sdr} nits",
                                        desc1.ColorSpace, desc1.MaxLuminance, desc1.MaxFullFrameLuminance);
                                else
                                    Log.Information("[HDR] SDR display: ColorSpace={ColorSpace}",
                                        desc1.ColorSpace);

                                return isHdr;
                            }
                        }
                    }
                }
            }

            Log.Warning("[HDR] No DXGI output found for HMONITOR=0x{Monitor:X16}", hmonitor);
            return false;
        }
        catch (Exception ex)
        {
            Log.Warning("[HDR] DXGI detection failed: {Message}", ex.Message);
            return false;
        }
    }

    public static unsafe byte[] ConvertFp16ToSdrPixels(SoftwareBitmap hdrBitmap, int width, int height)
    {
        using var buffer = hdrBitmap.LockBuffer(BitmapBufferAccessMode.Read);
        using var reference = buffer.CreateReference();

        var byteAccess = reference.As<IMemoryBufferByteAccess>();
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
