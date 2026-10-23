using System.Reflection;
using System.Text.Json;
using Il2Cpp;
using LastEpochLAN.Mod.OfflineTools;
var checks=new List<string>();
void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
object Call(Type t,string method,params object[] values)=>t.GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,values);
object Begin(Actor actor){object[] values={new UsingAbility{Actor=actor},default(OfflineEchoEnemyAggroScopePatch.Scope)};Call(typeof(OfflineEchoEnemyAggroScopePatch),"Prefix",values);return values[1];}
void End(object previous,Exception error=null)=>Call(typeof(OfflineEchoEnemyAggroScopePatch),"Finalizer",previous,error);
var enemy=new Actor();
Check(EchoEnemyAggro.Applies(enemy),"hostile echo actor is eligible");
Check(!EchoEnemyAggro.Applies(PlayerFinder.Player),"player excluded");
var minion=new Actor();minion.Alignment.Hostile=false;Check(!EchoEnemyAggro.Applies(minion),"friendly minion/NPC excluded");
MonolithZoneManager.instance.gameObject.scene=new(77);
Check(EchoEnemyAggro.Applies(enemy),"manager in a different scene does not reject echo gameplay actors");
enemy.Sync.actorVisuals=null;Check(EchoEnemyAggro.Applies(enemy),"minimap/visual availability is not required");
enemy.stateController.aggroRangeModifier=.5f;
var previous=Begin(enemy);
Check(EchoEnemyAggro.Searching && enemy.stateController.aggroRangeModifier==3.5f,"native total range including original +50% expands x3 for caller/vertical checks");
object[] box={90f,-60f,4.5f};Call(typeof(OfflineEchoEnemyAggroBoxPatch),"Prefix",box);
Check((float)box[0]==30 && (float)box[1]==-20 && (float)box[2]==1.5f,"box coordinates normalize x3 and caller multiplier is undone to avoid x9");
var same=Begin(enemy);Check(enemy.stateController.aggroRangeModifier==3.5f,"reentrant acquisition of same enemy never becomes x9");End(same);Check(enemy.stateController.aggroRangeModifier==3.5f && EchoEnemyAggro.Searching,"nested same enemy preserves outer range lease");
var nested=Begin(minion);Check(!EchoEnemyAggro.Searching,"nested friendly acquisition masks enemy scope");End(nested);Check(EchoEnemyAggro.Searching,"friendly finalizer restores parent scope");
var failure=new Exception("native error");Check(ReferenceEquals(Call(typeof(OfflineEchoEnemyAggroScopePatch),"Finalizer",previous,failure),failure) && !EchoEnemyAggro.Searching && enemy.stateController.aggroRangeModifier==.5f,"finalizer restores modifier and propagates native exception");
for(int n=0;n<100;n++){var lease=Begin(enemy);End(lease);}Check(enemy.stateController.aggroRangeModifier==.5f,"100 acquisitions never permanently inflate range");
object[] native={90f,-60f,1.5f};Call(typeof(OfflineEchoEnemyAggroBoxPatch),"Prefix",native);Check((float)native[0]==90 && (float)native[2]==1.5f,"outside scoped acquisition all native box consumers remain unchanged");
MonolithZoneManager.instance=null;Check(!EchoEnemyAggro.Applies(enemy),"campaign/dungeon remains native");
MonolithZoneManager.instance=new();MonolithZoneManager.instance.gameObject.scene=new(1,false);Check(!EchoEnemyAggro.Applies(enemy),"unloaded stale echo manager excluded");
MonolithZoneManager.instance=new();enemy.gameObject.scene=new(2);Check(EchoEnemyAggro.Applies(enemy),"pooled gameplay actor in another scene still receives x3");enemy.gameObject.scene=new(1);
enemy.stateController.autoCombatDisabled=true;Check(EchoEnemyAggro.Applies(enemy) && enemy.stateController.autoCombatDisabled,"native autoCombatDisabled flag is not changed or used as an acquisition prefilter");enemy.stateController.autoCombatDisabled=false;
enemy.Dead=true;Check(!EchoEnemyAggro.Applies(enemy),"dead enemy excluded");enemy.Dead=false;
OfflineToolkit.Enabled=false;Check(!EchoEnemyAggro.Applies(enemy),"online unchanged");OfflineToolkit.Enabled=true;
foreach(float invalid in new[]{-1f,-2f,float.NaN,float.PositiveInfinity,float.MaxValue}){enemy.stateController.aggroRangeModifier=invalid;var lease=Begin(enemy);Check(!EchoEnemyAggro.Searching,"disabled/invalid range multiplier is not expanded: "+invalid);End(lease);}
Directory.CreateDirectory("validation");File.WriteAllText("validation/echo-aggro-x3-fixture.json",JsonSerializer.Serialize(new{passed=checks.Count,checks,nativePursuitVerified=false},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("PASS "+checks.Count+" x3 aggro checks");
