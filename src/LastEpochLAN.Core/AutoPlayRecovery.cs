namespace LastEpochLAN.Core;

// Only retry reads. A failed native action may already have side effects and
// must be stopped for explicit review instead of being replayed automatically.
public sealed class AutoPlayRecovery
{
    public int ReadFailures { get; private set; }
    public double RetryAt { get; private set; }
    public bool Failed(double now, bool actionAttempted)
    {
        if(actionAttempted) return false;
        ReadFailures++;
        RetryAt=now+.25;
        return ReadFailures<4;
    }
    public void Reset(){ReadFailures=0;RetryAt=0;}
}
