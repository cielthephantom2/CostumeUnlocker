using System.Reflection;
using System.Text.Json;
using Il2Cpp;
using Il2CppLE.AssetManagement;
using Il2CppLE.Networking;
using Il2CppLE.Services;
using Il2CppLE.Services.Cosmetics;
using Il2CppLE.Services.Models.Abilities;
using Il2CppLE.Services.Visuals.Abilities;
using LastEpochLAN.Mod.OfflineTools;
using StaticSync=Il2CppLE.Networking.Generated.Static.AbilitySyncService;
var checks=new List<string>();
void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);Console.WriteLine("PASS: "+text);}
void Call(Type t,string method,params object[] args)=>t.GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
var source=GlobalAssets.Cosmetics;
long[] childKeys={-485139073,-884264813,-1724283353,-1833006261,621478148};
var template=new CosmeticAbility{BackendID="MTX_Skills_RadiantMeteor",DisplayName="Holy Meteor",AbilityRef=new(MeteorDefaultCosmetic.MeteorKey),EquipmentAbility=true,EquipSlot=7,UniqueId=288,Class=1,StoreSpriteSoftRef=new object()};
foreach(long child in childKeys)template.AdditionalAbilityKeys.Add(new(){Ability=new(child),BackendId="Holy"});
source.AbilityCosmetics.Add(template);source.AbilityLookupPairs[template.BackendID]=template;source.CosmeticLookupPairs[template.BackendID]=template;
Ability.All[AbilityID.meteor]=new(){Key=MeteorDefaultCosmetic.MeteorKey,abilitySpriteSoftRef=new object()};
Ability.All[AbilityID.meteorShower]=new(){Key=123456};
PlayerFinder.Local=new();
var nativeVisual=new AbilityVisual();var existing=new AbilityVisualKey{Ability=new(MeteorDefaultCosmetic.MeteorKey),BackendId=template.BackendID};
GlobalAssets.AbilityVisuals.Entries.Add(new(){Key=existing,Visual=nativeVisual});
OfflineToolkit.Enabled=false;MeteorDefaultCosmetic.Ensure();Check(source.AbilityCosmetics.Count==1,"online startup never injects default Meteor");
OfflineToolkit.Enabled=true;OfflineToolkit.Settings.UnlockCosmetics=false;MeteorDefaultCosmetic.Ensure();Check(source.AbilityCosmetics.Count==1,"disabled cosmetic unlock never injects option");
OfflineToolkit.Settings.UnlockCosmetics=true;MeteorDefaultCosmetic.Ensure();
var added=source.AbilityLookupPairs[MeteorDefaultCosmetic.Id];
Check(added.DisplayName=="Default Meteor" && added.BackendID==MeteorDefaultCosmetic.Id,"native cosmetic registry contains a selectable Default Meteor");
Check(ReferenceEquals(source.CosmeticLookupPairs[MeteorDefaultCosmetic.Id],added),"generic lookup resolves the same native cosmetic for save/equip");
Check(added.AbilityRef.key==template.AbilityRef.key && added.EquipmentAbility==template.EquipmentAbility && added.EquipSlot==7 && added.UniqueId==288 && added.Class==1,"default option preserves native Meteor skill and equipment slot eligibility");
Check(ReferenceEquals(added.StoreSpriteSoftRef,Ability.All[AbilityID.meteor].abilitySpriteSoftRef),"default option uses the original Meteor icon");
Check(template.BackendID=="MTX_Skills_RadiantMeteor" && template.DisplayName=="Holy Meteor" && template.AdditionalAbilityKeys.All(k=>k.BackendId=="Holy"),"original cosmetic and child visual keys remain untouched");
Check(added.AdditionalAbilityKeys.Count==6 && added.AdditionalAbilityKeys.All(k=>k.BackendId==MeteorDefaultCosmetic.Id),"all five native children and Meteor Shower use independent default keys");
int count=GlobalAssets.AbilityVisuals.Entries.Count;
for(int i=0;i<10;i++)MeteorDefaultCosmetic.Ensure();
Check(source.AbilityCosmetics.Count==2 && count==GlobalAssets.AbilityVisuals.Entries.Count,"repeated UI/tick initialization never duplicates registry entries");
Check(ReferenceEquals(GlobalAssets.AbilityVisuals.cachedLookup[existing].Visual,nativeVisual),"existing MTX visual database entry remains unchanged");
foreach(var entry in GlobalAssets.AbilityVisuals.Entries.Where(e=>e.Key.BackendId==MeteorDefaultCosmetic.Id))
 Check(!entry.Visual.OverrideVisualObject && !entry.Visual.OverrideAlternateVisualObjects && !entry.Visual.OverrideCreateVfxOnDeath && !entry.Visual.OverrideTheme,"default cosmetic resolves successfully while retaining original shipped visual data");
var equipped=new Dictionary<long,AbilityVisualKey>{{MeteorDefaultCosmetic.MeteorKey,added.VisualKey}};
ServiceProvider.CosmeticsNetworkManager.CachedCharacterEquippedCosmeticSkills["local"]=equipped;
foreach(long key in childKeys.Append(MeteorDefaultCosmetic.MeteorKey).Append(123456))
 foreach(byte input in new byte[]{0,1,2,3,255})
 {
  byte variant=input;MeteorDefaultCosmetic.Variant(PlayerFinder.Local,new Ability{Key=key},ref variant);
  Check(variant==0,"selected default overrides item/lightning/cold variant "+input+" for Meteor family "+key);
 }
byte unchanged=2;MeteorDefaultCosmetic.Variant(new ActorSync(),Ability.All[AbilityID.meteor],ref unchanged);
Check(unchanged==2,"other actors retain their original visual variant");
MeteorDefaultCosmetic.Variant(PlayerFinder.Local,new Ability{Key=999},ref unchanged);Check(unchanged==2,"unrelated skill retains its original visual variant");
object[] indicator={PlayerFinder.Local,true,true};Call(typeof(OfflineMeteorDefaultIndicatorPatch),"Prefix",indicator);
Check(!(bool)indicator[1] && !(bool)indicator[2],"default Meteor indicator ignores lightning/cold visual conversions");
var message=new StaticSync.MessageCreateVisuals{ActorSyncKey=new StaticSync.ActorKey{Actor=PlayerFinder.Local},AbilityIndex=new StaticSync.AbilityIndex{Ability=Ability.All[AbilityID.meteor]},VisualsId=1};
object[] args2={message};Call(typeof(OfflineMeteorDefaultLightweightPatch),"Prefix",args2);
Check(((StaticSync.MessageCreateVisuals)args2[0]).VisualsId==0,"lightweight receiver selects stock visuals for item-proc Meteor");
object[] instanceArgs={PlayerFinder.Local,Ability.All[AbilityID.meteor],(byte)2};Call(typeof(OfflineMeteorDefaultVisualPatch),"Prefix",instanceArgs);
Check((byte)instanceArgs[2]==0,"individual receiver selects stock visuals for direct Meteor casts");
equipped[MeteorDefaultCosmetic.MeteorKey]=new(){Ability=new(MeteorDefaultCosmetic.MeteorKey),BackendId=template.BackendID};
MeteorDefaultCosmetic.Variant(PlayerFinder.Local,Ability.All[AbilityID.meteor],ref unchanged);Check(unchanged==2,"selecting another costume immediately restores its native variants");
equipped.Clear();MeteorDefaultCosmetic.Variant(PlayerFinder.Local,Ability.All[AbilityID.meteor],ref unchanged);Check(unchanged==2,"Remove Cosmetic restores native equipment conversion visuals");
equipped[MeteorDefaultCosmetic.MeteorKey]=added.VisualKey;
OfflineToolkit.Enabled=false;MeteorDefaultCosmetic.Variant(PlayerFinder.Local,Ability.All[AbilityID.meteor],ref unchanged);Check(unchanged==2,"online gameplay never forces visual variants");
OfflineToolkit.Enabled=true;OfflineToolkit.Settings.UnlockCosmetics=false;MeteorDefaultCosmetic.Variant(PlayerFinder.Local,Ability.All[AbilityID.meteor],ref unchanged);Check(unchanged==2,"turning cosmetic unlock off stops the presentation override");
OfflineToolkit.Settings.UnlockCosmetics=true;PlayerStore.LocalUserIdentity=null;Check(!MeteorDefaultCosmetic.Selected(PlayerFinder.Local),"missing local identity is safely ignored");
Console.WriteLine($"All {checks.Count} actual-source Meteor Default cosmetic checks passed; engine rendering not exercised.");
if(args.Length>0)File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=checks.Count,nativeGameplayVerified=false,checks},new JsonSerializerOptions{WriteIndented=true}));
