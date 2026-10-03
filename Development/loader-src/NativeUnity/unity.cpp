#include "common.h"
#include <algorithm>
#include <cmath>
#include <cstdarg>
#include <cstring>
#include <atomic>
static BYTE* game;
static std::wstring root,logRoot;
static HANDLE logfile=INVALID_HANDLE_VALUE,mapping;
static const BYTE* shared;
static SRWLOCK logLock=SRWLOCK_INIT;
static SRWLOCK pathLock=SRWLOCK_INIT;
static std::atomic<unsigned long> samples{},fallbacks{},failures{},redirects{};
static std::atomic<bool> installed{},mapped{};
static bool inputEnabled;




static void log(const char* format,...) { char b[2048];int n=sprintf_s(b,"%llu ",GetTickCount64());va_list a;va_start(a,format);vsnprintf_s(b+n,sizeof(b)-n,_TRUNCATE,format,a);va_end(a);strcat_s(b,"\r\n");DWORD done;AcquireSRWLockExclusive(&logLock);WriteFile(logfile,b,(DWORD)strlen(b),&done,nullptr);ReleaseSRWLockExclusive(&logLock); }
using Call=uintptr_t(*)(void*,void*,void*,void*);
struct Site { DWORD rva;BYTE bytes[5];bool second;const wchar_t* from;const wchar_t* relative;Call original;void* replacement;uint32_t handle;unsigned count; };
static Site sites[]={
 {4193432,{0xe8,0x93,0x02,0x3e,0x00},false,L"C:/Sega/ShellData/ShellData.ini",L"ShellData\\ShellData.ini"},
 {4193450,{0xe8,0x71,0x0b,0,0},true,L"C:/Sega/ShellData/ShellData.ini",L"ShellData\\ShellData.ini"},
 {4193603,{0xe8,0xe8,0x01,0x3e,0},false,L"C:/Sega/ShellData/GameSettings.ini",L"ShellData\\GameSettings.ini"},
 {4193621,{0xe8,0xc6,0x0a,0,0},true,L"C:/Sega/ShellData/GameSettings.ini",L"ShellData\\GameSettings.ini"},
 {11853595,{0xe8,0x50,0x0a,0xee,0xff},false,L"C:/Sega/GameData//",L"GameData\\"},
 {11853029,{0xe8,0x86,0x0c,0xee,0xff},false,L"C:/Sega/GameData//",L"GameData\\"},
 {4105841,{0xe8,0xfa,0x42,0x64,0},false,L"C:/Sega/GameData//",L"GameData\\"},
 {4105761,{0xe8,0x4a,0x43,0x64,0},false,L"C:/Sega/GameData//",L"GameData\\"}
};
static const wchar_t* (*chars)(void*);static int (*length)(void*);static void* (*newString)(const wchar_t*,int);static uint32_t (*retain)(void*,bool);
template<int N> static uintptr_t redirected(void* a,void* b,void* c,void* d) {
 auto& s=sites[N];void*& value=s.second?b:a;
 if(value&&length(value)==(int)wcslen(s.from)&&!wmemcmp(chars(value),s.from,wcslen(s.from))) {
  AcquireSRWLockExclusive(&pathLock);
  if(!s.replacement){auto path=root+L"\\"+s.relative;s.replacement=newString(path.c_str(),(int)path.size());if(s.replacement)s.handle=retain(s.replacement,false);}
  if(s.replacement){value=s.replacement;++redirects;if(!s.count++)log("path site=%d redirected",N);}else{++failures;log("ERROR managed string allocation site=%d",N);}
  ReleaseSRWLockExclusive(&pathLock);
 }
 return s.original(a,b,c,d);
}
struct Pad { uint32_t now,on,off;float h,v,rt,throttle; };
static_assert(sizeof(Pad)==28);
static Pad nativeSample{},previous{0,0,0,0,0,0,1};static bool saved;
static void (*updateOriginal)(void*,void*);
static Pad* current(){auto klass=*(BYTE**)(game+0x14bfaa8);auto stat=*(BYTE**)(klass+0xb8);return (Pad*)(stat+0x60);}
static bool snapshot(BYTE* out) {
 if(!shared)return false;
 for(int i=0;i<20;i++){LONG sequence=*(const volatile LONG*)shared;if(sequence&1)continue;MemoryBarrier();memcpy(out,shared,64);MemoryBarrier();if(sequence==*(const volatile LONG*)shared){
  DWORD tick;memcpy(&tick,out+4,4);uint16_t version;memcpy(&version,out+30,2);if(version!=1||(DWORD)(GetTickCount()-tick)>2000)return false;
  float v[8];memcpy(v,out+32,32);for(float x:v)if(!std::isfinite(x))return false;return true;
 }}return false;
}
static void beforeInput(){__try{if(saved)*current()=nativeSample;}__except(EXCEPTION_EXECUTE_HANDLER){saved=false;++failures;}}
static void afterInput() {
 __try {
  Pad* p=current();nativeSample=*p;saved=true;BYTE data[64];
  if(!snapshot(data)){if(mapped.exchange(false)){++fallbacks;log("input fallback: unavailable/stale/incoherent mapping");}previous={0,0,0,0,0,0,1};return;}
  float v[8];memcpy(v,data+32,32);const BYTE* report=data+8;uint32_t now=0;
  if(report[13]&128)now|=1;if(report[14]&1)now|=2;
  if(v[1]<-.5f)now|=4;if(v[1]>.5f)now|=8;if(v[0]<-.5f)now|=16;if(v[0]>.5f)now|=32;
  if(v[5]>.5f)now|=0x40;if(v[6]>.5f)now|=0x200;if(v[4]>.5f)now|=0x400;
  Pad next{now,now&~previous.now,previous.now&~now,v[0],v[1],v[3],1-2*std::clamp(v[2],0.f,1.f)};
  p[1]=previous;*p=next;
  if(next.on||next.off||next.h!=previous.h||next.v!=previous.v)log("input now=%x on=%x off=%x h=%.3f v=%.3f throttle=%.3f",next.now,next.on,next.off,next.h,next.v,next.throttle);
  previous=next;mapped=true;++samples;
 }__except(EXCEPTION_EXECUTE_HANDLER){++failures;mapped=false;log("ERROR input adapter exception; native fallback");}
}
static void update(void* array,void* method) { beforeInput();updateOriginal(array,method);afterInput(); }
static void jump(BYTE* destination,void* fn) { BYTE code[]={0xff,0x25,0,0,0,0};memcpy(destination,code,6);memcpy(destination+6,&fn,8); }
static BYTE* allocateNear(BYTE* target) {
 SYSTEM_INFO info;GetSystemInfo(&info);uintptr_t aligned=(uintptr_t)target&~((uintptr_t)info.dwAllocationGranularity-1);
 for(uintptr_t distance=info.dwAllocationGranularity;distance<0x70000000;distance+=info.dwAllocationGranularity){
  for(int sign:{-1,1}){uintptr_t address=aligned+sign*distance;auto p=(BYTE*)VirtualAlloc((void*)address,4096,MEM_COMMIT|MEM_RESERVE,PAGE_READWRITE);if(p)return p;}
 }return nullptr;
}
static bool patch(BYTE* target,const BYTE* data,SIZE_T count) { DWORD old,ignored;if(!VirtualProtect(target,count,PAGE_EXECUTE_READWRITE,&old))return false;memcpy(target,data,count);FlushInstructionCache(GetCurrentProcess(),target,count);VirtualProtect(target,count,old,&ignored);return true; }
static DWORD WINAPI statusThread(void*) {
 auto path=logRoot+L"\\unity-"+std::to_wstring(GetCurrentProcessId())+L".json";
 for(;;){std::string s="{\"GamePid\":"+std::to_string(GetCurrentProcessId())+",\"Tick\":"+std::to_string(GetTickCount64())+",\"Active\":"+(installed?"true":"false")+",\"InputEnabled\":"+(inputEnabled?"true":"false")+",\"Mapped\":"+(mapped?"true":"false")+",\"Samples\":"+std::to_string(samples)+",\"Fallbacks\":"+std::to_string(fallbacks)+",\"Failures\":"+std::to_string(failures)+",\"Redirects\":"+std::to_string(redirects)+"}";writeAtomic(path,s);Sleep(1000);} }
extern "C" __declspec(dllexport) DWORD WINAPI Initialize(void*) {
 root=env(L"DRG_CONTENT_ROOT");if(root.empty())return 0;logRoot=root+L"\\Launcher\\Logs\\Loader";
 auto file=logRoot+L"\\unity-"+std::to_wstring(GetCurrentProcessId())+L".log";
 logfile=CreateFileW(file.c_str(),GENERIC_WRITE,FILE_SHARE_READ,nullptr,CREATE_ALWAYS,FILE_ATTRIBUTE_NORMAL,nullptr);if(logfile==INVALID_HANDLE_VALUE)return 0;
 auto path=root+L"\\DroneRacing\\GameAssembly.dll";
 if(hashFile(path)!="491f7c3e3ab392f78aea8b3b9775b65aad63acde2ae4e2fc4d04fdfe716fb652"){log("ERROR unsupported GameAssembly SHA256; no hooks installed");return 0;}
 log("GameAssembly SHA256 verified; initializing before original game main thread resumes");
 game=(BYTE*)LoadLibraryExW(path.c_str(),nullptr,LOAD_WITH_ALTERED_SEARCH_PATH);if(!game){log("ERROR GameAssembly load %lu",GetLastError());return 0;}
 const BYTE signature[]={0x40,0x57,0x48,0x81,0xec,0xf0,0,0,0,0x80,0x3d,0x34};
 for(auto& s:sites)if(memcmp(game+s.rva,s.bytes,5)){log("ERROR call-site signature RVA=%lx",s.rva);return 0;}
 if(memcmp(game+0xdc09e0,signature,sizeof(signature))){log("ERROR input signature");return 0;}
 chars=(decltype(chars))GetProcAddress((HMODULE)game,"il2cpp_string_chars");length=(decltype(length))GetProcAddress((HMODULE)game,"il2cpp_string_length");newString=(decltype(newString))GetProcAddress((HMODULE)game,"il2cpp_string_new_utf16");retain=(decltype(retain))GetProcAddress((HMODULE)game,"il2cpp_gchandle_new");
 if(!chars||!length||!newString||!retain){log("ERROR required IL2CPP exports missing");return 0;}
 inputEnabled=env(L"DRG_UNITY_INPUT")==L"1";mapping=OpenFileMappingW(FILE_MAP_READ,FALSE,env(L"DRG_IO_MAPPING").c_str());if(mapping)shared=(const BYTE*)MapViewOfFile(mapping,FILE_MAP_READ,0,0,64);
 if(shared&&*(const uint16_t*)(shared+30)!=1){log("ERROR unsupported mapping version; no hooks installed");return 0;}
 BYTE* stubs=allocateNear(game);if(!stubs){log("ERROR no nearby trampoline allocation");return 0;}
 Call replacements[]={redirected<0>,redirected<1>,redirected<2>,redirected<3>,redirected<4>,redirected<5>,redirected<6>,redirected<7>};
 for(int i=0;i<8;i++){auto& s=sites[i];int32_t delta;memcpy(&delta,s.bytes+1,4);s.original=(Call)(game+s.rva+5+delta);jump(stubs+i*16,(void*)replacements[i]);}
 jump(stubs+128,(void*)update);memcpy(stubs+160,game+0xdc09e0,9);jump(stubs+169,game+0xdc09e9);updateOriginal=(decltype(updateOriginal))(stubs+160);

 DWORD old;if(!VirtualProtect(stubs,4096,PAGE_EXECUTE_READ,&old)){VirtualFree(stubs,0,MEM_RELEASE);return 0;}FlushInstructionCache(GetCurrentProcess(),stubs,4096);
 int changed=0;for(int i=0;i<8;i++){BYTE callBytes[5]={0xe8};int32_t delta=(int32_t)((stubs+i*16)-(game+sites[i].rva+5));memcpy(callBytes+1,&delta,4);if(!patch(game+sites[i].rva,callBytes,5))break;changed++;}
 BYTE entry[9]={0xe9,0,0,0,0,0x90,0x90,0x90,0x90};int32_t delta=(int32_t)((stubs+128)-(game+0xdc09e5));memcpy(entry+1,&delta,4);
 if(changed!=8||(inputEnabled&&!patch(game+0xdc09e0,entry,9))){for(int i=0;i<changed;i++)patch(game+sites[i].rva,sites[i].bytes,5);log("ERROR installation rolled back");return 0;}
 installed=true;log("ACTIVE eight narrow paths; input=%d; no network array writes",inputEnabled);HANDLE thread=CreateThread(nullptr,0,statusThread,nullptr,0,nullptr);if(thread)CloseHandle(thread);return 1;
}
BOOL WINAPI DllMain(HINSTANCE instance,DWORD why,void*) { if(why==DLL_PROCESS_ATTACH)DisableThreadLibraryCalls(instance);return TRUE; }
