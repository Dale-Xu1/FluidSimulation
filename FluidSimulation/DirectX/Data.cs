using SharpDX.DXGI;
using SharpDX.WIC;

namespace ComputeShader.DirectX;

using SharpDX;
using SharpDX.Direct3D11;

internal class ConstantBuffer<T> : Buffer where T : unmanaged
{

    public unsafe ConstantBuffer(Device device) : base(device, new BufferDescription
    {
        SizeInBytes = (sizeof(T) - 1 | 15) + 1, // Nearest multiple of 16 that is larger
        BindFlags = BindFlags.ConstantBuffer
    })
    { }

}

internal class ShaderResourceBuffer<T> : Buffer where T : unmanaged
{

    public ShaderResourceView View { get; }
    public int Length { get; }

    public unsafe ShaderResourceBuffer(Device device, int length) : base(device, new BufferDescription
    {
        SizeInBytes = sizeof(T) * length,
        StructureByteStride = sizeof(T),
        BindFlags = BindFlags.ShaderResource,
        OptionFlags = ResourceOptionFlags.BufferStructured
    })
    {
        View = new ShaderResourceView(device, this);
        Length = length;
    }

    public new void Dispose()
    {
        base.Dispose();
        View.Dispose();
    }

}

internal class UnorderedAccessBuffer<T> : Buffer where T : unmanaged
{

    public UnorderedAccessView View { get; }
    public int Length { get; }

    public unsafe UnorderedAccessBuffer(Device device, int length) : base(device, new BufferDescription
    {
        SizeInBytes = sizeof(T) * length,
        StructureByteStride = sizeof(T),
        BindFlags = BindFlags.UnorderedAccess,
        OptionFlags = ResourceOptionFlags.BufferStructured
    })
    {
        View = new UnorderedAccessView(device, this);
        Length = length;
    }

    public new void Dispose()
    {
        base.Dispose();
        View.Dispose();
    }

}

internal class ShaderResourceTexture : Texture2D
{

    private static BitmapSource LoadBitmap(string path)
    {
        using ImagingFactory factory = new();
        using BitmapDecoder decoder = new(factory, path, DecodeOptions.CacheOnDemand);

        FormatConverter converter = new(factory);
        converter.Initialize(decoder.GetFrame(0), PixelFormat.Format32bppPRGBA,
            BitmapDitherType.None, null, 0, BitmapPaletteType.Custom);

        return converter;
    }

    public static ShaderResourceTexture FromFile(Device device, string file)
    {
        using BitmapSource source = LoadBitmap(file);

        // Allocate DataStream to receive the WIC image pixels
        int stride = source.Size.Width * 4;
        using DataStream buffer = new(source.Size.Height * stride, true, true);

        // Copy the content of the WIC to the buffer
        source.CopyPixels(stride, buffer);
        return new ShaderResourceTexture(device, source.Size.Width, source.Size.Height,
            data: new DataRectangle(buffer.DataPointer, stride));
    }


    public ShaderResourceView View { get; }

    public int Width { get; }
    public int Height { get; }

    public ShaderResourceTexture(Device device, int width, int height, Format format = Format.R8G8B8A8_UNorm,
        params DataRectangle[] data) : base(device, new Texture2DDescription
        {
            Width = width, Height = height,
            ArraySize = 1, MipLevels = 1,
            Format = format,
            BindFlags = BindFlags.ShaderResource,
            SampleDescription = new SampleDescription(1, 0)
        }, data)
    {
        View = new ShaderResourceView(device, this);

        Width = width;
        Height = height;
    }

}

internal class UnorderedAccessTexture : Texture2D
{

    public UnorderedAccessView View { get; }

    public int Width { get; }
    public int Height { get; }

    public UnorderedAccessTexture(Device device, int width, int height, Format format = Format.R8G8B8A8_UNorm) :
        base(device, new Texture2DDescription
        {
            Width = width, Height = height,
            ArraySize = 1, MipLevels = 1,
            Format = format,
            BindFlags = BindFlags.UnorderedAccess,
            SampleDescription = new SampleDescription(1, 0)
        })
    {
        View = new UnorderedAccessView(device, this);

        Width = width;
        Height = height;
    }

}
