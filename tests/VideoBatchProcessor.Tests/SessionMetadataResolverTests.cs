using VideoBatchProcessor.Core.Nomenclature;
using VideoBatchProcessor.Core.SessionResolver;
using Xunit;

namespace VideoBatchProcessor.Tests;

public class SessionMetadataResolverTests
{
    private readonly NomenclatureParser _parser = new();
    private readonly SessionMetadataResolver _resolver = new();

    private SessionMetadata Resolve(string fp, BatchManifest? m = null)
    {
        _parser.TryParse(fp, out var p);
        return _resolver.Resolve(p, m, fp);
    }

    [Fact]
    public void Legacy_ConManifest_EsCompleto()
    {
        var meta = Resolve("/s/exp_0122_dis_d22r1.mp4",
            new BatchManifest { Iniciales="abs", Sexo="m", Tratamiento="stx" });
        Assert.True(meta.IsComplete);
    }

    [Fact]
    public void Legacy_NormalizaFecha()
    {
        var meta = Resolve("/s/exp_0122_dis_d22r1.mp4",
            new BatchManifest { Iniciales="abs", Sexo="m", Tratamiento="stx" });
        Assert.Equal("2201", meta.Fecha);
    }

    [Fact]
    public void Legacy_NormalizaFase_dis()
    {
        var meta = Resolve("/s/exp_0122_dis_d22r1.mp4",
            new BatchManifest { Iniciales="abs", Sexo="m", Tratamiento="stx" });
        Assert.Equal("f5", meta.Fase);
    }

    [Fact]
    public void Legacy_NormalizaFase_pb()
    {
        var meta = Resolve("/s/exp_0526_pb_d31r4.mp4",
            new BatchManifest { Iniciales="abs", Sexo="h", Tratamiento="dzp" });
        Assert.Equal("f6", meta.Fase);
    }

    [Fact]
    public void Legacy_SinManifest_NoEsCompleto()
    {
        var meta = Resolve("/s/exp_0122_dis_d22r1.mp4");
        Assert.False(meta.IsComplete);
        Assert.Contains("Iniciales", meta.MissingFields);
        Assert.Contains("Sexo", meta.MissingFields);
        Assert.Contains("Tratamiento", meta.MissingFields);
    }

    [Fact]
    public void Legacy_SinManifest_FechaFaseDisponibles()
    {
        var meta = Resolve("/s/exp_0122_cs_d7r1.mp4");
        Assert.Equal("2201", meta.Fecha);
        Assert.Equal("f2", meta.Fase);
        Assert.Equal(7, meta.Dia);
    }

    [Fact]
    public void LabStandard_SinManifest_EsCompleto()
    {
        var meta = Resolve("/s/abs_2201_f5_d22r1_m_e1_s_stx.mp4");
        Assert.True(meta.IsComplete);
    }

    [Fact]
    public void FormatoNoReconocido_Flag()
    {
        var meta = Resolve("/s/video_raro.mp4");
        Assert.True(meta.FormatoNoReconocido);
        Assert.False(meta.IsComplete);
    }

    [Fact]
    public void FormatoNoReconocido_TodosCamposFaltantes()
    {
        var meta = Resolve("/s/video_raro.mp4");
        Assert.Contains("Iniciales", meta.MissingFields);
        Assert.Contains("Fecha", meta.MissingFields);
        Assert.Contains("Fase", meta.MissingFields);
        Assert.Contains("Dia", meta.MissingFields);
        Assert.Contains("Rata", meta.MissingFields);
        Assert.Contains("Sexo", meta.MissingFields);
        Assert.Contains("Tratamiento", meta.MissingFields);
    }

    [Fact]
    public void FormatoNoReconocido_MensajeContieneEjemplos()
    {
        var msg = SessionMetadataResolver.MensajeFormatoNoReconocido;
        Assert.Contains("exp_", msg);
        Assert.Contains("_f5_", msg);
    }

    [Fact]
    public void Complete_Legacy_EsCompleto()
    {
        var partial = Resolve("/s/exp_0122_dis_d22r1.mp4");
        var completa = _resolver.Complete(partial,
            new UserFieldValues { Iniciales="abs", Sexo="m", Tratamiento="stx" });
        Assert.True(completa.IsComplete);
    }

    [Fact]
    public void Complete_NoSobreescribeCampos()
    {
        var partial = Resolve("/s/exp_0122_dis_d22r1.mp4");
        var completa = _resolver.Complete(partial,
            new UserFieldValues { Iniciales="xyz", Sexo="h", Tratamiento="dzp" });
        Assert.Equal("2201", completa.Fecha);
        Assert.Equal("f5", completa.Fase);
        Assert.Equal(22, completa.Dia);
    }

    [Fact]
    public void Complete_FormatoNoReconocido_Completo()
    {
        var partial = Resolve("/s/video_raro.mp4");
        var completa = _resolver.Complete(partial, new UserFieldValues
        {
            Iniciales="abs", Fecha="2601", Fase="f5",
            Dia=1, Rata=3, Sexo="m", Tratamiento="stx"
        });
        Assert.True(completa.IsComplete);
        Assert.Equal("2601", completa.Fecha);
    }

    [Fact]
    public void Complete_FormatoNoReconocido_Parcial()
    {
        var partial = Resolve("/s/video_raro.mp4");
        var incompleta = _resolver.Complete(partial,
            new UserFieldValues { Iniciales="abs", Sexo="m" });
        Assert.False(incompleta.IsComplete);
        Assert.Contains("Fecha", incompleta.MissingFields);
    }

    [Fact]
    public void MatPath_MismaCarpeta()
    {
        var meta = Resolve("/sesiones/exp_0122_dis_d22r1.mp4",
            new BatchManifest { Iniciales="abs", Sexo="m", Tratamiento="stx" });
        Assert.Equal("/sesiones/exp_0122_dis_d22r1.mat", meta.SourceMatPath);
    }

    [Fact]
    public void Complete_MatPath_Override()
    {
        var partial = Resolve("/s/exp_0122_dis_d22r1.mp4");
        var completa = _resolver.Complete(partial, new UserFieldValues
        {
            Iniciales="abs", Sexo="m", Tratamiento="stx",
            MatPath="/mats/exp_0122_dis_d22r1.mat"
        });
        Assert.Equal("/mats/exp_0122_dis_d22r1.mat", completa.SourceMatPath);
    }

    [Fact]
    public void Override_ReemplazaSexo()
    {
        var manifest = new BatchManifest
        {
            Iniciales="abs", Sexo="m", Tratamiento="stx",
            Overrides = new Dictionary<string, FileOverride>
            {
                ["exp_0122_dis_d30r4"] = new FileOverride { Sexo="h" }
            }
        };
        var meta = Resolve("/s/exp_0122_dis_d30r4.mp4", manifest);
        Assert.Equal("h", meta.Sexo);
        Assert.True(meta.IsComplete);
    }

    [Fact]
    public void FlujoCompleto_Legacy_A_NombreVBP()
    {
        var partial = Resolve("/s/exp_0122_dis_d22r1.mp4");
        var meta = _resolver.Complete(partial,
            new UserFieldValues { Iniciales="abs", Sexo="m", Tratamiento="stx" });
        var nombre = NomenclatureParser.BuildVbpOutputName(
            meta.Iniciales, meta.Fecha, meta.Fase,
            meta.Dia, meta.Rata, meta.Sexo, "e1", "s", "cr", meta.Tratamiento);
        Assert.Equal("abs_2201_f5_d22r1_m_e1_s_cr_stx.mp4", nombre);
    }
}
