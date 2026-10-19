using System.Text.Json;

namespace LastEpochLAN.Core;

public sealed record EnemyTemplate(int Id, string Name);
public sealed record TypedEnemy(int Type, EnemyState State);
public sealed record WorldChunk
{
    public long Epoch { get; init; }
    public long Revision { get; init; }
    public int Index { get; init; }
    public int Count { get; init; }
    public int TotalEnemies { get; init; }
    public long BaseRevision {get;init;}=-1;
    public int ChangedEnemies {get;init;}
    public int[] RemovedEnemies {get;init;}=Array.Empty<int>();
    public int BossId { get; init; }
    public WorldState? Header { get; init; }
    public EnemyTemplate[] Templates { get; init; } = Array.Empty<EnemyTemplate>();
    public TypedEnemy[] Enemies { get; init; } = Array.Empty<TypedEnemy>();
}

/// <summary>Type definitions are cached per connection; a world commits only after all ordered chunks arrive.</summary>
public static class WorldTransfer
{
    // Resource-safety bound for assembled snapshots, not a per-packet enemy limit.
    public const int MaximumEnemies = 65536;
    public const int EnemiesPerChunk = 64;
    public const int MaximumChunkBytes = 262144;
    public const int MaximumChunks = 8192;
    public const int MaximumSnapshotBytes = 32 * 1024 * 1024;
}

public sealed class WorldChunkWriter
{
    private readonly Dictionary<string, int> templates = new(StringComparer.Ordinal);
    private long epoch = -1;
    private string scene = "";
    private WorldState? previous;

    public WorldChunk[] Encode(WorldState world,bool allowDelta=false)
    {
        if (!world.IsValid()) throw new ArgumentException("Invalid world.");
        if (epoch != world.Epoch || scene != world.Scene) { templates.Clear(); previous=null;epoch = world.Epoch; scene = world.Scene; }
        // This is one ordered reliable stream. New connections/instances get
        // a baseline; a periodic full resend only produces traffic/GC spikes.
        bool delta=allowDelta && previous!=null && world.Revision>previous.Revision;
        var beforeEnemies=delta ? previous!.Enemies.ToDictionary(e=>e.Id) : new Dictionary<int,EnemyState>();
        var changes=world.Enemies.Where(e=>!delta || !beforeEnemies.TryGetValue(e.Id,out var old) || !WorldContent.EnemySame(old,e)).ToArray();
        var ids=world.Enemies.Select(e=>e.Id).ToHashSet();
        var removed=beforeEnemies.Keys.Where(id=>!ids.Contains(id)).ToArray();
        var definitions = new List<EnemyTemplate>();
        var groups = new List<TypedEnemy[]>();
        var pending = new List<TypedEnemy>();
        int bytes = 0;
        long totalBytes = 0;
        foreach (var enemy in changes)
        {
            if (!templates.TryGetValue(enemy.Template, out int type))
            {
                if (templates.Count == WorldTransfer.MaximumEnemies) throw new InvalidDataException("Type catalogue too large.");
                templates.Add(enemy.Template, type = templates.Count + 1);
                definitions.Add(new EnemyTemplate(type, enemy.Template));
            }
            // Type supplies the name; omit the redundant string field entirely on the wire.
            var entry = new TypedEnemy(type, enemy with { Template = null! });
            int entryBytes = JsonSerializer.SerializeToUtf8Bytes(entry).Length;
            if (entryBytes > WorldTransfer.MaximumChunkBytes) throw new InvalidDataException("Enemy record too large.");
            totalBytes += entryBytes;
            if (totalBytes > WorldTransfer.MaximumSnapshotBytes / 2) throw new InvalidDataException("World payload too large.");
            if (pending.Count != 0 && (pending.Count == WorldTransfer.EnemiesPerChunk || bytes + entryBytes > WorldTransfer.MaximumChunkBytes))
            { groups.Add(pending.ToArray()); pending.Clear(); bytes = 0; }
            pending.Add(entry); bytes += entryBytes;
        }
        if (pending.Count != 0 || groups.Count == 0) groups.Add(pending.ToArray());
        // Catalogue definitions have their own bounded chunks, including an initial metadata-only chunk.
        var chunks = new List<WorldChunk> { new() { Header = world with { Enemies = Array.Empty<EnemyState>(), BossId = 0 } } };
        foreach(var group in removed.Chunk(64)) chunks.Add(new WorldChunk {RemovedEnemies=group});
        foreach (var group in definitions.Chunk(512)) chunks.Add(new WorldChunk { Templates = group });
        foreach (var group in groups) chunks.Add(new WorldChunk { Enemies = group });
        if (chunks.Count > WorldTransfer.MaximumChunks) throw new InvalidDataException("Too many world chunks.");
        var result=chunks.Select((chunk, index) => chunk with { Epoch = world.Epoch, Revision = world.Revision, Index = index,
            Count = chunks.Count, TotalEnemies = world.Enemies.Length, BossId = world.BossId,
            BaseRevision=delta ? previous!.Revision : -1,ChangedEnemies=changes.Length }).ToArray();
        previous=world;return result;
    }
}

public sealed class WorldChunkReader
{
    private readonly Dictionary<int, string> templates = new();
    private readonly List<EnemyState> enemies = new();
    private WorldChunk? first;
    private int next;
    private long receivedBytes;
    private long epoch = -1, committedRevision = -1;
    private string scene = "";
    private WorldState? committed;
    private readonly HashSet<int> removed=new();
    public bool Pending => first != null;

    public WorldState? Accept(WorldChunk chunk, int frameBytes = 0)
    {
        if (chunk.Count is < 2 or > WorldTransfer.MaximumChunks || chunk.Index < 0 || chunk.Index >= chunk.Count ||
            chunk.TotalEnemies is < 0 or > WorldTransfer.MaximumEnemies || chunk.Epoch < 0 || chunk.Revision < 0 || chunk.BossId < 0 ||
            chunk.BaseRevision < -1 || chunk.ChangedEnemies<0 || chunk.ChangedEnemies>chunk.TotalEnemies ||
            chunk.RemovedEnemies is not {Length: <=64} ||
            chunk.Enemies is not { Length: <= WorldTransfer.EnemiesPerChunk } || chunk.Templates is not { Length: <= 512 })
            throw new InvalidDataException("Invalid world chunk.");
        if (chunk.Index == 0)
        {
            if (Pending || chunk.Header == null || !chunk.Header.IsValid() || chunk.Header.Enemies.Length != 0 ||
                chunk.Header.Epoch != chunk.Epoch || chunk.Header.Revision != chunk.Revision || chunk.Header.BossId != 0 ||
                chunk.Enemies.Length != 0 || chunk.Templates.Length != 0 || chunk.Epoch < epoch ||
                chunk.RemovedEnemies.Length!=0 ||
                (chunk.Epoch == epoch && chunk.Revision <= committedRevision)) throw new InvalidDataException("Invalid world baseline.");
            if (epoch == chunk.Epoch && committedRevision >= 0 && scene != chunk.Header.Scene) throw new InvalidDataException("Scene changed without new epoch.");
            if(chunk.BaseRevision>=0 && (committed==null || committed.Epoch!=chunk.Epoch || committed.Scene!=chunk.Header.Scene || committed.Revision!=chunk.BaseRevision))
                throw new InvalidDataException("Delta does not match committed world baseline.");
            if(chunk.BaseRevision<0 && chunk.ChangedEnemies!=chunk.TotalEnemies)
                throw new InvalidDataException("Incomplete baseline declaration.");
            if (epoch != chunk.Epoch || scene != chunk.Header.Scene) { templates.Clear(); epoch = chunk.Epoch; scene = chunk.Header.Scene; }
            first = chunk; next = 0; receivedBytes = 0; enemies.Clear();removed.Clear();
        }
        if (first == null || chunk.Index != next || chunk.Epoch != first.Epoch || chunk.Revision != first.Revision ||
            chunk.Count != first.Count || chunk.TotalEnemies != first.TotalEnemies || chunk.BossId != first.BossId ||
            chunk.BaseRevision!=first.BaseRevision || chunk.ChangedEnemies!=first.ChangedEnemies ||
            (chunk.Index != 0 && chunk.Header != null)) throw new InvalidDataException("Out-of-order world chunk.");
        int size = frameBytes > 0 ? frameBytes : JsonSerializer.SerializeToUtf8Bytes(chunk).Length;
        receivedBytes += size;
        if (size > Wire.MaximumFrameBytes || receivedBytes > WorldTransfer.MaximumSnapshotBytes) throw new InvalidDataException("World assembly exceeds payload budget.");
        foreach (var definition in chunk.Templates)
        {
            if (definition == null || definition.Id is < 1 or > WorldTransfer.MaximumEnemies || definition.Name is not { Length: > 0 and <= 256 } ||
                templates.ContainsKey(definition.Id) || templates.Count >= WorldTransfer.MaximumEnemies)
                throw new InvalidDataException("Invalid enemy type definition.");
            templates.Add(definition.Id, definition.Name);
        }
        foreach (var entry in chunk.Enemies)
        {
            if (entry?.State == null || !string.IsNullOrEmpty(entry.State.Template) || !templates.TryGetValue(entry.Type, out var name) || enemies.Count >= first.ChangedEnemies)
                throw new InvalidDataException("Invalid typed enemy.");
            var restored = entry.State with { Template = name };
            if (!restored.IsValid()) throw new InvalidDataException("Invalid enemy record.");
            enemies.Add(restored);
        }
        foreach(var id in chunk.RemovedEnemies)
            if(first.BaseRevision<0 || id<=0 || !removed.Add(id) || removed.Count>WorldTransfer.MaximumEnemies) throw new InvalidDataException("Invalid removed enemy.");
        if (++next != first.Count) return null;
        if (enemies.Count != first.ChangedEnemies || enemies.Select(e=>e.Id).Distinct().Count()!=enemies.Count)
            throw new InvalidDataException("Incomplete or duplicate world changes.");
        var enemyMap=first.BaseRevision>=0 ? committed!.Enemies.ToDictionary(e=>e.Id) : new Dictionary<int,EnemyState>();
        var changedEnemyIds=enemies.Select(e=>e.Id).ToHashSet();
        foreach(var id in removed) if(!enemyMap.Remove(id) || changedEnemyIds.Contains(id)) throw new InvalidDataException("Invalid enemy removal.");
        foreach(var enemy in enemies) enemyMap[enemy.Id]=enemy;
        if(enemyMap.Count!=first.TotalEnemies) throw new InvalidDataException("Delta world count mismatch.");
        var result = first.Header! with { BossId = first.BossId, Enemies = enemyMap.Values.ToArray() };
        if (!result.IsValid()) throw new InvalidDataException("Invalid assembled world.");
        var deadIds = result.Enemies.Where(e=>e.Dead).Select(e=>e.Id).ToHashSet();
        if (result.RewardEnemyIds.Any(id=>!deadIds.Contains(id))) throw new InvalidDataException("Reward references a missing or living enemy.");
        committed=result;committedRevision = result.Revision; first = null; enemies.Clear();
        return result;
    }
}
