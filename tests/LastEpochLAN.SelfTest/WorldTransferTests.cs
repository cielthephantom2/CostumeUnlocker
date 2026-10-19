using System.Text.Json;
using LastEpochLAN.Core;

internal static class WorldTransferTests
{
    internal static void Run(Action<bool,string> check)
    {
        var source = new WorldState { Scene = "large-instance", Epoch = 1, Revision = 1,
            Enemies = Enumerable.Range(1, 2400).Select(id => new EnemyState { Id = id, Template = "skeleton", Health = 100, MaxHealth = 100 }).ToArray() };
        var writer = new WorldChunkWriter(); var reader = new WorldChunkReader();
        var chunks = writer.Encode(source);
        check(source.IsValid() && chunks.All(c => c.Enemies.Length <= 64), "2400 live enemies use bounded chunks instead of a 512-instance cutoff");
        check(chunks.Sum(c => c.Templates.Length) == 1 && chunks.SelectMany(c => c.Enemies).All(e => e.State.Template == null), "2400 skeletons share one type definition while retaining distinct enemy IDs");
        string typedJson = JsonSerializer.Serialize(chunks[2].Enemies[0]);
        check(!typedJson.Contains("\"Template\"") && typedJson.Contains("\"Type\":1"), "typed wire record omits the redundant template string field rather than transmitting an empty placeholder");
        WorldState? assembled = null;
        foreach (var chunk in chunks)
        {
            var wire = JsonSerializer.Deserialize<WorldChunk>(JsonSerializer.Serialize(chunk))!;
            assembled = reader.Accept(wire);
            if (chunk.Index < chunk.Count - 1) check(assembled == null, "partial world remains invisible to gameplay " + chunk.Index);
        }
        check(assembled != null && assembled.Enemies.Length == 2400 && assembled.Enemies.All(e => e.Template == "skeleton") && WorldContent.Same(source, assembled), "chunk assembly restores all identities and native template names atomically");
        var next = writer.Encode(source with { Revision = 2 });
        check(next.All(c => c.Templates.Length == 0), "unchanged enemy types are cached across world revisions");
        foreach (var chunk in next) assembled = reader.Accept(chunk);
        check(assembled?.Revision == 2, "cached type references decode in a subsequent snapshot");
        var reset = writer.Encode(source with { Epoch = 2, Revision = 1, Scene = "next-map" });
        check(reset.Sum(c => c.Templates.Length) == 1, "new instance rebuilds the per-connection type catalogue");
        foreach (var chunk in reset) assembled = reader.Accept(chunk);
        check(assembled?.Scene == "next-map", "new instance baseline replaces old world after complete assembly");
        var deltaWriter=new WorldChunkWriter();var deltaReader=new WorldChunkReader();
        var baseline=source;
        foreach(var chunk in deltaWriter.Encode(baseline,true)) assembled=deltaReader.Accept(chunk);
        var update=baseline with {Revision=2,Enemies=baseline.Enemies.Select(e=>e.Id==1 ? e with {Health=95} : e).ToArray()};
        var delta=deltaWriter.Encode(update,true);
        check(delta[0].BaseRevision==1 && delta.Sum(c=>c.Enemies.Length)==1,
            "one changed enemy in a 2400-monster instance sends one record");
        check(delta.Sum(c=>JsonSerializer.SerializeToUtf8Bytes(c).Length)<JsonSerializer.SerializeToUtf8Bytes(baseline).Length/10,
            "sparse world update materially reduces serialized payload instead of resending the full crowd");
        foreach(var chunk in delta) assembled=deltaReader.Accept(JsonSerializer.Deserialize<WorldChunk>(JsonSerializer.Serialize(chunk))!);
        check(assembled?.Enemies.Length==2400 && assembled.Enemies.Single(e=>e.Id==1).Health==95,
            "delta merge preserves unchanged monsters while applying new HP atomically");
        var replacement=update with {Revision=3,Enemies=update.Enemies.Where(e=>e.Id!=2).Append(new EnemyState {Id=2401,Template="new-native-type",MaxHealth=50,Health=50}).ToArray()};
        var replaced=deltaWriter.Encode(replacement,true);
        foreach(var chunk in replaced) assembled=deltaReader.Accept(chunk);
        check(replaced.SelectMany(c=>c.RemovedEnemies).SequenceEqual(new[]{2}) && assembled!.Enemies.All(e=>e.Id!=2) &&
            assembled.Enemies.Single(e=>e.Id==2401).Template=="new-native-type",
            "delta handles removal and a new native template together");
        var noChanges=deltaWriter.Encode(replacement with {Revision=4},true);
        foreach(var chunk in noChanges) assembled=deltaReader.Accept(chunk);
        check(noChanges.All(c=>c.Enemies.Length==0) && assembled!.Enemies.Length==2400,
            "metadata-only heartbeat preserves the committed world without retransmitting idle actors");
        bool noBaseline=false;try{new WorldChunkReader().Accept(delta[0]);}catch(InvalidDataException){noBaseline=true;}
        check(noBaseline,"delta cannot be applied by a late join without its authoritative baseline");
        bool wrongRevision=false;try{deltaReader.Accept(noChanges[0] with {Revision=5,BaseRevision=1});}catch(InvalidDataException){wrongRevision=true;}
        check(wrongRevision,"delta naming a stale base revision is rejected before gameplay state changes");
        var refreshWriter=new WorldChunkWriter();
        refreshWriter.Encode(source,true);
        WorldChunk[] refresh=Array.Empty<WorldChunk>();
        for(int rev=2;rev<=21;rev++) refresh=refreshWriter.Encode(source with {Revision=rev},true);
        check(refresh[0].BaseRevision==20 && refresh.Sum(c=>c.Enemies.Length)==0,
            "reliable connection keeps idle world deltas beyond 20 snapshots without periodic full-crowd burst");
        var fresh=deltaWriter.Encode(source with {Epoch=2,Scene="another-instance",Revision=1},true);
        check(fresh[0].BaseRevision==-1 && fresh.Sum(c=>c.Templates.Length)==1,
            "instance change discards delta baseline and rebuilds native type definitions");
        var order = new WorldChunkReader(); order.Accept(chunks[0]);
        bool rejected = false; try { order.Accept(chunks[2]); } catch (InvalidDataException) { rejected = true; }
        check(rejected, "missing or out-of-order chunks are rejected without exposing a partial map");
        var unknown = new WorldChunkReader(); unknown.Accept(chunks[0]); unknown.Accept(chunks[1]);
        rejected = false; try { unknown.Accept(chunks[2] with { Enemies = new[] { chunks[2].Enemies[0] with { Type = 999 } } }); } catch (InvalidDataException) { rejected = true; }
        check(rejected, "unknown enemy type reference is rejected");
        var history = new EnemyHistory();
        foreach (var enemy in source.Enemies) history.Observe(enemy);
        history.Observe(new EnemyState { Id = 2401, Template = "skeleton", MaxHealth = 100, Dead = true });
        check(history.Snapshot(source.Enemies.Select(e=>e.Id)).Length == 2401, "dead transition retains a separate slot even when more than 512 enemies remain alive");
        var ledger = new EnemyDeathLedger();
        var corpse = source with { Enemies = new[] { source.Enemies[0] with { Dead=true, Health=0 } } };
        var guests = new[] { new PlayerState { Id="a", Scene=source.Scene, Available=true }, new PlayerState { Id="b", Scene=source.Scene, Available=true } };
        ledger.Observe(corpse, guests);
        check(!ledger.CanRetire(1,1,new[]{"a","b"}) && ledger.Eligible("late",corpse).Length==0, "death ledger waits for present recipients and excludes retrospective late-join rewards");
        ledger.Record("a",new DeathReceipt(1,1,DeathReceiptOutcome.Completed));
        check(!ledger.CanRetire(1,1,new[]{"a","b"}), "one guest completion cannot retire another guest's pending reward");
        ledger.Record("b",new DeathReceipt(1,1,DeathReceiptOutcome.Failed));
        ledger.Record("b",new DeathReceipt(1,1,DeathReceiptOutcome.Completed));
        check(!ledger.CanRetire(1,1,new[]{"a","b"}) && ledger.Eligible("b",corpse).Length==0, "partial native failure is retained without duplicate automatic retries or forged outcome replacement");
        check(ledger.CanRetire(1,1,new[]{"a"}), "disconnected recipient does not retain a corpse indefinitely");
        check(ledger.RetireAcknowledged(1,new[]{"a"}).SetEquals(new[]{1}) && ledger.RetireAcknowledged(1,new[]{"a"}).Count == 0,
            "completed delivery details are removed rather than rescanned on every future capture");
        ledger.Observe(corpse,guests);
        check(ledger.Eligible("a",corpse).Length == 0 && ledger.Eligible("b",corpse).Length == 0,
            "compact retired identity cannot reopen a completed reward from an old corpse snapshot");
        ledger.Observe(corpse with {Epoch=2}, Array.Empty<PlayerState>());
        check(!ledger.Record("a",new DeathReceipt(1,1,DeathReceiptOutcome.Completed)) && ledger.CanRetire(2,1,Array.Empty<string>()), "instance change rejects stale receipts and releases deaths without eligible guests");
    }
}
