#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <cstdio>
#include <string>

// x86 helper keeps remote LoadLibrary/Initialize pointer sizes equal to Shell.
static DWORD remoteCall(HANDLE process, LPTHREAD_START_ROUTINE address, void* argument) {
    HANDLE thread = CreateRemoteThread(process, nullptr, 0, address, argument, 0, nullptr);
    if (!thread) return 0;
    DWORD result = 0;
    if (WaitForSingleObject(thread, 15000) == WAIT_OBJECT_0) GetExitCodeThread(thread, &result);
    CloseHandle(thread); return result;
}
int wmain(int argc, wchar_t** argv) {
    if (argc != 4) { fwprintf(stderr, L"Expected Shell.exe, IO DLL, checksum argument\n"); return 2; }
    std::wstring shell = argv[1], dll = argv[2], command = L"\"" + shell + L"\" " + argv[3];
    std::wstring cwd = shell.substr(0, shell.find_last_of(L"\\/"));
    STARTUPINFOW si{}; si.cb = sizeof(si); PROCESS_INFORMATION pi{};
    if (!CreateProcessW(shell.c_str(), command.data(), nullptr, nullptr, FALSE, CREATE_SUSPENDED, nullptr, cwd.c_str(), &si, &pi)) {
        fwprintf(stderr, L"CreateProcess failed: %lu\n", GetLastError()); return 3;
    }
    bool okay = false;
    SIZE_T bytes = (dll.size() + 1) * sizeof(wchar_t);
    void* remote = VirtualAllocEx(pi.hProcess, nullptr, bytes, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    if (remote && WriteProcessMemory(pi.hProcess, remote, dll.c_str(), bytes, nullptr)) {
        auto load = reinterpret_cast<LPTHREAD_START_ROUTINE>(GetProcAddress(GetModuleHandleW(L"kernel32.dll"), "LoadLibraryW"));
        DWORD module = remoteCall(pi.hProcess, load, remote);
        HMODULE local = LoadLibraryExW(dll.c_str(), nullptr, DONT_RESOLVE_DLL_REFERENCES);
        if (module && local) {
            auto init = GetProcAddress(local, "_Initialize@4");
            if (init) okay = remoteCall(pi.hProcess, reinterpret_cast<LPTHREAD_START_ROUTINE>(module + (reinterpret_cast<BYTE*>(init) - reinterpret_cast<BYTE*>(local))), nullptr) == 1;
        }
        if (local) FreeLibrary(local);
    }
    if (remote) VirtualFreeEx(pi.hProcess, remote, 0, MEM_RELEASE);
    if (!okay || ResumeThread(pi.hThread) == (DWORD)-1) {
        fwprintf(stderr, L"IO module initialization failed; terminating this suspended Shell (%lu). Error=%lu\n", pi.dwProcessId, GetLastError());
        TerminateProcess(pi.hProcess, 4); WaitForSingleObject(pi.hProcess, 5000);
        CloseHandle(pi.hThread); CloseHandle(pi.hProcess); return 4;
    }
    printf("%lu\n", pi.dwProcessId); fflush(stdout);
    CloseHandle(pi.hThread); CloseHandle(pi.hProcess); return 0;
}
