using System.Runtime.InteropServices;
using System.Windows;

using ComputeShader.DirectX;
using SharpDX;
using SharpDX.DXGI;

namespace FluidSimulation.Renderer;

using SharpDX.Direct3D11;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct FluidConstants
{

    public Vector2 Position { get; init; }
    public Vector2 Force { get; init; }

    public Color3 Color { get; init; }
    public float Radius { get; init; }

}

internal class FluidRenderer : ComputeRenderer
{

    private const int ITERATIONS = 64;
    private const int SCALE = 3;


    private readonly ConstantBuffer<FluidConstants> buffer;

    private readonly ComputeShader advection, divergence, vorticity;
    private readonly ComputeShader jacobi, gradient;

    private readonly ComputeShader density, render;
    private readonly ComputeShader addForce, addDensity;

    private readonly UnorderedAccessTexture velocity, previous;
    private readonly UnorderedAccessTexture p, pp;
    private readonly UnorderedAccessTexture d, pd;


    public FluidRenderer(Window window, int width, int height) : base(window, width, height)
    {
        advection = Compile("FluidSimulation.hlsl", "Advection");
        divergence = Compile("FluidSimulation.hlsl", "CalculateDivergence");
        vorticity = Compile("FluidSimulation.hlsl", "Vorticity");

        jacobi = Compile("FluidSimulation.hlsl", "Jacobi");
        gradient = Compile("FluidSimulation.hlsl", "GradientSubtraction");

        density = Compile("FluidSimulation.hlsl", "DensityAdvection");
        render = Compile("FluidSimulation.hlsl", "Render");

        addForce = Compile("FluidSimulation.hlsl", "AddForce");
        addDensity = Compile("FluidSimulation.hlsl", "AddDensity");

        buffer = new ConstantBuffer<FluidConstants>(device);
        context.ComputeShader.SetConstantBuffer(0, buffer);

        int w = width / SCALE, h = height / SCALE;

        velocity = new UnorderedAccessTexture(device, w, h, Format.R16G16_Float);
        previous = new UnorderedAccessTexture(device, w, h, Format.R16G16_Float);

        context.ComputeShader.SetUnorderedAccessView(1, velocity.View);
        context.ComputeShader.SetUnorderedAccessView(2, previous.View);

        using UnorderedAccessTexture div = new(device, w, h, Format.R16_Float);
        p = new UnorderedAccessTexture(device, w, h, Format.R16_Float);
        pp = new UnorderedAccessTexture(device, w, h, Format.R16_Float);

        context.ComputeShader.SetUnorderedAccessView(3, div.View);
        context.ComputeShader.SetUnorderedAccessView(4, p.View);
        context.ComputeShader.SetUnorderedAccessView(5, pp.View);

        d = new UnorderedAccessTexture(device, width, height, Format.R16G16B16A16_UNorm);
        pd = new UnorderedAccessTexture(device, width, height, Format.R16G16B16A16_UNorm);

        context.ComputeShader.SetUnorderedAccessView(6, d.View);
        context.ComputeShader.SetUnorderedAccessView(7, pd.View);
    }

    public new void Dispose()
    {
        base.Dispose();

        advection.Dispose(); divergence.Dispose(); vorticity.Dispose();
        jacobi.Dispose(); gradient.Dispose();

        density.Dispose(); render.Dispose();
        addForce.Dispose(); addDensity.Dispose();

        velocity.Dispose(); previous.Dispose();
        p.Dispose(); pp.Dispose();
        d.Dispose(); pd.Dispose();
    }


    public void AddForce(Vector2 current, Vector2 previous, Color3 color)
    {
        float radius = 50;
        FluidConstants constants = new()
        {
            Position = current,
            Force = current - previous,
            Color = color,
            Radius = radius
        };
        context.UpdateSubresource(ref constants, buffer);

        context.ComputeShader.Set(addForce);
        DispatchRadius(radius);

        constants = constants with { Radius = SCALE * radius };
        context.UpdateSubresource(ref constants, buffer);

        context.ComputeShader.Set(addDensity);
        DispatchRadius(SCALE * radius);
    }

    private void DispatchRadius(float radius)
    {
        int w = ((int) (2 * radius + 1) + 7) / 8;
        context.Dispatch(w, w, 1);
    }

    public override void Render()
    {
        int w = (width / SCALE + 7) / 8, h = (height / SCALE + 7) / 8;
        context.CopyResource(velocity, previous);

        context.ComputeShader.Set(advection);
        context.Dispatch(w, h, 1);

        context.ComputeShader.Set(divergence);
        context.Dispatch(w, h, 1);

        context.ComputeShader.Set(vorticity);
        context.Dispatch(w, h, 1);

        Project(w, h);
        Density();
        base.Render();
    }

    private void Project(int w, int h)
    {
        context.ClearUnorderedAccessView(pp.View, Vector4.Zero);
        for (int i = 0; i < ITERATIONS; i++)
        {
            context.ComputeShader.Set(jacobi);
            context.Dispatch(w, h, 1);

            context.CopyResource(p, pp);
        }

        context.ComputeShader.Set(gradient);
        context.Dispatch(w, h, 1);
    }

    private void Density()
    {
        int w = (width + 7) / 8, h = (height + 7) / 8;
        context.CopyResource(d, pd);

        context.ComputeShader.Set(density);
        context.Dispatch(w, h, 1);

        context.ComputeShader.Set(render);
        context.Dispatch(w, h, 1);
    }

}
