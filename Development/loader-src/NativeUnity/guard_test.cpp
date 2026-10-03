#include "common.h"
#include <cstring>
// Development-only host. Mutates only its own memory, never game files.
int wmain(int argc,wchar_t** argv) {
 if(argc!=4)return 2;std::wstring root=argv[1],dll=argv[2],mode=argv[3];
 SetEnvironmentVariableW(L"DRG_CONTENT_ROOT",root.c_str());SetEnvironmentVariableW(L"DRG_UNITY_INPUT",L"1");
 BYTE* game=nullptr;BYTE saved=0;HANDLE mapping=nullptr;void* view=nullptr;
 if(mode==L"signature"){
  game=(BYTE*)LoadLibraryExW((root+L"\\DroneRacing\\GameAssembly.dll").c_str(),nullptr,LOAD_WITH_ALTERED_SEARCH_PATH);if(!game)return 3;
  DWORD rva=4193432;
  DWORD protection;VirtualProtect(game+rva,1,PAGE_EXECUTE_READWRITE,&protection);saved=game[rva];game[rva]=0x90;DWORD ignored;VirtualProtect(game+rva,1,protection,&ignored);
 }
 if(mode==L"version"){
  auto name=L"Local\\DRG.GuardTest."+std::to_wstring(GetCurrentProcessId());mapping=CreateFileMappingW(INVALID_HANDLE_VALUE,nullptr,PAGE_READWRITE,0,64,name.c_str());view=MapViewOfFile(mapping,FILE_MAP_ALL_ACCESS,0,0,64);if(!view)return 4;
  *(uint16_t*)((BYTE*)view+30)=2;SetEnvironmentVariableW(L"DRG_IO_MAPPING",name.c_str());
 }
 auto module=LoadLibraryW(dll.c_str());if(!module)return 5;auto initialize=(DWORD(WINAPI*)(void*))GetProcAddress(module,"Initialize");if(!initialize)return 6;
 DWORD result=initialize(nullptr);bool untouched=true;
 if(mode!=L"hash"){
  if(!game)game=(BYTE*)GetModuleHandleW(L"GameAssembly.dll");if(!game)return 7;
  const DWORD sites[]={4193432,4193450,4193603,4193621,11853595,11853029,4105841,4105761};
  for(DWORD rva:sites)if(game[rva]!=(mode==L"signature"&&rva==4193432?0x90:0xe8))untouched=false;
  if(game[0xdc09e0]!=0x40)untouched=false;

 }
 if(view)UnmapViewOfFile(view);if(mapping)CloseHandle(mapping);
 wprintf(L"mode=%s rejected=%d untouched=%d pid=%lu\n",mode.c_str(),result==0,untouched,GetCurrentProcessId());return result==0&&untouched?0:8;
}
