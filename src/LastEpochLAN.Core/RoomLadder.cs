namespace LastEpochLAN.Core;

// Current room members report their own offline character. No world/combat authority.
public sealed record RoomLadderCharacter
{
    public string CharacterId { get; init; } = "";
    public string CharacterName { get; init; } = "";
    public int Class { get; init; }
    public string Mastery { get; init; } = "";
    public int Level { get; init; }
    public long Experience { get; init; }
    public int ArenaWave { get; init; }
    public int Corruption { get; init; }
    public int OmenKills { get; init; }
    public int OmenWindows { get; init; }
    public int Deaths { get; init; }
    public bool Hardcore { get; init; }
    public int Cycle { get; init; }
    public string[] Skills { get; init; } = Array.Empty<string>();
    public bool IsValid() => CharacterId is {Length: > 0 and <= 128} &&
        CharacterName is {Length: > 0 and <= 64} && !CharacterName.Any(char.IsControl) &&
        Class is >= 0 and <= 4 && Mastery is {Length: <= 64} && !Mastery.Any(char.IsControl) &&
        Level is >= 1 and <= 100 && Experience is >= 0 and <= 1000000000000000L &&
        ArenaWave is >= 0 and <= 1000000 && Corruption is >= 0 and <= 1000000 &&
        OmenKills is >= 0 and <= 100000000 && OmenWindows is >= 0 and <= 100000000 &&
        Deaths is >= 0 and <= 100000000 && Cycle is >= 0 and <= 255 &&
        Skills is {Length: <= 5} && Skills.All(s => s is {Length: > 0 and <= 128} && !s.Any(char.IsControl));
    public RoomLadderCharacter Copy() => this with {Skills = Skills.ToArray()};
}

public enum RoomLadderMetric { Arena, Level, Corruption, Omen }
public sealed record RoomLadderRow(string PlayerId, string AccountName, RoomLadderCharacter Character, long Rank);

public static class RoomLadder
{
    public static RoomLadderRow[] Select(IEnumerable<PlayerState> players, RoomLadderMetric metric,
        bool hardcore, int cycle, int classFilter = -1)
    {
        var entries = players.Where(p => p.Ladder is {} c && c.IsValid() && c.Hardcore == hardcore &&
                c.Cycle == cycle && (classFilter < 0 || c.Class == classFilter))
            .GroupBy(p => p.Id, StringComparer.Ordinal).Select(g => g.OrderByDescending(p => p.Sequence).First())
            .Select(p => new RoomLadderRow(p.Id, p.Name, p.Ladder!.Copy(), 0));
        // Level first, then within-level experience: avoids double precision losing XP ties.
        long Score(RoomLadderCharacter c) => metric switch {
            RoomLadderMetric.Arena => c.ArenaWave, RoomLadderMetric.Level => c.Level,
            RoomLadderMetric.Corruption => c.Corruption, _ => c.OmenKills};
        long Secondary(RoomLadderCharacter c) => metric == RoomLadderMetric.Level ? c.Experience :
            metric == RoomLadderMetric.Omen ? c.OmenWindows : 0;
        return entries.OrderByDescending(p => Score(p.Character)).ThenByDescending(p => Secondary(p.Character))
            .ThenBy(p => p.Character.CharacterId, StringComparer.Ordinal).ThenBy(p => p.PlayerId, StringComparer.Ordinal)
            .Select((p, i) => p with {Rank = i + 1}).ToArray();
    }
}
