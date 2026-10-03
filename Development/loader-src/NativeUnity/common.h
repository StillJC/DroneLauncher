#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <bcrypt.h>
#include <string>
#include <vector>
#include <cstdio>
#pragma comment(lib,"bcrypt.lib")
inline std::wstring env(const wchar_t* key) { wchar_t v[32768]; DWORD n=GetEnvironmentVariableW(key,v,32768); return n&&n<32768?v:L""; }
inline std::string utf8(const std::wstring& s) { int n=WideCharToMultiByte(CP_UTF8,0,s.data(),(int)s.size(),nullptr,0,nullptr,nullptr);std::string r(n,0);WideCharToMultiByte(CP_UTF8,0,s.data(),(int)s.size(),r.data(),n,nullptr,nullptr);return r; }
inline std::string jsonString(const std::string& s) { std::string r="\"";for(unsigned char c:s){if(c=='\\'||c=='\"'){r+='\\';r+=c;}else if(c<32){char b[7];sprintf_s(b,"\\u%04x",c);r+=b;}else r+=c;}return r+'\"'; }
inline bool writeAtomic(const std::wstring& path,const std::string& s) { auto temp=path+L".tmp";HANDLE f=CreateFileW(temp.c_str(),GENERIC_WRITE,FILE_SHARE_READ,nullptr,CREATE_ALWAYS,FILE_ATTRIBUTE_NORMAL,nullptr);if(f==INVALID_HANDLE_VALUE)return false;DWORD done;bool ok=WriteFile(f,s.data(),(DWORD)s.size(),&done,nullptr)&&done==s.size();CloseHandle(f);return ok&&MoveFileExW(temp.c_str(),path.c_str(),MOVEFILE_REPLACE_EXISTING); }
inline std::string hashFile(const std::wstring& path) {
 HANDLE file=CreateFileW(path.c_str(),GENERIC_READ,FILE_SHARE_READ,nullptr,OPEN_EXISTING,0,nullptr);if(file==INVALID_HANDLE_VALUE)return "";
 BCRYPT_ALG_HANDLE alg=nullptr;BCRYPT_HASH_HANDLE hash=nullptr;DWORD size=0,used=0;std::string result;
 if(BCryptOpenAlgorithmProvider(&alg,BCRYPT_SHA256_ALGORITHM,nullptr,0)>=0&&BCryptGetProperty(alg,BCRYPT_OBJECT_LENGTH,(PUCHAR)&size,4,&used,0)>=0){
  std::vector<BYTE> object(size),buffer(1024*1024);BYTE digest[32];
  if(BCryptCreateHash(alg,&hash,object.data(),size,nullptr,0,0)>=0){DWORD count;bool okay=true;
   for(;;){if(!ReadFile(file,buffer.data(),(DWORD)buffer.size(),&count,nullptr)){okay=false;break;}if(!count)break;if(BCryptHashData(hash,buffer.data(),count,0)<0){okay=false;break;}}
   if(okay&&BCryptFinishHash(hash,digest,32,0)>=0){char b[3];for(BYTE c:digest){sprintf_s(b,"%02x",c);result+=b;}}
   BCryptDestroyHash(hash);
  }
 }
 if(alg)BCryptCloseAlgorithmProvider(alg,0);CloseHandle(file);return result;
}
