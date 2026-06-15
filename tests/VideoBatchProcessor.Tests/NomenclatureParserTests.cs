using VideoBatchProcessor.Core.Nomenclature;
using Xunit;

namespace VideoBatchProcessor.Tests;

public class NomenclatureParserTests
{
    private readonly NomenclatureParser _parser = new();

    // ── 1. Detección de esquema ───────────────────────────────────────────

    [Fact] public void Legacy_DetectaScheme()
    {
        Assert.True(_parser.TryParse("exp_0122_cs_d7r1.mp4", out var r));
        Assert.Equal(NamingScheme.LegacySession, r.Scheme);
    }

    [Fact] public void LabStandard_DetectaScheme()
    {
        Assert.True(_parser.TryParse("abs_2201_f2_d7r1_m_e1_s_stx.mp4", out var r));
        Assert.Equal(NamingScheme.LabStandard, r.Scheme);
    }

    [Fact] public void VbpOutput_DetectaScheme()
    {
        Assert.True(_parser.TryParse("abs_2201_f2_d7r1_m_e1_s_cr_stx.mp4", out var r));
        Assert.Equal(NamingScheme.VideoBatchOutput, r.Scheme);
    }

    [Fact] public void Desconocido_DevuelveFalse()
    {
        Assert.False(_parser.TryParse("video_sin_formato.mp4", out _));
    }

    // ── 2. FechaFormato ──────────────────────────────────────────────────

    [Fact] public void Legacy_FechaFormato_EsMMYY()
    {
        _parser.TryParse("exp_0122_cs_d7r1.mp4", out var r);
        Assert.Equal(FechaFormato.MMYY, r.FechaFormato);
        Assert.Equal("0122", r.Fecha);  // MMYY: 01=enero, 22=2022
    }

    [Fact] public void LabStandard_FechaFormato_EsYYMM()
    {
        _parser.TryParse("abs_2201_f2_d7r1_m_e1_s_stx.mp4", out var r);
        Assert.Equal(FechaFormato.YYMM, r.FechaFormato);
        Assert.Equal("2201", r.Fecha);  // YYMM: 22=2022, 01=enero
    }

    [Fact] public void VbpOutput_FechaFormato_EsYYMM()
    {
        _parser.TryParse("abs_2201_f2_d7r1_m_e1_s_cr_stx.mp4", out var r);
        Assert.Equal(FechaFormato.YYMM, r.FechaFormato);
    }

    // ── 3. FaseEstandar — legacy codes → f-codes ────────────────────────

    [Fact] public void FaseEstandar_cs_EsF2()
    {
        _parser.TryParse("exp_0122_cs_d7r1.mp4", out var r);
        Assert.Equal("cs", r.Fase);
        Assert.Equal("f2", r.FaseEstandar);
    }

    [Fact] public void FaseEstandar_cm_EsF3()
    {
        _parser.TryParse("exp_0122_cm_d12r1.mp4", out var r);
        Assert.Equal("f3", r.FaseEstandar);
    }

    [Fact] public void FaseEstandar_cp_EsF4()
    {
        _parser.TryParse("exp_0122_cp_d17r1.mp4", out var r);
        Assert.Equal("f4", r.FaseEstandar);
    }

    [Fact] public void FaseEstandar_dis_EsF5()
    {
        _parser.TryParse("exp_0122_dis_d22r1.mp4", out var r);
        Assert.Equal("f5", r.FaseEstandar);
    }

    [Fact] public void FaseEstandar_pb_EsF6()
    {
        _parser.TryParse("exp_0122_pb_d31r1.mp4", out var r);
        Assert.Equal("pb", r.Fase);
        Assert.Equal("f6", r.FaseEstandar);
    }

    [Fact] public void FaseEstandar_LabStandard_PasaDirecto()
    {
        // Lab/VBP ya vienen con f-code, FaseEstandar los devuelve sin cambio
        _parser.TryParse("abs_2201_f5_d22r1_m_e1_s_stx.mp4", out var r);
        Assert.Equal("f5", r.Fase);
        Assert.Equal("f5", r.FaseEstandar);
    }

    [Fact] public void FaseEstandar_VbpOutput_PasaDirecto()
    {
        _parser.TryParse("abs_2605_f6_d31r4_h_e1_s_cr_dzp.mp4", out var r);
        Assert.Equal("f6", r.Fase);
        Assert.Equal("f6", r.FaseEstandar);
    }

    // ── 4. ConvertirFechaAYYMM ───────────────────────────────────────────

    [Fact] public void ConvertirFecha_0122_Da_2201()
    {
        Assert.Equal("2201", NomenclatureParser.ConvertirFechaAYYMM("0122"));
    }

    [Fact] public void ConvertirFecha_0526_Da_2605()
    {
        Assert.Equal("2605", NomenclatureParser.ConvertirFechaAYYMM("0526"));
    }

    [Fact] public void ConvertirFecha_0126_Da_2601()
    {
        Assert.Equal("2601", NomenclatureParser.ConvertirFechaAYYMM("0126"));
    }

    [Fact] public void ConvertirFecha_ArgumentoInvalido_LanzaExcepcion()
    {
        Assert.Throws<ArgumentException>(() => NomenclatureParser.ConvertirFechaAYYMM("012"));
    }

    // ── 5. Metadata completa — Legacy ────────────────────────────────────

    [Fact] public void Legacy_Metadata_exp0122_cs_d7r1()
    {
        _parser.TryParse("exp_0122_cs_d7r1.mp4", out var r);
        Assert.Equal("0122", r.Fecha);
        Assert.Equal("cs",   r.Fase);
        Assert.Equal(7,      r.Dia);
        Assert.Equal(1,      r.Rata);
        Assert.Null(r.Iniciales);
        Assert.Null(r.Resultado);
    }

    [Fact] public void Legacy_Metadata_exp0526_dis_d30r4()
    {
        _parser.TryParse("exp_0526_dis_d30r4.mat", out var r);
        Assert.Equal("0526", r.Fecha);
        Assert.Equal("dis",  r.Fase);
        Assert.Equal("f5",   r.FaseEstandar);
        Assert.Equal(30,     r.Dia);
        Assert.Equal(4,      r.Rata);
        Assert.Equal(".mat", r.Extension);
    }

    // ── 6. Metadata completa — Lab Standard ─────────────────────────────

    [Fact] public void LabStandard_Metadata_abs2201_f2_d7r1()
    {
        _parser.TryParse("abs_2201_f2_d7r1_m_e1_s_stx.mp4", out var r);
        Assert.Equal("abs",  r.Iniciales);
        Assert.Equal("2201", r.Fecha);
        Assert.Equal("f2",   r.Fase);
        Assert.Equal("f2",   r.FaseEstandar);
        Assert.Equal(7,      r.Dia);
        Assert.Equal(1,      r.Rata);
        Assert.Equal("m",    r.Sexo);
        Assert.Equal("e1",   r.Segmento);
        Assert.Equal(TipoEnsayo.Seguro, r.Tipo);
        Assert.Equal("stx",  r.Tratamiento);
        Assert.Null(r.Resultado);  // LabStandard no tiene resultado
    }

    [Fact] public void LabStandard_Metadata_abs2605_f4_d17r1_dzp()
    {
        _parser.TryParse("abs_2605_f4_d17r1_m_e1_p_dzp.mp4", out var r);
        Assert.Equal("f4",   r.Fase);
        Assert.Equal("f4",   r.FaseEstandar);
        Assert.Equal(TipoEnsayo.Peligroso, r.Tipo);
        Assert.Equal("dzp",  r.Tratamiento);
    }

    // ── 7. Metadata completa — VBP Output ───────────────────────────────

    [Fact] public void VbpOutput_Cruce()
    {
        _parser.TryParse("abs_2201_f2_d7r1_m_e1_s_cr_stx.mp4", out var r);
        Assert.Equal(ResultadoConductual.Cruce,   r.Resultado);
        Assert.Equal(TipoSegmento.Evento,         r.SegmentoTipo);
        Assert.Equal(1,                           r.SegmentoNumero);
    }

    [Fact] public void VbpOutput_NoCruce()
    {
        _parser.TryParse("abs_2201_f2_d7r1_m_e2_s_nc_stx.mp4", out var r);
        Assert.Equal(ResultadoConductual.NoCruce, r.Resultado);
        Assert.Equal(2,                           r.SegmentoNumero);
    }

    [Fact] public void VbpOutput_Timeout()
    {
        _parser.TryParse("abs_2201_f2_d7r1_m_e3_s_to_stx.mp4", out var r);
        Assert.Equal(ResultadoConductual.Timeout, r.Resultado);
    }

    [Fact] public void VbpOutput_ITI()
    {
        _parser.TryParse("abs_2201_f2_d7r1_m_iti1_na_na_stx.mp4", out var r);
        Assert.Equal(TipoSegmento.ITI,            r.SegmentoTipo);
        Assert.Equal(1,                           r.SegmentoNumero);
        Assert.Equal(TipoEnsayo.NoAplica,         r.Tipo);
        Assert.Equal(ResultadoConductual.NoAplica,r.Resultado);
    }

    [Fact] public void VbpOutput_Habituacion()
    {
        _parser.TryParse("abs_2201_f2_d7r1_m_hab_na_na_stx.mp4", out var r);
        Assert.Equal(TipoSegmento.Habituacion, r.SegmentoTipo);
        Assert.Null(r.SegmentoNumero);
    }

    [Fact] public void VbpOutput_HabituacionInicial()
    {
        _parser.TryParse("abs_2201_f2_d7r1_m_habini_na_na_stx.mp4", out var r);
        Assert.Equal(TipoSegmento.HabituacionInicial, r.SegmentoTipo);
    }

    [Fact] public void VbpOutput_HabituacionFinal()
    {
        _parser.TryParse("abs_2201_f2_d7r1_m_habfin_na_na_stx.mp4", out var r);
        Assert.Equal(TipoSegmento.HabituacionFinal, r.SegmentoTipo);
    }

    [Fact] public void VbpOutput_FasePb_f6()
    {
        _parser.TryParse("abs_2605_f6_d31r4_h_e1_s_cr_dzp.mp4", out var r);
        Assert.Equal(NamingScheme.VideoBatchOutput, r.Scheme);
        Assert.Equal("f6", r.FaseEstandar);
        Assert.Equal(ResultadoConductual.Cruce, r.Resultado);
    }

    // ── 8. BuildVbpOutputName ────────────────────────────────────────────

    [Fact] public void Build_Evento_Cruce()
    {
        var n = NomenclatureParser.BuildVbpOutputName(
            "abs","2201","f2",7,1,"m","e1","s","cr","stx");
        Assert.Equal("abs_2201_f2_d7r1_m_e1_s_cr_stx.mp4", n);
    }

    [Fact] public void Build_ITI()
    {
        var n = NomenclatureParser.BuildVbpOutputName(
            "abs","2201","f2",7,1,"m","iti1","na","na","stx");
        Assert.Equal("abs_2201_f2_d7r1_m_iti1_na_na_stx.mp4", n);
    }

    [Fact] public void Build_Habituacion_Dzp()
    {
        var n = NomenclatureParser.BuildVbpOutputName(
            "abs","2605","f6",31,4,"h","hab","na","na","dzp");
        Assert.Equal("abs_2605_f6_d31r4_h_hab_na_na_dzp.mp4", n);
    }

    // ── 9. Flujo completo Legacy → VBP Output ───────────────────────────

    [Fact] public void FlujoCompleto_Legacy_A_VbpOutput()
    {
        // Input: archivo legacy
        _parser.TryParse("exp_0122_dis_d22r1.mp4", out var r);

        // Normalizar fecha (MMYY → YYMM) y fase (dis → f5)
        var fechaYYMM  = NomenclatureParser.ConvertirFechaAYYMM(r.Fecha!);
        var faseStd    = r.FaseEstandar!;

        // Construir nombre de output VBP con datos del .mat (simulados aquí)
        var outputName = NomenclatureParser.BuildVbpOutputName(
            "abs", fechaYYMM, faseStd, r.Dia, r.Rata,
            "m", "e1", "s", "cr", "stx");

        Assert.Equal("abs_2201_f5_d22r1_m_e1_s_cr_stx.mp4", outputName);
    }
}