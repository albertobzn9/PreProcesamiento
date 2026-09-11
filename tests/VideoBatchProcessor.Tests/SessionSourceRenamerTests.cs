using VideoBatchProcessor.Core.SessionFiles;

namespace VideoBatchProcessor.Tests;

public sealed class SessionSourceRenamerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"vbp-rename-{Guid.NewGuid():N}");

    public SessionSourceRenamerTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void TryRename_RenamesPrimaryAndAlternateContainers()
    {
        var mp4 = CreateFile("exp_0526_cp_r1d5.mp4");
        var mkv = CreateFile("exp_0526_cp_r1d5.mkv");
        var renamer = new SessionSourceRenamer();

        var success = renamer.TryRename(
            mp4,
            [mkv],
            "exp_0526_cp_d5r2",
            out var result,
            out var error);

        Assert.True(success, error);
        Assert.NotNull(result);
        Assert.EndsWith("exp_0526_cp_d5r2.mp4", result.PrimaryVideoPath);
        Assert.Single(result.AlternateVideoPaths);
        Assert.True(File.Exists(Path.Combine(_directory, "exp_0526_cp_d5r2.mp4")));
        Assert.True(File.Exists(Path.Combine(_directory, "exp_0526_cp_d5r2.mkv")));
        Assert.False(File.Exists(mp4));
        Assert.False(File.Exists(mkv));
    }

    [Fact]
    public void TryRename_AlsoFindsAnAlternateContainerThatWasNotLoaded()
    {
        var mp4 = CreateFile("exp_0526_cp_r1d5.mp4");
        _ = CreateFile("exp_0526_cp_r1d5.mkv");
        var renamer = new SessionSourceRenamer();

        var success = renamer.TryRename(mp4, [], "exp_0526_cp_d5r2", out var result, out var error);

        Assert.True(success, error);
        Assert.NotNull(result);
        Assert.Single(result.AlternateVideoPaths);
        Assert.True(File.Exists(Path.Combine(_directory, "exp_0526_cp_d5r2.mp4")));
        Assert.True(File.Exists(Path.Combine(_directory, "exp_0526_cp_d5r2.mkv")));
    }

    [Fact]
    public void TryRename_DoesNotOverwriteAnExistingTarget()
    {
        var source = CreateFile("exp_0526_cp_r1d5.mp4", "source");
        var target = CreateFile("exp_0526_cp_d5r2.mp4", "target");
        var renamer = new SessionSourceRenamer();

        var success = renamer.TryRename(source, [], "exp_0526_cp_d5r2", out var result, out var error);

        Assert.False(success);
        Assert.Null(result);
        Assert.Contains("Ya existe", error);
        Assert.Equal("source", File.ReadAllText(source));
        Assert.Equal("target", File.ReadAllText(target));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../exp_0526_cp_d5r2")]
    [InlineData("exp_0526_cp_d5r2.mp4")]
    public void TryRename_RejectsInvalidStem(string stem)
    {
        var source = CreateFile("exp_0526_cp_r1d5.mp4");
        var renamer = new SessionSourceRenamer();

        Assert.False(renamer.TryRename(source, [], stem, out _, out _));
        Assert.True(File.Exists(source));
    }

    private string CreateFile(string name, string contents = "video")
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, contents);
        return path;
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
