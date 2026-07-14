namespace VideoBatchProcessor.Core.LightDetection;

/// <summary>
/// Convierte lecturas ON/OFF por frame en transiciones estables e ignora
/// cambios aislados que no duran lo suficiente. Esta clase no abre videos;
/// recibe <see cref="LightSample"/> ya medidos para conservarla comprobable de
/// forma aislada.
/// </summary>
public sealed class LightTimelineBuilder
{
    private static readonly LightId[] AllLights =
    [
        LightId.FoodLeft,
        LightId.FoodRight,
        LightId.NoiseLed,
    ];

    private readonly LightTimelineConfig _config;

    public LightTimelineBuilder(LightTimelineConfig? config = null)
    {
        _config = config ?? new LightTimelineConfig();
        _config.Validate();
    }

    public LightTimeline Build(IEnumerable<LightSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var trackers = AllLights.ToDictionary(light => light, _ => new StateTracker());
        var transitions = new List<LightTransition>();
        var samplesAnalyzed = 0;
        LightSample? previous = null;

        foreach (var sample in samples)
        {
            ArgumentNullException.ThrowIfNull(sample);
            ValidateOrder(previous, sample);

            foreach (var light in AllLights)
            {
                var state = GetState(sample, light);
                var tracker = trackers[light];
                tracker.Observe(
                    state,
                    sample,
                    light,
                    _config,
                    transitions);
            }

            previous = sample;
            samplesAnalyzed++;
        }

        return new LightTimeline(samplesAnalyzed, transitions);
    }

    private static void ValidateOrder(LightSample? previous, LightSample current)
    {
        if (current.FrameIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(current), "El índice de frame no puede ser negativo.");
        if (current.TimeSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(current), "El tiempo de frame no puede ser negativo.");

        if (previous is null)
            return;

        if (current.FrameIndex <= previous.FrameIndex || current.TimeSeconds < previous.TimeSeconds)
            throw new ArgumentException("Las muestras deben llegar en orden estricto de frame y tiempo.", nameof(current));
    }

    private static bool GetState(LightSample sample, LightId light) => light switch
    {
        LightId.FoodLeft => sample.IsFoodLeftOn,
        LightId.FoodRight => sample.IsFoodRightOn,
        LightId.NoiseLed => sample.IsNoiseLedOn,
        _ => throw new ArgumentOutOfRangeException(nameof(light), light, "Luz no reconocida."),
    };

    private sealed class StateTracker
    {
        private bool? _stableState;
        private bool? _candidateState;
        private int _candidateCount;
        private LightSample? _candidateStart;

        public void Observe(
            bool observedState,
            LightSample sample,
            LightId light,
            LightTimelineConfig config,
            ICollection<LightTransition> transitions)
        {
            if (_candidateState != observedState)
            {
                _candidateState = observedState;
                _candidateCount = 1;
                _candidateStart = sample;
            }
            else
            {
                _candidateCount++;
            }

            var requiredCount = observedState
                ? config.MinimumConsecutiveOnSamples
                : config.MinimumConsecutiveOffSamples;
            if (_candidateCount < requiredCount)
                return;

            if (_stableState is null)
            {
                _stableState = observedState;
                return;
            }

            if (_stableState == observedState)
                return;

            var candidateStart = _candidateStart!;
            transitions.Add(new LightTransition(
                light,
                _stableState.Value,
                observedState,
                candidateStart.FrameIndex,
                candidateStart.TimeSeconds,
                sample.FrameIndex));
            _stableState = observedState;
        }
    }
}
