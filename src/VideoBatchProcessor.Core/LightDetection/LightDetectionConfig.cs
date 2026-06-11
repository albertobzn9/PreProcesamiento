namespace VideoBatchProcessor.Core.LightDetection;

public sealed record LightDetectionConfig
{
    public LightDetectionConfig(
        LightRoi foodLeft,
        LightRoi foodRight,
        LightRoi noiseLed)
    {
        if (foodLeft.Light != LightId.FoodLeft)
            throw new ArgumentException("FoodLeft ROI must use LightId.FoodLeft.", nameof(foodLeft));
        if (foodRight.Light != LightId.FoodRight)
            throw new ArgumentException("FoodRight ROI must use LightId.FoodRight.", nameof(foodRight));
        if (noiseLed.Light != LightId.NoiseLed)
            throw new ArgumentException("NoiseLed ROI must use LightId.NoiseLed.", nameof(noiseLed));

        FoodLeft = foodLeft;
        FoodRight = foodRight;
        NoiseLed = noiseLed;
    }

    public LightRoi FoodLeft { get; }
    public LightRoi FoodRight { get; }
    public LightRoi NoiseLed { get; }
}
