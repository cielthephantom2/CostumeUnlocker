using Mono.Cecil;
using System.Text.Json;
using System.Text.RegularExpressions;
var repo=Path.GetFullPath(args[0]);var game=args[1];
var resolver=new DefaultAssemblyResolver();foreach(var dir in new[]{Path.Combine(game,"MelonLoader","net6"),Path.Combine(game,"MelonLoader","Il2CppAssemblies"),Path.Combine(repo,"src","LastEpochLAN.Mod","bin","Release","net6.0"),Path.Combine(repo,"src","LastEpochLAN.Core","bin","Release","net6.0"),Path.GetDirectoryName(typeof(object).Assembly.Location)!})resolver.AddSearchDirectory(dir);
using var mod=AssemblyDefinition.ReadAssembly(Path.Combine(repo,"src","LastEpochLAN.Mod","bin","Release","net6.0","LastEpochLAN.dll"),new ReaderParameters{AssemblyResolver=resolver,ReadSymbols=true});
using var core=AssemblyDefinition.ReadAssembly(Path.Combine(repo,"src","LastEpochLAN.Core","bin","Release","net6.0","LastEpochLAN.Core.dll"),new ReaderParameters{AssemblyResolver=resolver,ReadSymbols=true});
IEnumerable<TypeDefinition> Walk(IEnumerable<TypeDefinition> ts)=>ts.SelectMany(t=>new[]{t}.Concat(Walk(t.NestedTypes)));
var types=Walk(mod.MainModule.Types).Concat(Walk(core.MainModule.Types)).ToArray();
var calls=types.SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().Select(m=>m.GetElementMethod().FullName).ToHashSet();
string Source(TypeDefinition t)=>t.Methods.SelectMany(m=>m.DebugInformation.SequencePoints).Select(s=>s.Document.Url).FirstOrDefault()??(t.DeclaringType==null?"":Source(t.DeclaringType));
string Relative(string p)=>p.Length==0?"":Path.GetRelativePath(repo,p).Replace('\\','/');
bool Patch(TypeDefinition t)=>t.CustomAttributes.Any(a=>a.AttributeType.FullName=="HarmonyLib.HarmonyPatch");
bool PatchOwner(TypeDefinition t)=>Patch(t)||(t.DeclaringType!=null&&PatchOwner(t.DeclaringType));
bool Allowed(TypeDefinition t)=>LastEpochLAN.Mod.RoomFeaturePolicy.AllowsPatch(t.DeclaringType?.Namespace??t.Namespace,t.Name,t.DeclaringType?.Name??"");
var installed=types.Where(t=>Patch(t)&&Allowed(t)).ToArray();var blocked=types.Where(t=>Patch(t)&&!Allowed(t)).ToArray();
var fileRows=Directory.EnumerateFiles(Path.Combine(repo,"src"),"*.cs",SearchOption.AllDirectories).Where(f=>!f.Contains(Path.DirectorySeparatorChar+"obj"+Path.DirectorySeparatorChar)&&!f.Contains(Path.DirectorySeparatorChar+"bin"+Path.DirectorySeparatorChar)).Select(file=>{
 var ts=types.Where(t=>t.Methods.SelectMany(m=>m.DebugInformation.SequencePoints).Any(s=>string.Equals(s.Document.Url,file,StringComparison.OrdinalIgnoreCase))).ToArray();
 bool qa=Path.GetFileName(file).Contains("Probe")||Path.GetFileName(file).Contains("Smoke")||Path.GetFileName(file)=="NativeQaNaturalBoot.cs";
 return new{file=Relative(file),classification=qa?"QA/diagnostics; inspect CLI/call gates":ts.Any(t=>Patch(t)&&Allowed(t))?"contains installed release hooks":ts.Any(t=>Patch(t)&&!Allowed(t))?"contains policy-disabled hooks":"support/entrypoint/rules; direct-call references are conservative",types=ts.Select(t=>t.FullName).ToArray(),installedHooks=ts.Where(t=>Patch(t)&&Allowed(t)).Select(t=>t.FullName).ToArray(),disabledHooks=ts.Where(t=>Patch(t)&&!Allowed(t)).Select(t=>t.FullName).ToArray()};
}).ToArray();
var candidates=types.Where(t=>!t.Name.Contains('<')&&!PatchOwner(t)&&!t.Name.Contains("Settings")&&!t.Name.Contains("Config")).SelectMany(t=>t.Methods.Where(m=>m.HasBody&&!m.IsConstructor&&!m.IsSpecialName&&!m.IsVirtual&&!calls.Contains(m.FullName)&&!m.Name.StartsWith("On")&&!m.IsPublic).Select(m=>new{method=m.FullName,source=Relative(Source(t)),note="No direct IL caller; reflection/delegates/QA may still use this. Review candidate, not proof of dead code."})).ToArray();
var result=new{sourceFiles=fileRows.Length,compiledTypes=types.Length,installedHooks=installed.Length,disabledHooks=blocked.Length,sharedWorldEnabled=LastEpochLAN.Mod.RoomFeaturePolicy.SharedWorldEnabled,files=fileRows,installed=installed.Select(t=>t.FullName),disabled=blocked.Select(t=>t.FullName),reviewCandidates=candidates,scope="All source files and compiled release metadata; conservative direct-call analysis, not whole-program proof of dead code"};
File.WriteAllText(Path.Combine(repo,"validation","dev55-mod-inventory.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine(JsonSerializer.Serialize(new{result.sourceFiles,result.compiledTypes,result.installedHooks,result.disabledHooks,reviewCandidates=candidates.Length}));

if(args.Length>2)
{
 using var before=AssemblyDefinition.ReadAssembly(args[2],new ReaderParameters{AssemblyResolver=resolver});
 var oldMethods=Walk(before.MainModule.Types).SelectMany(t=>t.Methods).Where(m=>m.HasBody).ToDictionary(m=>m.FullName);
 var newMethods=Walk(mod.MainModule.Types).SelectMany(t=>t.Methods).Where(m=>m.HasBody).ToDictionary(m=>m.FullName);
 string Normalize(string value)=>Regex.Replace(Regex.Replace(value,@"b__(\d+)_", "b__N_"),@"(DisplayClass|<>9__)(\d+)_","$1N_");
 string Fingerprint(MethodDefinition m)=>Normalize(string.Join("\n",m.Body.Instructions.Select(i=>i.OpCode.Name+" "+(i.Operand?.ToString()??"")))+"\n"+string.Join("|",m.Body.ExceptionHandlers.Select(h=>$"{h.HandlerType}:{h.CatchType}:{h.TryStart?.Offset}:{h.TryEnd?.Offset}:{h.HandlerStart?.Offset}:{h.HandlerEnd?.Offset}"))+"\n"+string.Join("|",m.Body.Variables.Select(v=>v.VariableType.FullName)));
 var generatedRemoved=oldMethods.Keys.Except(newMethods.Keys).Where(k=>(k.Contains("::<")||k.Contains("/<>"))).ToArray();
 var generatedAdded=newMethods.Keys.Except(oldMethods.Keys).Where(k=>(k.Contains("::<")||k.Contains("/<>"))).ToArray();
 var removed=oldMethods.Keys.Except(newMethods.Keys).Where(k=>!(k.Contains("::<")||k.Contains("/<>"))).OrderBy(k=>k).ToArray();
 var added=newMethods.Keys.Except(oldMethods.Keys).Where(k=>!(k.Contains("::<")||k.Contains("/<>"))).OrderBy(k=>k).ToArray();
 var changed=oldMethods.Keys.Intersect(newMethods.Keys).Where(k=>Fingerprint(oldMethods[k])!=Fingerprint(newMethods[k])).OrderBy(k=>k).ToArray();
 var proof=new{removed,added,changed,compilerGeneratedRenamedRemoved=generatedRemoved.Length,compilerGeneratedRenamedAdded=generatedAdded.Length,unchanged=oldMethods.Keys.Intersect(newMethods.Keys).Count()-changed.Length,scope="Normalized compiled method IL, local types and exception handlers; normalizes compiler-generated lambda/display-class ordinal renumbering and metadata token relocation. Not a native gameplay test."};
 File.WriteAllText(Path.Combine(repo,"validation","dev55-helper-cleanup-il-equivalence.json"),JsonSerializer.Serialize(proof,new JsonSerializerOptions{WriteIndented=true}));
 Console.WriteLine(JsonSerializer.Serialize(new{removed=removed.Length,added=added.Length,changed=changed.Length,proof.unchanged}));
 if(removed.Length!=7 || added.Length!=0 || changed.Length!=0)throw new InvalidOperationException("Cleanup changed more than the seven unused methods");
}
