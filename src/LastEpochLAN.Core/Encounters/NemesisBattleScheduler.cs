namespace LastEpochLAN.Core.Encounters;

public sealed record NemesisBattle(Guid Token,EncounterInstance Instance,Guid Encounter,EncounterReservation[] Turns);

// Groups independent Challenge/Empower turns into one Host combat execution.
// Banish does not start a fight. No method reads or mutates anyone's items.
// Native adapter owns spawning and calls Complete only after observing death.
public sealed class NemesisBattleScheduler
{
    private readonly Dictionary<Guid,(EncounterInstance Instance,Guid Token,List<EncounterReservation> Turns,bool Started)> battles=new();
    private readonly HashSet<Guid> seen=new();
    public bool Add(EncounterExecution execution)
    {
        if(execution.View.Kind!=EncounterKind.Nemesis || execution.Reservation.Encounter!=execution.View.Id ||
            execution.Reservation.Action is not (EncounterAction.Challenge or EncounterAction.Empower) ||
            execution.Reservation.CharacterKey=="" || seen.Contains(execution.Reservation.Id))return false;
        if(!battles.TryGetValue(execution.View.Id,out var battle))
            battle=(execution.Instance,Guid.NewGuid(),new List<EncounterReservation>(),false);
        if(battle.Instance!=execution.Instance || battle.Turns.Count>=4 || battle.Turns.Any(t=>t.CharacterKey==execution.Reservation.CharacterKey))return false;
        battle.Turns.Add(execution.Reservation);seen.Add(execution.Reservation.Id);battles[execution.View.Id]=battle;return true;
    }
    public NemesisBattle? Start(Guid encounter)
    {
        if(!battles.TryGetValue(encounter,out var battle) || battle.Started)return null;
        battles[encounter]=battle with {Started=true};
        return new(battle.Token,battle.Instance,encounter,battle.Turns.ToArray());
    }
    public EncounterReservation[] Complete(NemesisBattle execution)
    {
        if(!battles.TryGetValue(execution.Encounter,out var battle) || battle.Token!=execution.Token || battle.Instance!=execution.Instance || !battle.Started)return Array.Empty<EncounterReservation>();
        battles.Remove(execution.Encounter);
        // Includes independently authorized turns admitted while native combat
        // was already active; each retains its own action and outcome token.
        return battle.Turns.ToArray();
    }
    public void EndInstance(){battles.Clear();seen.Clear();}
}
