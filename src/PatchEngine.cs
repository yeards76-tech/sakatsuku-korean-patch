using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace SakatsukuPatcher {
 [DataContract] public class ManifestV2 {
  [DataMember(Name="format")] public string Format;
  [DataMember(Name="version")] public string Version;
  [DataMember(Name="files")] public List<ItemV2> Files;
 }
 [DataContract] public class ItemV2 {
  [DataMember(Name="path")] public string Path;
  [DataMember(Name="target_size")] public int TargetSize;
  [DataMember(Name="target_sha256")] public string TargetHash;
  [DataMember(Name="variants")] public List<VariantV2> Variants;
 }
 [DataContract] public class VariantV2 {
  [DataMember(Name="size")] public int Size;
  [DataMember(Name="sha256")] public string Hash;
  [DataMember(Name="payload")] public string Payload;
 }
 public static class PatchEngine {
  class Pending { public string Dest, Stage, Backup, BeforeHash, TargetHash; }
  static string Hash(byte[] b) { using(var s=SHA256.Create()) return BitConverter.ToString(s.ComputeHash(b)).Replace("-","").ToLowerInvariant(); }
  static string HashFile(string p) { using(var s=SHA256.Create()) using(var f=File.OpenRead(p)) return BitConverter.ToString(s.ComputeHash(f)).Replace("-","").ToLowerInvariant(); }
  static string Safe(string root,string rel) {
   if (!Regex.IsMatch(rel,@"^[A-Za-z0-9_./-]+$") || rel.StartsWith("/") || rel.Contains("..")) throw new Exception("잘못된 패치 경로");
   string prefix=System.IO.Path.GetFullPath(root).TrimEnd('\\')+"\\";
   string p=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,rel.Replace('/','\\')));
   if(!p.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)) throw new Exception("패치 경로 범위 오류");
   return p;
  }
  public static void Run(string root,bool verify,Action<string,int> status) {
   if(Process.GetProcessesByName("rpcs3").Length>0) throw new Exception("RPCS3를 종료한 뒤 적용해주세요.");
   string data=null;
   foreach(string c in new[]{@"PS3_GAME\USRDIR\data",@"USRDIR\data","data",""}) {
    string p=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,c));if(File.Exists(System.IO.Path.Combine(p,"font.par"))) {data=p;break;}
   }
   if(data==null) throw new Exception("게임 데이터(font.par)를 찾을 수 없습니다.");
   var roots=new List<string>{data};
   var parent=new DirectoryInfo(data);
   while(parent!=null && !File.Exists(System.IO.Path.Combine(parent.FullName,"rpcs3.exe"))) parent=parent.Parent;
   if(parent!=null) foreach(string c in new[]{@"dev_hdd0\game\BLJM61064\USRDIR\data",@"dev_hdd0\game\BLJM61064\USRDIR\patch\data"}) {
    string p=System.IO.Path.GetFullPath(System.IO.Path.Combine(parent.FullName,c));if(File.Exists(System.IO.Path.Combine(p,"font.par")) && !roots.Contains(p)) roots.Add(p);
   }
   string stage=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"sakatsuku-v09-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(stage);var pending=new List<Pending>();var written=new List<Pending>();
   try {
    // Prefer the embedded payload so an old adjacent korean.patch cannot shadow v0.9.
    Stream patch=Assembly.GetExecutingAssembly().GetManifestResourceStream("korean.patch");
    if(patch==null) patch=File.OpenRead(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"korean.patch"));
    using(patch) using(var zip=new ZipArchive(patch,ZipArchiveMode.Read)) {
     ManifestV2 m;var entry=zip.GetEntry("manifest.json");if(entry==null)throw new Exception("매니페스트 누락");
     using(var s=entry.Open())m=(ManifestV2)new DataContractJsonSerializer(typeof(ManifestV2)).ReadObject(s);
     if(m.Format!="sakatsuku-xor-v2" || m.Version!="0.9" || m.Files==null)throw new Exception("패치 형식 오류");
     var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);int done=0;
     foreach(var item in m.Files) {
      if(!seen.Add(item.Path) || item.TargetSize<0)throw new Exception("중복 경로 또는 크기 오류");
      for(int r=0;r<roots.Count;r++) {
       string dest=Safe(roots[r],item.Path);
       if(!File.Exists(dest)) {if(r>0)continue;throw new Exception("파일 누락: "+item.Path);}
       byte[] original=File.ReadAllBytes(dest);string before=Hash(original);
       if(before==item.TargetHash && original.Length==item.TargetSize)continue;
       VariantV2 variant=null;foreach(var v in item.Variants)if(v.Hash==before && v.Size==original.Length){variant=v;break;}
       if(variant==null)throw new Exception("지원하지 않는 파일 버전: "+item.Path+"\n원본 또는 이전 공식 배포본을 사용해주세요.");
       var pe=zip.GetEntry(variant.Payload);if(pe==null)throw new Exception("패치 데이터 누락");byte[] delta;
       using(var s=pe.Open())using(var mem=new MemoryStream()){s.CopyTo(mem);delta=mem.ToArray();}
       if(delta.Length!=Math.Max(original.Length,item.TargetSize))throw new Exception("패치 데이터 크기 오류");
       for(int i=0;i<original.Length;i++)delta[i]^=original[i];
       var result=new byte[item.TargetSize];Array.Copy(delta,result,result.Length);
       if(Hash(result)!=item.TargetHash)throw new Exception("패치 결과 검증 실패: "+item.Path);
       string prepared=Safe(stage,r+"/"+item.Path);Directory.CreateDirectory(System.IO.Path.GetDirectoryName(prepared));File.WriteAllBytes(prepared,result);
       pending.Add(new Pending{Dest=dest,Stage=prepared,BeforeHash=before,TargetHash=item.TargetHash});
      }
      status("0.9 검증: "+item.Path,10+(++done*50/m.Files.Count));
     }
    }
    if(verify){status("0.9 검증 완료: "+pending.Count+"개 적용 가능",100);return;}
    if(pending.Count==0){status("이미 0.9이 적용되어 있습니다.",100);return;}
    var backups=new Dictionary<string,string>();
    foreach(var p in pending){
     string owner=null;foreach(string r in roots)if(p.Dest.StartsWith(r+"\\",StringComparison.OrdinalIgnoreCase)){owner=r;break;}
     if(!backups.ContainsKey(owner)){string dir=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(owner),"KoreanPatch_Backup_v0.9_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);backups[owner]=dir;}
     p.Backup=Safe(backups[owner],p.Dest.Substring(owner.Length+1).Replace('\\','/'));Directory.CreateDirectory(System.IO.Path.GetDirectoryName(p.Backup));
     if(HashFile(p.Dest)!=p.BeforeHash)throw new Exception("검증 중 게임 파일이 변경되었습니다.");
     File.Copy(p.Dest,p.Backup,false);if(HashFile(p.Backup)!=p.BeforeHash)throw new Exception("백업 검증 실패");
    }
    try {
     int n=0;foreach(var p in pending){if(HashFile(p.Dest)!=p.BeforeHash)throw new Exception("게임 파일이 변경되었습니다.");written.Add(p);File.Copy(p.Stage,p.Dest,true);if(HashFile(p.Dest)!=p.TargetHash)throw new Exception("저장 후 검증 실패");status("0.9 적용 중",65+(++n*35/pending.Count));}
    }catch{foreach(var p in written){File.Copy(p.Backup,p.Dest,true);if(HashFile(p.Dest)!=p.BeforeHash)throw new Exception("복구 실패. 백업 경로: "+p.Backup);}throw;}
    status("0.9 대사·이미지 적용 완료 ("+pending.Count+"개)",100);
   } finally {if(Directory.Exists(stage))Directory.Delete(stage,true);}
  }
 }
}
