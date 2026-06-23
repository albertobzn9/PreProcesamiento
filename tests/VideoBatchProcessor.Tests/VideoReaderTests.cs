using OpenCvSharp;
using VideoBatchProcessor.Core.VideoReader;
using Xunit;

namespace VideoBatchProcessor.Tests;

public class VideoReaderTests : IClassFixture<VideoReaderTests.SyntheticVideoFixture>
{
    private readonly SyntheticVideoFixture _fixture;
    public VideoReaderTests(SyntheticVideoFixture fixture) => _fixture = fixture;

    [Fact]
    public void TryOpen_ArchivoValido_DevuelveTrue()
    {
        var ok = VideoReader.TryOpen(_fixture.VideoPath, out var reader, out var error);
        using (reader) { Assert.True(ok); Assert.NotNull(reader); Assert.Null(error); }
    }

    [Fact]
    public void TryOpen_RutaVacia_DevuelveFalse()
    {
        var ok = VideoReader.TryOpen("", out var reader, out var error);
        Assert.False(ok); Assert.Null(reader); Assert.NotNull(error);
    }

    [Fact]
    public void TryOpen_ArchivoInexistente_DevuelveFalse()
    {
        var ok = VideoReader.TryOpen("/no/existe/video.mp4", out var reader, out var error);
        Assert.False(ok); Assert.Contains("no existe", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Metadata_FpsCorrecto()
    {
        VideoReader.TryOpen(_fixture.VideoPath, out var reader, out _);
        using (reader) Assert.Equal(SyntheticVideoFixture.Fps, reader!.Metadata.Fps, precision: 1);
    }

    [Fact]
    public void Metadata_Dimensiones()
    {
        VideoReader.TryOpen(_fixture.VideoPath, out var reader, out _);
        using (reader)
        {
            Assert.Equal(SyntheticVideoFixture.Width, reader!.Metadata.Width);
            Assert.Equal(SyntheticVideoFixture.Height, reader.Metadata.Height);
        }
    }

    [Fact]
    public void Metadata_TotalFrames()
    {
        VideoReader.TryOpen(_fixture.VideoPath, out var reader, out _);
        using (reader) Assert.Equal(SyntheticVideoFixture.TotalFrames, reader!.Metadata.TotalFrames);
    }

    [Fact]
    public void Metadata_DuracionConsistente()
    {
        VideoReader.TryOpen(_fixture.VideoPath, out var reader, out _);
        using (reader)
        {
            var expected = SyntheticVideoFixture.TotalFrames / (double)SyntheticVideoFixture.Fps;
            Assert.Equal(expected, reader!.Metadata.Duration.TotalSeconds, precision: 1);
        }
    }

    [Fact]
    public void Metadata_FilePathAbsoluto()
    {
        VideoReader.TryOpen(_fixture.VideoPath, out var reader, out _);
        using (reader) Assert.True(Path.IsPathRooted(reader!.Metadata.FilePath));
    }

    [Fact]
    public async Task MoveNextAsync_LeeFrames()
    {
        VideoReader.TryOpen(_fixture.VideoPath, out var reader, out _);
        using (reader)
        {
            Assert.Equal(-1, reader!.CurrentFrameIndex);
            Assert.True(await reader.MoveNextAsync());
            Assert.Equal(0, reader.CurrentFrameIndex);
            Assert.False(reader.Current.Empty());
            Assert.True(await reader.MoveNextAsync());
            Assert.Equal(1, reader.CurrentFrameIndex);
        }
    }

    [Fact]
    public async Task MoveNextAsync_AlFinalDevuelveFalse()
    {
        VideoReader.TryOpen(_fixture.VideoPath, out var reader, out _);
        using (reader)
        {
            long count = 0;
            while (await reader!.MoveNextAsync()) count++;
            Assert.True(count > 0, "Debe leer al menos un frame");
        }
    }

    [Fact]
    public async Task MoveNextAsync_RespetaCancelacion()
    {
        VideoReader.TryOpen(_fixture.VideoPath, out var reader, out _);
        using (reader)
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var result = false; try { result = await reader!.MoveNextAsync(cts.Token); } catch (System.Threading.Tasks.TaskCanceledException) { } Assert.False(result);
        }
    }

    [Fact]
    public async Task ReadFrameAtAsync_PorIndice()
    {
        VideoReader.TryOpen(_fixture.VideoPath, out var reader, out _);
        using (reader)
        {
            using var frame = await reader!.ReadFrameAtAsync(10);
            Assert.NotNull(frame); Assert.False(frame!.Empty());
        }
    }

    [Fact]
    public async Task ReadFrameAtAsync_PorTimestamp()
    {
        VideoReader.TryOpen(_fixture.VideoPath, out var reader, out _);
        using (reader)
        {
            using var frame = await reader!.ReadFrameAtAsync(TimeSpan.FromSeconds(1));
            Assert.NotNull(frame); Assert.False(frame!.Empty());
        }
    }

    [Fact]
    public async Task ReadFrameAtAsync_FueraDeRango_Null()
    {
        VideoReader.TryOpen(_fixture.VideoPath, out var reader, out _);
        using (reader)
        {
            var frame = await reader!.ReadFrameAtAsync(SyntheticVideoFixture.TotalFrames + 100);
            Assert.Null(frame);
        }
    }

    [Fact]
    public async Task ReadFrameAtAsync_IndiceNegativo_Null()
    {
        VideoReader.TryOpen(_fixture.VideoPath, out var reader, out _);
        using (reader) { var frame = await reader!.ReadFrameAtAsync(-1); Assert.Null(frame); }
    }

    [Fact]
    public async Task Reset_VuelveAlInicio()
    {
        VideoReader.TryOpen(_fixture.VideoPath, out var reader, out _);
        using (reader)
        {
            await reader!.MoveNextAsync();
            await reader.MoveNextAsync();
            reader.Reset();
            Assert.Equal(-1, reader.CurrentFrameIndex);
            Assert.True(await reader.MoveNextAsync());
            Assert.Equal(0, reader.CurrentFrameIndex);
        }
    }

    [Fact]
    public void Dispose_LiberaRecursos()
    {
        VideoReader.TryOpen(_fixture.VideoPath, out var reader, out _);
        reader!.Dispose();
        Assert.Throws<ObjectDisposedException>(() => reader.MoveNext());
    }

    [Fact]
    public void Dispose_DobleLlamada_NoExplota()
    {
        VideoReader.TryOpen(_fixture.VideoPath, out var reader, out _);
        reader!.Dispose();
        reader.Dispose();
    }

    public sealed class SyntheticVideoFixture : IDisposable
    {
        public const int Fps = 30;
        public const int Width = 320;
        public const int Height = 240;
        public const int TotalFrames = 90;
        public string VideoPath { get; }

        public SyntheticVideoFixture()
        {
            VideoPath = Path.Combine(Path.GetTempPath(), $"vbp_test_{Guid.NewGuid():N}.avi");

            using var writer = new VideoWriter(
                VideoPath,
                FourCC.MJPG,
                Fps,
                new Size(Width, Height));

            if (!writer.IsOpened())
                throw new InvalidOperationException(
                    $"No se pudo crear el video de prueba en {VideoPath}. " +
                    "Verifica que OpenCvSharp4 runtime esté instalado correctamente.");

            using var frame = new Mat(Height, Width, MatType.CV_8UC3);
            for (var i = 0; i < TotalFrames; i++)
            {
                var v = (byte)(i * 255 / TotalFrames);
                frame.SetTo(new Scalar(v, 255 - v, 128));
                writer.Write(frame);
            }
        }

        public void Dispose()
        {
            try { if (File.Exists(VideoPath)) File.Delete(VideoPath); } catch { }
        }
    }
}
