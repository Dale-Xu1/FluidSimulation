static const float DT = 1;
static const int SCALE = 3;

static const float DIFF = 0.99;
static const float CURL = 0.8;

static const float VELOCITY = 0.5;
static const float DENSITY = 0.2;

RWTexture2D<float4> Result : register(u0);

RWTexture2D<float2> Velocity : register(u1);
RWTexture2D<float2> Previous : register(u2);

RWTexture2D<float> Divergence : register(u3);
RWTexture2D<float> Pressure : register(u4);
RWTexture2D<float> PreviousPressure : register(u5);

RWTexture2D<float4> Density : register(u6);
RWTexture2D<float4> PreviousDensity : register(u7);

float4 BilinInterp(RWTexture2D<float4> data, float2 i)
{
    int4 ij = int4(i, i + 1);
    float2 t = i - ij.xy;

    return lerp(
        lerp(data[ij.xy], data[ij.xw], t.y),
        lerp(data[ij.zy], data[ij.zw], t.y),
        t.x);
}

float2 BilinInterp(RWTexture2D<float2> data, float2 i)
{
    int4 ij = int4(i, i + 1);
    float2 t = i - ij.xy;

    return lerp(
        lerp(data[ij.xy], data[ij.xw], t.y),
        lerp(data[ij.zy], data[ij.zw], t.y),
        t.x);
}

[numthreads(8, 8, 1)]
void Advection(uint3 id : SV_DispatchThreadID)
{
    // Bilinear interpolation of four pixels surrounding previous position
    float2 previous = id.xy - Previous[id.xy] * DT;
    Velocity[id.xy] = DIFF * BilinInterp(Previous, previous);
}

[numthreads(8, 8, 1)]
void CalculateDivergence(uint3 id : SV_DispatchThreadID)
{
    float n = Velocity[id.xy - int2(0, 1)].y;
    float s = Velocity[id.xy + int2(0, 1)].y;
    float e = Velocity[id.xy + int2(1, 0)].x;
    float w = Velocity[id.xy - int2(1, 0)].x;

    Divergence[id.xy] = 0.5 * (s - n + e - w);
}

[numthreads(8, 8, 1)]
void Vorticity(uint3 id : SV_DispatchThreadID)
{
    float c = Divergence[id.xy];
    float n = Divergence[id.xy - int2(0, 1)];
    float s = Divergence[id.xy + int2(0, 1)];
    float e = Divergence[id.xy + int2(1, 0)];
    float w = Divergence[id.xy - int2(1, 0)];

    float2 force = 0.5 * float2(abs(s) - abs(n), abs(e) - abs(w));
    float ls = max(0.001, dot(force, force));

    force *= CURL * rsqrt(ls) * c;
    Velocity[id.xy] += float2(force.x, -force.y);
}

[numthreads(8, 8, 1)]
void Jacobi(uint3 id : SV_DispatchThreadID)
{
    float n = PreviousPressure[id.xy - int2(0, 1)];
    float s = PreviousPressure[id.xy + int2(0, 1)];
    float e = PreviousPressure[id.xy + int2(1, 0)];
    float w = PreviousPressure[id.xy - int2(1, 0)];
    float d = Divergence[id.xy];

    Pressure[id.xy] = 0.25 * (n + s + e + w - d);
}

[numthreads(8, 8, 1)]
void GradientSubtraction(uint3 id : SV_DispatchThreadID)
{
    float n = Pressure[id.xy - int2(0, 1)];
    float s = Pressure[id.xy + int2(0, 1)];
    float e = Pressure[id.xy + int2(1, 0)];
    float w = Pressure[id.xy - int2(1, 0)];

    Velocity[id.xy] -= 0.5 * DT * float2(e - w, s - n);
}

[numthreads(8, 8, 1)]
void DensityAdvection(uint3 id : SV_DispatchThreadID)
{
    float2 i = float2(id.xy) / SCALE;
    float2 previous = id.xy - BilinInterp(Previous, i) * SCALE * DT;

    Density[id.xy] = DIFF * BilinInterp(PreviousDensity, previous);
}

[numthreads(8, 8, 1)]
void Render(uint3 id : SV_DispatchThreadID)
{
    Result[id.xy] = Density[id.xy];
}

float2 Position;
float2 Force;

float3 Color;
float Radius;

[numthreads(8, 8, 1)]
void AddForce(uint3 id : SV_DispatchThreadID)
{
    float2 offset = id.xy - float2(Radius, Radius);
    float m = exp(-dot(offset, offset) / Radius);

    uint2 index = uint2(Position / SCALE + offset);
    Velocity[index] += VELOCITY * m * Force;
}

[numthreads(8, 8, 1)]
void AddDensity(uint3 id : SV_DispatchThreadID)
{
    float2 offset = id.xy - float2(Radius, Radius);
    float m = exp(-dot(offset, offset) / Radius);

    uint2 index = uint2(Position + offset);
    Density[index] += float4(DENSITY * m * length(Force) * Color, 0);
}
