#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <setupapi.h>
#include <hidsdi.h>
#include <shellapi.h>
#include <set>
#pragma comment(lib,"shell32.lib")
#include <cstdio>
#include <cstdarg>
#include <cstring>
#include <cstddef>
#include "../NativeUnity/common.h"

// Only Shell's IAT is changed. No Shell state or original file is written.
static BYTE* shell;
static HANDLE logFile = INVALID_HANDLE_VALUE, mapping;
static const BYTE* shared;
static SRWLOCK logLock = SRWLOCK_INIT;
static int deviceToken, setToken, preparsedToken;
static HANDLE board = &deviceToken;
static HDEVINFO deviceSet = &setToken;
static const char devicePath[] = "DRG-Software-Cabinet-IO";
static volatile LONG reads, writes;
static BYTE pendingReply, pendingValue;
static DWORD lastHeartbeat;
static BYTE lastSnapshot[64];
static bool haveSnapshot;
static std::wstring contentRoot;
static decltype(&CreateProcessA) realProcess;
static bool childInitializationFailed;
// Optional, lossy observer. Shell never waits for an output consumer.
struct OutputEvent { volatile LONG sequence; DWORD tick; DWORD word; DWORD reserved; };
struct OutputRing { DWORD version; DWORD pid; volatile LONG sequence; volatile LONG tick; DWORD reserved[4]; OutputEvent events[256]; };
static_assert(sizeof(OutputRing)==4128);
static OutputRing* outputs;
static HANDLE outputMapping;
static SRWLOCK outputLock=SRWLOCK_INIT;
static void log(const char* format, ...) {
    char line[2048]; int used = sprintf_s(line, "%llu ", GetTickCount64());
    va_list args; va_start(args, format);
    vsnprintf_s(line + used, sizeof(line) - used, _TRUNCATE, format, args); va_end(args);
    strcat_s(line, "\r\n"); DWORD done;
    AcquireSRWLockExclusive(&logLock);
    WriteFile(logFile, line, (DWORD)strlen(line), &done, nullptr);
    ReleaseSRWLockExclusive(&logLock);
}
template<class T> static T original(DWORD rva) { return *reinterpret_cast<T*>(shell + rva); }
static decltype(&SetupDiGetClassDevsA) realClass;
static decltype(&SetupDiEnumDeviceInterfaces) realEnum;
static decltype(&SetupDiGetDeviceInterfaceDetailA) realDetail;
static decltype(&SetupDiDestroyDeviceInfoList) realDestroy;
static decltype(&CreateFileA) realCreate;
static decltype(&ReadFile) realRead;
static decltype(&WriteFile) realWrite;
static decltype(&CloseHandle) realClose;
static decltype(&HidD_GetAttributes) realAttributes;
static decltype(&HidD_GetPreparsedData) realPreparsed;
static decltype(&HidP_GetCaps) realCaps;
static GUID hidGuid;

static HDEVINFO WINAPI getClass(const GUID* guid, PCSTR enumerator, HWND hwnd, DWORD flags) {
    if (guid && IsEqualGUID(*guid, hidGuid) && !enumerator && (flags & DIGCF_DEVICEINTERFACE)) {
        log("enumeration software board VID=1043 PID=0002 usage=0001:0005"); return deviceSet;
    }
    return realClass(guid, enumerator, hwnd, flags);
}
static BOOL WINAPI enumInterfaces(HDEVINFO set, PSP_DEVINFO_DATA dev, const GUID* guid, DWORD index, PSP_DEVICE_INTERFACE_DATA out) {
    if (set != deviceSet) return realEnum(set, dev, guid, index, out);
    if (index) { SetLastError(ERROR_NO_MORE_ITEMS); return FALSE; }
    if (!out || out->cbSize != sizeof(*out)) { SetLastError(ERROR_INVALID_PARAMETER); return FALSE; }
    out->InterfaceClassGuid = hidGuid; out->Flags = SPINT_ACTIVE; out->Reserved = 0; return TRUE;
}
static BOOL WINAPI getDetail(HDEVINFO set, PSP_DEVICE_INTERFACE_DATA data, PSP_DEVICE_INTERFACE_DETAIL_DATA_A detail, DWORD size, PDWORD required, PSP_DEVINFO_DATA info) {
    if (set != deviceSet) return realDetail(set, data, detail, size, required, info);
    DWORD needed = (DWORD)(offsetof(SP_DEVICE_INTERFACE_DETAIL_DATA_A, DevicePath) + sizeof(devicePath));
    if (required) *required = needed;
    if (!detail || size < needed) { SetLastError(ERROR_INSUFFICIENT_BUFFER); return FALSE; }
    memcpy(detail->DevicePath, devicePath, sizeof(devicePath)); return TRUE;
}
static BOOL WINAPI destroySet(HDEVINFO set) { return set == deviceSet ? TRUE : realDestroy(set); }
static HANDLE WINAPI createFile(LPCSTR name, DWORD access, DWORD share, LPSECURITY_ATTRIBUTES sa, DWORD disposition, DWORD flags, HANDLE templateFile) {
    if (name && !strcmp(name, devicePath)) { log("board opened"); pendingReply = 0; return board; }
    return realCreate(name, access, share, sa, disposition, flags, templateFile);
}
static BOOL WINAPI closeHandle(HANDLE handle) {
    if (handle == board) { log("board closed"); return TRUE; } return realClose(handle);
}
static BOOLEAN __stdcall attributes(HANDLE handle, PHIDD_ATTRIBUTES out) {
    if (handle != board) return realAttributes(handle, out);
    out->Size = sizeof(*out); out->VendorID = 0x1043; out->ProductID = 2; out->VersionNumber = 0x0100; return TRUE;
}
static BOOLEAN __stdcall preparsed(HANDLE handle, PHIDP_PREPARSED_DATA* out) {
    if (handle != board) return realPreparsed(handle, out);
    *out = reinterpret_cast<PHIDP_PREPARSED_DATA>(&preparsedToken); return TRUE;
}
static NTSTATUS __stdcall caps(PHIDP_PREPARSED_DATA data, PHIDP_CAPS out) {
    if (data != reinterpret_cast<PHIDP_PREPARSED_DATA>(&preparsedToken)) return realCaps(data, out);
    memset(out, 0, sizeof(*out)); out->UsagePage = 1; out->Usage = 5;
    out->InputReportByteLength = 22; out->OutputReportByteLength = 3; return HIDP_STATUS_SUCCESS;
}
static BOOL WINAPI readFile(HANDLE handle, LPVOID buffer, DWORD size, LPDWORD done, LPOVERLAPPED overlap) {
    if (handle != board) return realRead(handle, buffer, size, done, overlap);
    if (done) *done = 0;
    if (size != 22 || overlap || !shared) { SetLastError(ERROR_INVALID_PARAMETER); return FALSE; }
    BYTE snapshot[64]; bool copied = false;
    for (int attempt = 0; attempt < 10; ++attempt) {
        auto seq = *reinterpret_cast<const volatile LONG*>(shared);
        if (seq & 1) continue;
        MemoryBarrier(); memcpy(snapshot, shared, sizeof(snapshot)); MemoryBarrier();
        if (seq == *reinterpret_cast<const volatile LONG*>(shared)) { copied = true; break; }
    }
    // A preempted writer is not a disconnected board. Retain the last coherent
    // sample, but its original timestamp still expires after two seconds.
    if (copied) { memcpy(lastSnapshot, snapshot, sizeof(snapshot)); haveSnapshot = true; }
    else if (haveSnapshot) memcpy(snapshot, lastSnapshot, sizeof(snapshot));
    else { SetLastError(ERROR_NOT_READY); return FALSE; }
    DWORD tick; memcpy(&tick, snapshot + 4, 4);
    if ((DWORD)(GetTickCount() - tick) > 2000) {
        log("input broker heartbeat expired; returning device disconnected");
        SetLastError(ERROR_DEVICE_NOT_CONNECTED); return FALSE;
    }
    memcpy(buffer, snapshot + 8, 22);
    BYTE* report = static_cast<BYTE*>(buffer);
    report[18] = 0x10; // Synthetic board firmware 1.0, not a claim about original hardware.
    report[19] = pendingReply ? pendingReply : 0xb4;
    report[20] = pendingReply ? pendingValue : 0; report[21] = 0;
    if (pendingReply) { log("handshake reply=%02X value=%02X", pendingReply, pendingValue); pendingReply = 0; }
    if (done) *done = 22; InterlockedIncrement(&reads); return TRUE;
}
// Output sink boundary: later backends can dispatch vibration/RGB here.
static void output(BYTE value, BYTE command) {
    log("output accepted value=%02X command=%02X", value, command);
    if(!outputs||!TryAcquireSRWLockExclusive(&outputLock))return;
    LONG next=outputs->sequence+1;auto& event=outputs->events[((DWORD)next-1)%256];
    InterlockedExchange(&event.sequence,0);event.tick=GetTickCount();event.word=value|((DWORD)command<<8);
    MemoryBarrier();InterlockedExchange(&event.sequence,next);InterlockedExchange(&outputs->sequence,next);
    ReleaseSRWLockExclusive(&outputLock);
}
static BOOL WINAPI writeFile(HANDLE handle, LPCVOID buffer, DWORD size, LPDWORD done, LPOVERLAPPED overlap) {
    if (handle != board) return realWrite(handle, buffer, size, done, overlap);
    if (done) *done = 0;
    if (size != 3 || overlap) { SetLastError(ERROR_INVALID_PARAMETER); return FALSE; }
    auto report = static_cast<const BYTE*>(buffer); InterlockedIncrement(&writes);
    output(report[1], report[2]);
    switch (report[2]) {
    case 0xe0: pendingReply = 0xb0; pendingValue = 0; break;
    case 0xe1: pendingReply = 0xb1; pendingValue = 0; break;
    case 0xf0: pendingReply = 0xb2; pendingValue = 0; break;
    case 0xf1: pendingReply = 0xb3; pendingValue = 0; break;
    }
    if (done) *done = size; return TRUE;
}
static bool patch(DWORD rva, void* replacement) {
    DWORD before, ignored; if (!VirtualProtect(shell + rva, sizeof(void*), PAGE_READWRITE, &before)) return false;
    *reinterpret_cast<void**>(shell + rva) = replacement;
    VirtualProtect(shell + rva, sizeof(void*), before, &ignored); return true;
}
// Only Shell's own CreateProcess import and its exact DroneRacing child are adapted.
static std::wstring quoteArgument(const std::wstring& value) {
    std::wstring result=L"\"";size_t slashes=0;
    for(wchar_t c:value){if(c==L'\\'){slashes++;continue;}if(c==L'\"')result.append(slashes*2+1,L'\\');else result.append(slashes,L'\\');slashes=0;result+=c;}
    result.append(slashes*2,L'\\');return result+L"\"";
}
static std::vector<std::wstring> arguments(const std::wstring& line) {
    int count=0;auto parsed=CommandLineToArgvW((L"drg "+line).c_str(),&count);std::vector<std::wstring> result;
    if(parsed){for(int i=1;i<count;i++)result.push_back(parsed[i]);LocalFree(parsed);}return result;
}
static std::string playerCommand(LPCSTR app,LPCSTR command) {
    auto extra=env(L"DRG_PLAYER_ARGUMENTS");if(extra.empty())return command?command:"";
    int size=MultiByteToWideChar(CP_ACP,0,command?command:"",-1,nullptr,0);std::wstring original(size,L'\0');MultiByteToWideChar(CP_ACP,0,command?command:"",-1,original.data(),size);original.resize(size-1);
    auto before=arguments(original),after=arguments(extra);std::set<std::wstring> replace;
    const std::set<std::wstring> allowed={L"-screen-width",L"-screen-height",L"-screen-fullscreen",L"-window-mode",L"-screen-quality",L"-monitor"};
    if(after.size()%2){log("ERROR optional player arguments malformed; retaining original command");return command?command:"";}
    for(size_t i=0;i<after.size();i+=2){if(!allowed.count(after[i])||!replace.insert(after[i]).second){log("ERROR optional player argument invalid; retaining original command");return command?command:"";}}
    std::wstring merged=quoteArgument(contentRoot+L"\\DroneRacing\\DroneRacing.exe");
    for(size_t i=0;i<before.size();i++){if(replace.count(before[i])){if(i+1<before.size())i++;continue;}if(i==0&&!_wcsicmp(before[i].c_str(),(contentRoot+L"\\DroneRacing\\DroneRacing.exe").c_str()))continue;merged+=L" "+quoteArgument(before[i]);}
    for(auto& arg:after)merged+=L" "+quoteArgument(arg);
    size=WideCharToMultiByte(CP_ACP,0,merged.c_str(),-1,nullptr,0,nullptr,nullptr);std::string result(size,'\0');WideCharToMultiByte(CP_ACP,0,merged.c_str(),-1,result.data(),size,nullptr,nullptr);result.resize(size-1);return result;
}
static BOOL WINAPI createProcess(LPCSTR app,LPSTR command,LPSECURITY_ATTRIBUTES psa,LPSECURITY_ATTRIBUTES tsa,BOOL inherit,DWORD flags,LPVOID environment,LPCSTR cwd,LPSTARTUPINFOA startup,LPPROCESS_INFORMATION child) {
    std::wstring expected=contentRoot+L"\\DroneRacing\\DroneRacing.exe";
    char actual[MAX_PATH]={};if(app)GetFullPathNameA(app,MAX_PATH,actual,nullptr);
    wchar_t wide[MAX_PATH]={};MultiByteToWideChar(CP_ACP,0,actual,-1,wide,MAX_PATH);
    if(!app||_wcsicmp(wide,expected.c_str()))return realProcess(app,command,psa,tsa,inherit,flags,environment,cwd,startup,child);
    if(childInitializationFailed){SetLastError(ERROR_DLL_INIT_FAILED);return FALSE;}
    auto effective=playerCommand(app,command);if(!env(L"DRG_PLAYER_ARGUMENTS").empty())command=effective.data();
    if(!realProcess(app,command,psa,tsa,inherit,flags|CREATE_SUSPENDED,environment,cwd,startup,child))return FALSE;
    log("child suspended pid=%lu app=%s command=%s cwd=%s originalFlags=%lu",child->dwProcessId,app,command?command:"",cwd?cwd:"",flags);
    auto bootstrap=contentRoot+L"\\Launcher\\Plugins\\Unity\\DroneRacingGenesis.UnityBootstrap.exe";
    auto dll=contentRoot+L"\\Launcher\\Plugins\\Unity\\DroneRacingGenesis.Unity.dll";
    auto args=L"\""+bootstrap+L"\" "+std::to_wstring(child->dwProcessId)+L" \""+dll+L"\"";
    STARTUPINFOW si{};si.cb=sizeof(si);PROCESS_INFORMATION pi{};DWORD code=99;bool okay=false;
    if(CreateProcessW(bootstrap.c_str(),args.data(),nullptr,nullptr,FALSE,CREATE_NO_WINDOW,nullptr,nullptr,&si,&pi)) {
        DWORD wait=WaitForSingleObject(pi.hProcess,65000);
        if(wait==WAIT_OBJECT_0&&GetExitCodeProcess(pi.hProcess,&code))okay=code==0;
        else {TerminateProcess(pi.hProcess,99);WaitForSingleObject(pi.hProcess,5000);}
        CloseHandle(pi.hThread);CloseHandle(pi.hProcess);
    }
    if(!okay) {
        childInitializationFailed=true;
        log("ERROR native Unity initialization failed pid=%lu code=%lu; original child never resumed",child->dwProcessId,code);
        writeAtomic(contentRoot+L"\\Launcher\\Logs\\Loader\\native-launch-error.json","{\"ShellPid\":"+std::to_string(GetCurrentProcessId())+",\"GamePid\":"+std::to_string(child->dwProcessId)+",\"Error\":\"Native Unity initialization failed; inspect unity PID log. No unsupported hooks were installed.\"}");
        TerminateProcess(child->hProcess,4);WaitForSingleObject(child->hProcess,5000);CloseHandle(child->hThread);CloseHandle(child->hProcess);ZeroMemory(child,sizeof(*child));SetLastError(ERROR_DLL_INIT_FAILED);return FALSE;
    }
    log("child compatibility ready pid=%lu; original arguments/cwd/environment retained",child->dwProcessId);
    if(!(flags&CREATE_SUSPENDED))ResumeThread(child->hThread);
    return TRUE;
}
static DWORD WINAPI observe(void*) {
    int lastState = -1, lastProtocol = -1;
    for (;;) {
        if(outputs)InterlockedExchange(&outputs->tick,(LONG)GetTickCount());
        int state = *reinterpret_cast<volatile int*>(shell + 0x1fa08c);
        int protocol = *reinterpret_cast<volatile int*>(shell + 0x11a5ea4);
        if (state != lastState || protocol != lastProtocol) {
            log("state=%d protocol=%d ioReady=%d protocolReady=%d reads=%ld writes=%ld", state, protocol,
                *reinterpret_cast<volatile int*>(shell + 0x11a5e8c), *reinterpret_cast<volatile int*>(shell + 0x11a5e90), reads, writes);
            lastState = state; lastProtocol = protocol;
        }
        if(GetTickCount()-lastHeartbeat>1000)writeAtomic(contentRoot+L"\\Launcher\\Logs\\Loader\\shell-lifecycle.json","{\"ShellPid\":"+std::to_string(GetCurrentProcessId())+",\"State\":"+std::to_string(state)+",\"Tick\":"+std::to_string(GetTickCount64())+",\"IOReady\":"+std::to_string(*reinterpret_cast<volatile int*>(shell+0x11a5e8c))+"}");
        if (GetTickCount() - lastHeartbeat > 1000) { lastHeartbeat = GetTickCount(); log("heartbeat state=%d reads=%ld writes=%ld", state, reads, writes); }
        Sleep(5);
    }
}
extern "C" __declspec(dllexport) DWORD WINAPI Initialize(void*) {
    shell = reinterpret_cast<BYTE*>(GetModuleHandleW(nullptr));
    auto dos = reinterpret_cast<IMAGE_DOS_HEADER*>(shell);
    auto nt = reinterpret_cast<IMAGE_NT_HEADERS*>(shell + dos->e_lfanew);
    if (nt->FileHeader.Machine != IMAGE_FILE_MACHINE_I386 ||
        memcmp(shell + 0x456ba, "\xc7\x05\x8c\x5e\x5a\x01\x01\x00\x00\x00", 10)) return 0;
    wchar_t name[256], root[32768], path[32768];
    if (!GetEnvironmentVariableW(L"DRG_IO_MAPPING", name, 256) || !GetEnvironmentVariableW(L"DRG_CONTENT_ROOT", root, 32768)) return 0;
    contentRoot=root;
    mapping = OpenFileMappingW(FILE_MAP_READ, FALSE, name);
    if (!mapping) return 0;
    shared = static_cast<const BYTE*>(MapViewOfFile(mapping, FILE_MAP_READ, 0, 0, 64));
    if (!shared) return 0;
    swprintf_s(path, L"%s\\Launcher\\Logs\\Loader\\io-%lu.log", root, GetCurrentProcessId());
    logFile = CreateFileW(path, GENERIC_WRITE, FILE_SHARE_READ, nullptr, CREATE_NEW, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (logFile == INVALID_HANDLE_VALUE) return 0;
    auto outputName=env(L"DRG_OUTPUT_MAPPING");
    if(!outputName.empty()) {
        outputMapping=OpenFileMappingW(FILE_MAP_WRITE,FALSE,outputName.c_str());
        if(outputMapping)outputs=(OutputRing*)MapViewOfFile(outputMapping,FILE_MAP_WRITE,0,0,sizeof(OutputRing));
        if(outputs&&outputs->version!=1){UnmapViewOfFile(outputs);outputs=nullptr;}
        if(outputs){outputs->pid=GetCurrentProcessId();outputs->tick=(LONG)GetTickCount();}
        log("optional output observer connected=%d",outputs!=nullptr);
    }
    HidD_GetHidGuid(&hidGuid);
#define INSTALL(slot, saved, replacement) saved = original<decltype(saved)>(slot); if (!patch(slot, reinterpret_cast<void*>(replacement))) return 0
    INSTALL(0x9128c, realClass, getClass); INSTALL(0x91298, realEnum, enumInterfaces);
    INSTALL(0x91294, realDetail, getDetail); INSTALL(0x91290, realDestroy, destroySet);
    INSTALL(0x91148, realCreate, createFile); INSTALL(0x91140, realClose, closeHandle);
    INSTALL(0x91070, realAttributes, attributes); INSTALL(0x91074, realPreparsed, preparsed);
    INSTALL(0x91068, realCaps, caps); INSTALL(0x911b8, realRead, readFile); INSTALL(0x911b4, realWrite, writeFile);
    if(env(L"DRG_NATIVE_UNITY")==L"1") { INSTALL(0x91188,realProcess,createProcess); }
    log("IO compatibility initialized; original ready/state instructions unchanged; MkII only");
    HANDLE thread = CreateThread(nullptr, 0, observe, nullptr, 0, nullptr);
    if (!thread) return 0; CloseHandle(thread); return 1;
}
BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) DisableThreadLibraryCalls(instance); return TRUE;
}
