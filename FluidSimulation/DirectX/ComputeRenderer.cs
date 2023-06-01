using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;

using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.DXGI;

namespace ComputeShader.DirectX;

using SharpDX.Direct3D11;

internal abstract class ComputeRenderer : IDisposable
{

    protected readonly Device device = new(DriverType.Hardware, DeviceCreationFlags.None);

    protected readonly DeviceContext context;
    private readonly SwapChain swapChain;

    protected int width;
    protected int height;


    protected ComputeRenderer(Window window, int width, int height)
    {
        context = device.ImmediateContext;

        // Initialize swap chain
        using Factory1 factory = new();
        swapChain = new SwapChain(factory, device, new SwapChainDescription
        {
            ModeDescription = new ModeDescription(width, height, new Rational(60, 1), Format.R8G8B8A8_UNorm),
            BufferCount = 1,
            Usage = Usage.UnorderedAccess,
            OutputHandle = new WindowInteropHelper(window).Handle,
            IsWindowed = true,
            SampleDescription = new SampleDescription(1, 0)
        });

        this.width = width;
        this.height = height;

        BindViews();
    }

    private void BindViews()
    {
        using Texture2D backBuffer = swapChain.GetBackBuffer<Texture2D>(0);
        using UnorderedAccessView output = new(device, backBuffer);

        context.ComputeShader.SetUnorderedAccessView(0, output);
    }

    public void Resize(int width, int height)
    {
        this.width = width;
        this.height = height;

        swapChain.ResizeBuffers(1, width, height, Format.R8G8B8A8_UNorm, SwapChainFlags.None);
        BindViews();
    }

    public void Dispose()
    {
        device.Dispose();

        context.Dispose();
        swapChain.Dispose();
    }


    protected ComputeShader Compile(string path, string main = "Main")
    {
        ShaderPreprocessor preprocessor = new(path);
        string entry = preprocessor.Entry ?? main;

        using CompilationResult result = ShaderBytecode.Compile(preprocessor.Source, entry, "cs_5_0");
        return new ComputeShader(device, result);
    }

    public virtual void Render() => swapChain.Present(0, PresentFlags.None);

}

internal class ShaderPreprocessor
{

    public string Source { get; }
    public string? Entry { get; } = null;


    public ShaderPreprocessor(string path)
    {
        Source = File.ReadAllText("Shaders/" + path);
        foreach (Match match in Regex.Matches(Source, "#include \"([\\w.\\/-]+)\""))
        {
            string include = match.Groups[1].Value;
            ShaderPreprocessor preprocessor = new(include); // Process dependency

            Source = Source.Replace(match.Value, preprocessor.Source);
        }

        // Find entry pragma if there is one
        Match entry = Regex.Match(Source, "#pragma kernel (\\w+)");
        if (entry.Success)
        {
            Entry = entry.Groups[1].Value;
            Source = Source.Replace(entry.Value, "");
        }
    }

}
