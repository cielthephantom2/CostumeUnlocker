using System.Text.Json;
using UnityEngine;

namespace LastEpochLAN.Mod;

internal enum ShortcutAction { Telescope,Mastery,MasteryPoints,ChampionsGate,PersonalStash }
internal sealed class ShortcutBinding
{
    public KeyCode Key {get;set;}
    public bool Ctrl {get;set;}
    public bool Alt {get;set;}
    public bool Shift {get;set;}
    public override string ToString()=>Key==KeyCode.None?"Unbound":(Ctrl?"Ctrl+":"")+(Alt?"Alt+":"")+(Shift?"Shift+":"")+Key;
    internal bool Same(ShortcutBinding other)=>other!=null && Key==other.Key && Ctrl==other.Ctrl && Alt==other.Alt && Shift==other.Shift;
}
internal static class UiShortcutRules
{
    internal static readonly ShortcutAction[] Actions=Enum.GetValues<ShortcutAction>();
    internal static bool ValidKey(KeyCode key)=>Enum.IsDefined(typeof(KeyCode),key) && (int)key>=32 && (int)key<300 &&
        key is not (KeyCode.F1 or KeyCode.F2 or KeyCode.F4 or KeyCode.Delete);
    internal static Dictionary<ShortcutAction,ShortcutBinding> Read(string json)
    {
        if(json.Length>16384)throw new InvalidDataException("Shortcut settings exceed 16 KiB");
        var input=JsonSerializer.Deserialize<Dictionary<ShortcutAction,ShortcutBinding>>(json)??new();
        var result=new Dictionary<ShortcutAction,ShortcutBinding>();
        foreach(var action in Actions)
            if(input.TryGetValue(action,out var bind) && bind!=null && ValidKey(bind.Key) && !result.Values.Any(b=>b.Same(bind)))result[action]=bind;
        return result;
    }
    internal static bool Assign(Dictionary<ShortcutAction,ShortcutBinding> bindings,ShortcutAction action,ShortcutBinding binding)
    {
        if(!Actions.Contains(action) || !ValidKey(binding.Key) || bindings.Any(p=>p.Key!=action && p.Value.Same(binding)))return false;
        bindings[action]=binding;return true;
    }
}
