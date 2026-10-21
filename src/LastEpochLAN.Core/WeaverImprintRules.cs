namespace LastEpochLAN.Core;

public static class WeaverImprintRules
{
    public const float Multiplier = 2f;
    // User verified the unmodded one-point Sealed tooltip is 6%, not the
    // legacy .029 native constant. Pin the requested doubled gameplay rate
    // explicitly rather than presenting .058 as matching that tooltip.
    public const float SealedBaseChance = .06f;
    public static float SealedChance(int points) => points<=0?0:
        (float)Math.Min(1d,(double)SealedBaseChance*Multiplier*points);
    // Weighting/Corruption multipliers are not probabilities: do not clamp them
    // to one before the native consumer has multiplied its other factors.
    public static float DoubleWeight(float value) => float.IsNaN(value) || value <= 0 ? 0 :
        (float)Math.Min(float.MaxValue, (double)value * Multiplier);
    public static float DoubleChance(float value) => Math.Min(1f, DoubleWeight(value));
    public static string DoubleTooltipPercent(string text)
    {
        if(string.IsNullOrWhiteSpace(text))return text;
        // Only a pure percentage value, never numbers embedded in prose/tags or unrelated stats.
        var match=System.Text.RegularExpressions.Regex.Match(text,@"^(\s*[+]?)(\d+(?:[.,]\d+)?)(\s*%\s*)$");
        if(!match.Success || !decimal.TryParse(match.Groups[2].Value.Replace(',','.'),
            System.Globalization.NumberStyles.AllowDecimalPoint,System.Globalization.CultureInfo.InvariantCulture,out var value))return text;
        value=Math.Min(50m,value)*2;
        string number=value.ToString("0.####",System.Globalization.CultureInfo.InvariantCulture);
        if(match.Groups[2].Value.Contains(','))number=number.Replace('.',',');
        return match.Groups[1].Value+number+match.Groups[3].Value;
    }
}
