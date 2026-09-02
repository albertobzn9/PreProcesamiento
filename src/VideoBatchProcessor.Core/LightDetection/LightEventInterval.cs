namespace VideoBatchProcessor.Core.LightDetection;

/// <summary>
/// Periodo visual durante el cual una señal permaneció encendida. Es evidencia
/// de video; todavía no decide a qué fila conductual corresponde.
/// </summary>
public sealed record LightEventInterval(
    int Sequence,
    LightId Light,
    int OnFrameIndex,
    double OnTimeSeconds,
    int? OffFrameIndex,
    double? OffTimeSeconds)
{
    public bool IsComplete => OffFrameIndex is not null && OffTimeSeconds is not null;

    public double? DurationSeconds => IsComplete
        ? OffTimeSeconds!.Value - OnTimeSeconds
        : null;
}

/// <summary>
/// Agrupa los cambios ON/OFF confirmados de una timeline en intervalos que se
/// pueden revisar y exportar. No empata todavía con datos de MATLAB.
/// </summary>
public static class LightEventIntervalBuilder
{
    public static IReadOnlyList<LightEventInterval> Build(LightTimeline timeline)
    {
        ArgumentNullException.ThrowIfNull(timeline);

        var openByLight = new Dictionary<LightId, LightTransition>();
        var intervals = new List<LightEventInterval>();
        var sequence = 0;

        foreach (var transition in timeline.Transitions.OrderBy(item => item.FrameIndex))
        {
            if (transition.IsOn && !transition.WasOn)
            {
                if (openByLight.Remove(transition.Light, out var previousOn))
                {
                    intervals.Add(new LightEventInterval(
                        ++sequence,
                        previousOn.Light,
                        previousOn.FrameIndex,
                        previousOn.TimeSeconds,
                        null,
                        null));
                }

                openByLight[transition.Light] = transition;
                continue;
            }

            if (!transition.IsOn && transition.WasOn && openByLight.Remove(transition.Light, out var on))
            {
                intervals.Add(new LightEventInterval(
                    ++sequence,
                    on.Light,
                    on.FrameIndex,
                    on.TimeSeconds,
                    transition.FrameIndex,
                    transition.TimeSeconds));
            }
        }

        foreach (var on in openByLight.Values.OrderBy(item => item.FrameIndex))
        {
            intervals.Add(new LightEventInterval(
                ++sequence,
                on.Light,
                on.FrameIndex,
                on.TimeSeconds,
                null,
                null));
        }

        return intervals;
    }
}
