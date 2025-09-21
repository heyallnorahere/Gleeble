namespace Gleeble;

using Veldrid;
using Veldrid.SPIRV;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public sealed class ShaderCompiler
{
    private static readonly Regex sIncludeRegex = new Regex("^#include \"([a-zA-Z.\\/]*)\"$");
    private static readonly Regex sStageRegex = new Regex("^#stage ([A-Z][a-z]*)$");

    public ShaderCompiler(ResourceFactory factory, Encoding? encoding = null)
    {
        mFactory = factory;
        mEncoding = encoding ?? Encoding.ASCII;
    }

    private Stream? OpenStream(string path)
    {
        var type = GetType();
        var assembly = type.Assembly;

        var assemblyName = assembly.GetName().Name;
        var manifestName = $"{assemblyName}.{path.Replace('/', '.')}";

        return assembly.GetManifestResourceStream(manifestName);
    }

    public void Postprocess(string currentPath, Stream input, Stream output)
    {
        var sourceDirectory = Path.GetDirectoryName(currentPath);

        using var reader = new StreamReader(input, mEncoding, leaveOpen: true);
        using var writer = new StreamWriter(output, mEncoding, leaveOpen: true);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var match = sIncludeRegex.Match(line);
            if (match.Success)
            {
                writer.Flush();

                var group = match.Groups[1];
                var relativePath = group.Value;

                var manifestPath = Path.Join(sourceDirectory, relativePath);
                using var manifestStream = OpenStream(manifestPath);

                if (manifestStream is null)
                {
                    throw new FileNotFoundException($"Failed to include file from source {currentPath}", manifestPath);
                }

                Postprocess(manifestPath, manifestStream, output);
                continue;
            }

            writer.WriteLine(line);
        }

        writer.Flush();
    }

    public Shader[] CompileShader(string path)
    {
        using var input = OpenStream(path);
        if (input is null)
        {
            throw new FileNotFoundException();
        }

        TextWriter? outputWriter = null;
        var sourceStreams = new Dictionary<ShaderStages, MemoryStream>();

        var common = new List<string>();

        string? line;
        using var reader = new StreamReader(input, mEncoding, leaveOpen: true);
        while ((line = reader.ReadLine()) is not null)
        {
            var match = sStageRegex.Match(line);
            if (match.Success)
            {
                outputWriter?.Dispose();

                var group = match.Groups[1];
                var stageName = group.Value;

                var stage = Enum.Parse<ShaderStages>(stageName);
                var stream = new MemoryStream();
                sourceStreams.Add(stage, stream);

                outputWriter = new StreamWriter(stream, mEncoding, leaveOpen: true);
                foreach (var commonLine in common)
                {
                    outputWriter.WriteLine(commonLine);
                }

                outputWriter.Flush();
                continue;
            }

            if (outputWriter is null)
            {
                common.Add(line);
                continue;
            }

            outputWriter.WriteLine(line);
        }

        outputWriter?.Dispose();

        var descriptions = new Dictionary<ShaderStages, ShaderDescription>();
        foreach ((var stage, var rawStream) in sourceStreams)
        {
            rawStream.Position = 0;

            using var processedStream = new MemoryStream();
            Postprocess(path, rawStream, processedStream);
            rawStream.Dispose();

            var data = processedStream.ToArray();
            descriptions.Add(stage, new ShaderDescription(stage, data, "main"));
        }

        bool isGraphics = descriptions.Count == 2
            && descriptions.ContainsKey(ShaderStages.Vertex)
            && descriptions.ContainsKey(ShaderStages.Fragment);

        bool isCompute = descriptions.Count == 1 && descriptions.ContainsKey(ShaderStages.Compute);
        if (isGraphics && isCompute)
        {
            throw new InvalidOperationException("Cannot create a shader with both compute and graphics stages!");
        }

        if (isGraphics)
        {
            var vertexDesc = descriptions[ShaderStages.Vertex];
            var fragDesc = descriptions[ShaderStages.Fragment];

            return mFactory.CreateFromSpirv(vertexDesc, fragDesc);
        }

        if (isCompute)
        {
            var computeDesc = descriptions[ShaderStages.Compute];
            var shader = mFactory.CreateFromSpirv(computeDesc);

            return new Shader[] { shader };
        }

        throw new InvalidOperationException("Could not discern shader type!");
    }

    private readonly ResourceFactory mFactory;
    private readonly Encoding mEncoding;
}
