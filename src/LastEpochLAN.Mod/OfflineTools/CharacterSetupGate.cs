namespace LastEpochLAN.Mod.OfflineTools;

// Pure lifecycle guard, shared with the managed regression fixture.
internal sealed class CharacterSetupGate
{
    private string expectedName = "";
    private int expectedClass;
    private HashSet<string> existingIds = new();
    private double expiresAt;
    private string stableIdentity = "";
    private double stableSince;
    internal bool Pending(double now) => !string.IsNullOrEmpty(expectedName) && now < expiresAt;

    internal void Arm(string name, int characterClass, IEnumerable<string> ids, double now)
    {
        expectedName = name?.Trim() ?? "";
        expectedClass = characterClass;
        existingIds = new(ids, StringComparer.Ordinal);
        expiresAt = now + 600;
        ResetReadiness();
    }

    internal bool ConsumeCreation(string id, string name, int characterClass, double now)
    {
        if (!Pending(now) || string.IsNullOrEmpty(id) ||
            existingIds.Contains(id) || name != expectedName || characterClass != expectedClass) return false;
        expectedName = "";
        return true;
    }

    internal void ResetReadiness() => stableIdentity = "";

    internal bool Ready(string identity, bool playable, double now)
    {
        if (!playable || string.IsNullOrEmpty(identity)) { ResetReadiness(); return false; }
        if (stableIdentity != identity)
        { stableIdentity = identity; stableSince = now; return false; }
        return now - stableSince >= 2;
    }
}
