using Il2Cpp;
using Il2CppLE.Data;
using Il2CppLE.Services;
using Il2CppLE.Services.Login;
using Il2CppCysharp.Threading.Tasks;
using UnityEngine;
namespace ItemNodeNativeQA;

// External helper can test the exact release DLL without embedding test tools
// in that DLL. Natural engine boot, isolated paths supplied by GuestProfile.
public partial class Audit
{
 int releaseBootStage;
 UniTask.Awaiter releaseTransition;
 UniTask<IGameplayService>.Awaiter releaseService;
 UniTask<CharacterData>.Awaiter releaseCreation;
 IGameplayService releaseGameplay;
 bool releaseBootstrap;
 public static bool BlockPrivateRoom()=>false;
 void ReleaseBootstrapTick()
 {
  if(!releaseBootstrap || releaseBootStage==4)return;
  var state=Il2CppClientAppState.ClientStateManager.CurrentClientAppStateType;
  if(releaseBootStage==0)
  {
   if(state!=Il2CppClientAppState.ClientAppStateType.Login || Il2CppClientAppState.ClientStateManager._sceneManager?._isTransitioning!=false)return;
   string expected=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","GuestProfile"));
   if(Il2CppLE.Core.PlatformFileManager.ApplicationSavePath!=expected)throw new InvalidOperationException("Release QA profile is not isolated");
   releaseTransition=Il2CppClientAppState.ClientStateManager.ChangeState(Il2CppClientAppState.ClientAppStateType.CharacterSelect).GetAwaiter();releaseBootStage=1;
  }
  else if(releaseBootStage==1 && releaseTransition.IsCompleted)
  {releaseTransition.GetResult();releaseService=ServiceProvider.GameplayService.GetAwaiter();releaseBootStage=2;}
  else if(releaseBootStage==2 && releaseService.IsCompleted)
  {
   releaseGameplay=releaseService.GetResult();
   var arguments=new CharacterCreateFunctionArguments{CharacterName="LANQA"+Guid.NewGuid().ToString("N")[..6],ClassId=CharacterClassID.Mage,Cycle=Cycle.Legacy,IsHardcore=false,IsSoloChallenge=false,IsSoloCharacterChallenge=false};
   releaseCreation=releaseGameplay.CreateOfflinePlayerCharacter(ServiceProvider.UserService.CurrentUser,arguments,Il2CppSystem.Threading.CancellationToken.None).GetAwaiter();releaseBootStage=3;
  }
  else if(releaseBootStage==3 && releaseCreation.IsCompleted)
  {
   var character=releaseCreation.GetResult();
   if(character==null || !character.IsOffline)throw new InvalidOperationException("Release QA character not offline");
   Il2CppEHG.Multiplayer.GameplayEnvironment.IsOnlinePlay=false;
   releaseTransition=releaseGameplay.StartPlayWithCharacter(character,Il2CppSystem.Threading.CancellationToken.None).GetAwaiter();releaseBootStage=4;
  }
 }
}
