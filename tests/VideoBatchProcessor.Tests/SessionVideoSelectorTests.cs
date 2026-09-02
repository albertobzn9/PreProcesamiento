using VideoBatchProcessor.Core.SessionFiles;

namespace VideoBatchProcessor.Tests;

public sealed class SessionVideoSelectorTests
{
    [Fact]
    public void SelectOnePerSession_PrefiereMp4YConservaElAlternativoComoReferencia()
    {
        var selected = SessionVideoSelector.SelectOnePerSession(
        [
            "/tmp/exp_0526_cs_d4r1.mkv",
            "/tmp/exp_0526_cs_d4r1.mp4",
            "/tmp/exp_0526_cs_d4r4.mkv",
            "/tmp/exp_0526_cs_d4r4.mp4",
        ]);

        Assert.Equal(2, selected.Count);
        Assert.All(selected, item => Assert.EndsWith(".mp4", item.VideoPath, StringComparison.OrdinalIgnoreCase));
        Assert.All(selected, item => Assert.Single(item.AlternateVideoPaths));
        Assert.All(selected, item => Assert.EndsWith(".mkv", item.AlternateVideoPaths[0], StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SelectOnePerSession_UsaMkvCuandoNoExisteMp4()
    {
        var selected = SessionVideoSelector.SelectOnePerSession(
        [
            "/tmp/exp_0526_cs_d4r1.mkv",
            "/tmp/exp_0526_cs_d4r4.mp4",
        ]);

        Assert.Equal(2, selected.Count);
        Assert.Contains(selected, item => item.VideoPath.EndsWith("d4r1.mkv", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(selected, item => item.VideoPath.EndsWith("d4r4.mp4", StringComparison.OrdinalIgnoreCase));
    }
}
