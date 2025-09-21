namespace Gleeble;

using Veldrid;
using Veldrid.Sdl2;
using Veldrid.SPIRV;
using Veldrid.StartupUtilities;

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;
using System.Text;

public struct Vertex
{
    public Vector2 Position;
    public RgbaFloat Color;

    public static implicit operator Vertex((Vector2 pos, RgbaFloat color) t) => new Vertex
    {
        Position = t.pos,
        Color = t.color
    };
}

public static class Program
{
    private static Sdl2Window? sWindow;
    private static GraphicsDevice? sDevice;

    private static CommandList? sCommandList;
    private static DeviceBuffer? sVertexBuffer, sIndexBuffer;
    private static Shader[]? sShaders;
    private static Pipeline? sPipeline;

    private static readonly Vertex[] sVertices = new Vertex[]
    {
        (new Vector2(-0.75f, 0.75f), RgbaFloat.Red),
        (new Vector2(0.75f, 0.75f), RgbaFloat.Green),
        (new Vector2(-0.75f, -0.75f), RgbaFloat.Blue),
        (new Vector2(0.75f, -0.75f), RgbaFloat.Yellow),
    };

    private static readonly uint[] sIndices = new uint[]
    {
        0, 1, 2, 2, 1, 3,
    };


    [MemberNotNull(nameof(sWindow))]
    [MemberNotNull(nameof(sDevice))]
    private static void Initialize()
    {
        var createInfo = new WindowCreateInfo
        {
            X = 100,
            Y = 100,
            WindowWidth = 1600,
            WindowHeight = 900,
            WindowTitle = "Gleeble",
        };

        var deviceOptions = new GraphicsDeviceOptions
        {
            PreferStandardClipSpaceYDirection = true,
            PreferDepthRangeZeroToOne = true
        };

        sWindow = VeldridStartup.CreateWindow(ref createInfo);
        sDevice = VeldridStartup.CreateGraphicsDevice(sWindow, deviceOptions);
    }

    private static unsafe DeviceBuffer CreateBuffer<T>(GraphicsDevice device, ReadOnlySpan<T> data, BufferUsage usage) where T : unmanaged
    {
        var factory = device.ResourceFactory;

        uint size = (uint)(data.Length * sizeof(T));
        var description = new BufferDescription(size, usage);

        var buffer = factory.CreateBuffer(description);
        fixed (T* ptr = data)
        {
            device.UpdateBuffer(buffer, 0, (nint)ptr, size);
        }

        return buffer;
    }

    private static byte[] GetResourceBytes(string path)
    {
        var assembly = typeof(Program).Assembly;
        using var stream = assembly.GetManifestResourceStream(path);

        if (stream is null)
        {
            throw new FileNotFoundException();
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);

        var buffer = memory.GetBuffer().AsSpan();
        return buffer.Slice(0, (int)stream.Length).ToArray();
    }

    [MemberNotNull(nameof(sCommandList))]
    [MemberNotNull(nameof(sVertexBuffer))]
    [MemberNotNull(nameof(sIndexBuffer))]
    [MemberNotNull(nameof(sShaders))]
    [MemberNotNull(nameof(sPipeline))]
    private static void CreateResources(GraphicsDevice device)
    {
        var factory = device.ResourceFactory;

        sCommandList = factory.CreateCommandList();

        sVertexBuffer = CreateBuffer<Vertex>(device, sVertices, BufferUsage.VertexBuffer);
        sIndexBuffer = CreateBuffer<uint>(device, sIndices, BufferUsage.IndexBuffer);

        var vertexSrc = GetResourceBytes("Gleeble.Shaders.Vertex.glsl");
        var vertexDesc = new ShaderDescription(ShaderStages.Vertex, vertexSrc, "main");

        var fragSrc = GetResourceBytes("Gleeble.Shaders.Fragment.glsl");
        var fragDesc = new ShaderDescription(ShaderStages.Fragment, fragSrc, "main");

        sShaders = factory.CreateFromSpirv(vertexDesc, fragDesc);

        var elements = new VertexElementDescription[]
        {
            new VertexElementDescription("in_Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
            new VertexElementDescription("in_Color", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4),
        };

        var vertexLayout = new VertexLayoutDescription(elements);
        var pipelineDesc = new GraphicsPipelineDescription
        {
            BlendState = BlendStateDescription.SingleOverrideBlend,
            DepthStencilState = new DepthStencilStateDescription(
                    depthTestEnabled: true,
                    depthWriteEnabled: true,
                    comparisonKind: ComparisonKind.LessEqual),
            RasterizerState = new RasterizerStateDescription(
                    cullMode: FaceCullMode.Back,
                    fillMode: PolygonFillMode.Solid,
                    frontFace: FrontFace.Clockwise,
                    depthClipEnabled: true,
                    scissorTestEnabled: false),
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            ResourceLayouts = Array.Empty<ResourceLayout>(),
            ShaderSet = new ShaderSetDescription(
                    vertexLayouts: new VertexLayoutDescription[] { vertexLayout },
                    shaders: sShaders),
            Outputs = device.SwapchainFramebuffer.OutputDescription,
        };

        sPipeline = factory.CreateGraphicsPipeline(pipelineDesc);
    }

    public static void Main(string[] args)
    {
        Initialize();
        CreateResources(sDevice);

        while (sWindow.Exists)
        {
            sWindow.PumpEvents();

            sCommandList.Begin();
            sCommandList.SetFramebuffer(sDevice.SwapchainFramebuffer);

            sCommandList.SetVertexBuffer(0, sVertexBuffer);
            sCommandList.SetIndexBuffer(sIndexBuffer, IndexFormat.UInt32);
            sCommandList.SetPipeline(sPipeline);

            sCommandList.DrawIndexed(
                    indexCount: (uint)sIndices.Length,
                    instanceCount: 1,
                    indexStart: 0,
                    vertexOffset: 0,
                    instanceStart: 0);

            sCommandList.End();
            sDevice.SubmitCommands(sCommandList);
            sDevice.SwapBuffers();
        }

        sPipeline.Dispose();
        foreach (var shader in sShaders)
        {
            shader.Dispose();
        }

        sIndexBuffer.Dispose();
        sVertexBuffer.Dispose();
        sCommandList.Dispose();

        sDevice.Dispose();
    }
}
