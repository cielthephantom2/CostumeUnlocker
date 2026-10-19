namespace LastEpochLAN.Core;

// Host-authorized attacks are evaluated against the receiving character's native defenses.
public sealed record IncomingDamage
{
    public long Sequence { get; init; }
    public long Epoch { get; init; }
    public string Scene { get; init; } = "";
    public float[] Damage { get; init; } = Array.Empty<float>();
    public float[] Penetration { get; init; } = Array.Empty<float>();
    public float Modifier { get; init; } = 1;
    public float CritChance { get; init; }
    public float CritMultiplier { get; init; } = 1;
    public bool IsHit { get; init; }
    public int Tags { get; init; }
    public float StunChance { get; init; }
    public float StunDuration { get; init; }
    public float FreezeRate { get; init; }
    public bool IsValid() => Sequence > 0 && Epoch >= 0 && Scene is { Length: > 0 and <= 128 } &&
        Damage is { Length: 7 } && Damage.All(v => float.IsFinite(v) && v is >= 0 and <= 100000000) &&
        Penetration is { Length: 7 } && Penetration.All(v => float.IsFinite(v) && Math.Abs(v) <= 1000) &&
        float.IsFinite(Modifier) && Modifier is >= 0 and <= 1000 &&
        new[] { CritChance, CritMultiplier, StunChance, StunDuration, FreezeRate }.All(v => float.IsFinite(v) && Math.Abs(v) <= 100000);
    public IncomingDamage Copy() => this with { Damage = Damage.ToArray(), Penetration = Penetration.ToArray() };
}
