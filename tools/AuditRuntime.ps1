param([Parameter(Mandatory=$true)][string]$Root,[Parameter(Mandatory=$true)][string]$Output,[Parameter(Mandatory=$true)][string]$OriginalRoot)
$ErrorActionPreference='Stop'
Add-Type @'
using System;using System.IO;
public static class DrgCrc {
 public static uint[] Table(string shell){using(var r=new BinaryReader(File.OpenRead(shell))){r.BaseStream.Position=0x3c;int pe=r.ReadInt32();r.BaseStream.Position=pe+6;int sections=r.ReadUInt16();r.BaseStream.Position=pe+20;int optional=r.ReadUInt16();long start=pe+24+optional;for(int i=0;i<sections;i++){r.BaseStream.Position=start+40*i+8;uint virtualSize=r.ReadUInt32(),rva=r.ReadUInt32(),rawSize=r.ReadUInt32(),raw=r.ReadUInt32();if(0x168540>=rva&&0x168540<rva+Math.Max(virtualSize,rawSize)){r.BaseStream.Position=raw+0x168540-rva;var table=new uint[256];for(int j=0;j<256;j++)table[j]=r.ReadUInt32();return table;}}throw new Exception("CRC table outside PE sections");}}
 public static uint FileCrc(string path,uint[] table){uint value=0x13031968;var b=new byte[1024*1024];using(var f=File.OpenRead(path)){int n;while((n=f.Read(b,0,b.Length))>0)for(int i=0;i<n;i++)value=unchecked((value<<8)^table[((value>>24)^b[i])&255]);}return value;}
}
'@
$rootPath=(Resolve-Path -LiteralPath $Root).Path
$source=(Resolve-Path -LiteralPath $OriginalRoot).Path
$table=[DrgCrc]::Table((Join-Path $rootPath 'Shell\Shell.exe'))
$reader=[IO.BinaryReader]::new([IO.File]::OpenRead((Join-Path $rootPath 'DroneRacing\install.crc')))
$checks=@()
try{while($reader.BaseStream.Position -lt $reader.BaseStream.Length){$size=$reader.ReadInt32();$expected=$reader.ReadUInt32();$name=[Text.Encoding]::ASCII.GetString($reader.ReadBytes($size)).TrimStart('\','/');$path=Join-Path $rootPath ('DroneRacing\'+$name);$actual=[DrgCrc]::FileCrc($path,$table);$checks+=@{File=$name;Expected=('{0:X8}' -f $expected);Actual=('{0:X8}' -f $actual);Match=($actual-eq $expected)}}}finally{$reader.Dispose()}
$preserved=@()
foreach($relative in @('Shell\Shell.exe','Shell\dk2win32.dll','Shell\install.ini','Shell\install.crc','DroneRacing\DroneRacing.exe','DroneRacing\UnityPlayer.dll','DroneRacing\GameAssembly.dll','DroneRacing\install.ini','DroneRacing\install.crc','backup\DroneRacing\install.ini','backup\DroneRacing\install.crc')){$actual=(Get-FileHash -LiteralPath (Join-Path $rootPath $relative)).Hash;$original=(Get-FileHash -LiteralPath (Join-Path $source $relative)).Hash;$preserved+=@{File=$relative;Actual=$actual;Original=$original;Match=($actual-eq $original)}}
$report=@{Time=(Get-Date).ToString('o');Root=$rootPath;Checks=$checks;Preservation=$preserved;Count=$checks.Count;Passed=@($checks|Where-Object Match).Count;OriginalsPreserved=(@($preserved|Where-Object {-not $_.Match}).Count-eq 0)}
$report|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $Output -Encoding UTF8
[pscustomobject]$report|Select-Object Count,Passed,OriginalsPreserved,Root|ConvertTo-Json
if($report.Passed-ne 94-or -not $report.OriginalsPreserved){throw 'Integrity regression'}
