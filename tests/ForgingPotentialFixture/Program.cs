using System.Reflection;
using System.Text.Json;
using Il2Cpp;
using LastEpochLAN.Mod.OfflineTools;
var checks=new List<string>();
void Check(bool good,string label){if(!good)throw new Exception(label);checks.Add(label);}
var charge=typeof(OfflineForgingPotentialCostPatch).GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static);
var display=typeof(OfflineForgingPotentialDisplayPatch).GetMethod("Postfix",BindingFlags.NonPublic|BindingFlags.Static);
foreach(var (min,max,a,b) in new[]{(1,25,1,12),(1,20,1,10),(4,8,2,4),(1,1,1,1),(0,0,0,0),(0,15,0,7),(25,25,12,12)}){
 object[] costs={min,max};charge.Invoke(null,costs);
 Check((int)costs[0]==a && (int)costs[1]==b,$"native charge input {min}–{max} becomes {a}–{b}");
 var ui=new CraftingSlotManager{minPotentialCost=min,maxPotentialCost=max};ui.main.Item=new();
 display.Invoke(null,new object[]{ui});
 Check(ui.minPotentialCost==a && ui.maxPotentialCost==b,$"display agrees with charged range for {min}–{max}");
 if(max>0)Check(ui.minForgingPotentialText.text=="-"+a && ui.maxForgingPotentialText.text=="-"+b,"signed Forge labels show reduced cost");
}
var weaving=new CraftingSlotManager{minPotentialCost=4,maxPotentialCost=8};weaving.main.Item=new();weaving.modifier.Item=new(){itemType=102,subType=8};
display.Invoke(null,new object[]{weaving});Check(weaving.minPotentialCost==4 && weaving.maxPotentialCost==8,"Weaving Will/Touch UI cost remains native");
OfflineToolkit.Enabled=false;object[] unchanged={1,25};charge.Invoke(null,unchanged);
var online=new CraftingSlotManager{minPotentialCost=1,maxPotentialCost=25};online.main.Item=new();display.Invoke(null,new object[]{online});
Check((int)unchanged[1]==25 && online.maxPotentialCost==25,"online charge and display remain native");OfflineToolkit.Enabled=true;
for(int n=0;n<=255;n++)Check(ForgingPotentialRules.Half(n)==(n==0?0:Math.Max(1,n/2)),"all byte-sized FP costs preserve zero and floor positive half: "+n);
Directory.CreateDirectory("validation");File.WriteAllText("validation/forging-half-fixture.json",JsonSerializer.Serialize(new{passed=checks.Count,checks},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("PASS "+checks.Count+" forging-potential checks");
