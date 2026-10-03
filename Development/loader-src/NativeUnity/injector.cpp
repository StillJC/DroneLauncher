#include "common.h"
#include <tlhelp32.h>
#include <psapi.h>
#pragma comment(lib,"psapi.lib")
// A newly created suspended process may not have an initialized PEB loader list.
// Find the already mapped ntdll image without assuming system DLL base addresses.
static uintptr_t mappedImage(HANDLE process,const wchar_t* name) {
 MEMORY_BASIC_INFORMATION region{};uintptr_t address=0;
 while(VirtualQueryEx(process,(void*)address,&region,sizeof(region))) {
  if(region.Type==MEM_IMAGE&&region.BaseAddress==region.AllocationBase){wchar_t path[32768];DWORD n=GetMappedFileNameW(process,region.AllocationBase,path,32768);if(n&&n<32768){path[n]=0;auto leaf=wcsrchr(path,L'\\');if(leaf&&!_wcsicmp(leaf+1,name))return (uintptr_t)region.AllocationBase;}}
  uintptr_t next=(uintptr_t)region.BaseAddress+region.RegionSize;if(next<=address)break;address=next;
 }return 0;
}
static uintptr_t module(DWORD pid,const wchar_t* name) {
 for(int retry=0;retry<20;retry++) { HANDLE snapshot=CreateToolhelp32Snapshot(TH32CS_SNAPMODULE,pid);if(snapshot==INVALID_HANDLE_VALUE){Sleep(20);continue;}
  MODULEENTRY32W e{};e.dwSize=sizeof(e);uintptr_t result=0;
  for(BOOL more=Module32FirstW(snapshot,&e);more;more=Module32NextW(snapshot,&e))if(!_wcsicmp(e.szModule,name)){result=(uintptr_t)e.modBaseAddr;break;}
  CloseHandle(snapshot);if(result)return result;Sleep(20);
 }return 0;
}
static bool call(HANDLE process,uintptr_t fn,void* arg,DWORD& result) {
 HANDLE t=CreateRemoteThread(process,nullptr,0,(LPTHREAD_START_ROUTINE)fn,arg,0,nullptr);if(!t)return false;
 bool ok=WaitForSingleObject(t,30000)==WAIT_OBJECT_0&&GetExitCodeThread(t,&result);CloseHandle(t);return ok;
}
int wmain(int argc,wchar_t** argv) {
 if(argc!=3)return 2;DWORD pid=wcstoul(argv[1],nullptr,10);std::wstring dll=argv[2];
 HANDLE p=OpenProcess(PROCESS_CREATE_THREAD|PROCESS_QUERY_INFORMATION|PROCESS_VM_OPERATION|PROCESS_VM_WRITE|PROCESS_VM_READ,FALSE,pid);if(!p)return 3;
 auto load=GetProcAddress(GetModuleHandleW(L"kernel32.dll"),"LoadLibraryW");HMODULE owner=nullptr;
 GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,(LPCWSTR)load,&owner);
 wchar_t ownerPath[MAX_PATH];GetModuleFileNameW(owner,ownerPath,MAX_PATH);const wchar_t* name=wcsrchr(ownerPath,L'\\');
 auto remoteOwner=module(pid,name?name+1:ownerPath);
 if(!remoteOwner) {
  auto ntdll=GetModuleHandleW(L"ntdll.dll");auto exitThread=GetProcAddress(ntdll,"RtlExitUserThread");auto remoteNtdll=mappedImage(p,L"ntdll.dll");DWORD initResult=0;
  if(!remoteNtdll||!exitThread||!call(p,remoteNtdll+(uintptr_t)exitThread-(uintptr_t)ntdll,nullptr,initResult)){CloseHandle(p);return 5;}
  remoteOwner=module(pid,name?name+1:ownerPath);
 }
 if(!remoteOwner){CloseHandle(p);return 6;}
 uintptr_t target=remoteOwner+(uintptr_t)load-(uintptr_t)owner;
 SIZE_T bytes=(dll.size()+1)*2;void* remote=VirtualAllocEx(p,nullptr,bytes,MEM_COMMIT|MEM_RESERVE,PAGE_READWRITE);DWORD result=0;bool okay=false;
 if(remote&&WriteProcessMemory(p,remote,dll.c_str(),bytes,nullptr)&&call(p,target,remote,result)){
  auto base=module(pid,L"DroneRacingGenesis.Unity.dll");HMODULE local=LoadLibraryExW(dll.c_str(),nullptr,DONT_RESOLVE_DLL_REFERENCES);
  if(base&&local){auto initialize=GetProcAddress(local,"Initialize");if(initialize)okay=call(p,base+(uintptr_t)initialize-(uintptr_t)local,nullptr,result)&&result==1;}
  if(local)FreeLibrary(local);
 }
 // If a remote call timed out, the creating Shell owns and terminates its still-suspended child.
 if(remote&&okay)VirtualFreeEx(p,remote,0,MEM_RELEASE);CloseHandle(p);
 return okay?0:4;
}
