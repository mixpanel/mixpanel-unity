#include "platform_helpers.hpp"
#include <codecvt>
#include <fstream>
#include <iomanip>
#include <locale>
#include <sstream>
#include <string>
#include <utility>
#include <vector>

#if defined(__ANDROID__)
#   include <unistd.h>
#   include <sys/system_properties.h>
#elif defined(WIN32)
#    if WINAPI_FAMILY_DESKTOP_APP == WINAPI_FAMILY
#        include <shlwapi.h>
#        include <shlobj.h>
#        include <cassert>
#        include <iostream>
#        pragma comment(lib, "shlwapi.lib")
#        pragma comment(lib, "Rpcrt4.lib")
#        pragma comment(lib, "Version.lib")
#    else
#        include <Objbase.h>
#        include <wrl.h>
#        include <windows.storage.h>
#    endif
#endif

namespace mixpanel
{
    namespace detail
    {
        // convert UTF-8 string to wstring
        std::wstring PlatformHelpers::utf8_to_wstring(const std::string& str)
        {
            std::wstring_convert<std::codecvt_utf8<wchar_t>> myconv;
            return myconv.from_bytes(str);
        }

        // convert wstring to UTF-8 string
        std::string PlatformHelpers::wstring_to_utf8(const std::wstring& str)
        {
            std::wstring_convert<std::codecvt_utf8<wchar_t>> myconv;
            return myconv.to_bytes(str);
        }

        #ifdef __ANDROID__
        std::string PlatformHelpers::get_uuid()
        {
            std::ifstream ifs("/proc/sys/kernel/random/uuid");
            std::string ret;
            if(ifs >> ret) return ret;
            else return "failed-to-get-uuid";
        }

        //! get a path to a directory where we can write to. note: token might be ignored
        std::string PlatformHelpers::get_storage_directory(const std::string& token)
        {
            char buf[200]; // 64bit int can be 20 digits at most
            sprintf(buf,"/proc/%i/cmdline", (int)getpid());
            FILE* f = fopen(buf, "rb");
            if (!f)
            {
                return ".";
            }
            else
            {
                fread(buf, 1, sizeof(buf), f);
                fclose(f);

                if(buf[0] != '.')
                    return buf;
                else // this happens if we're executing the unix binary directly on the device
                    return ".";
            }
        }

        std::string PlatformHelpers::get_os_name()
        {
            return "Android";
        }

        static std::string get_system_property(const std::string& name)
        {
            char prop[PROP_VALUE_MAX + 1];
            int proplen = __system_property_get(name.c_str(), prop);
            if(proplen) return prop;
            else return "";
        }

        std::string PlatformHelpers::get_device_model()
        {
            std::string ret = get_system_property("ro.product.manufacturer") + " " + get_system_property("ro.product.model");
            return ret.size() > 1 ? ret : "Android Device";
        }

        bool PlatformHelpers::is_ios()     { return false; }
        bool PlatformHelpers::is_osx()     { return false; }
        bool PlatformHelpers::is_android() { return true; }
        bool PlatformHelpers::is_windows() { return false; }
        bool PlatformHelpers::is_desktop() { return false; }
        bool PlatformHelpers::is_mobile()  { return true; }

        static std::pair<int, int> get_screen_size()
        {
            return {};
        }

        Value PlatformHelpers::collect_automatic_properties()
        {
            Value ret;

            ret["$brand"] = get_system_property("ro.product.brand");
            ret["$manufacturer"] = get_system_property("ro.product.manufacturer");
            ret["$model"] = get_system_property("ro.product.model");
            ret["$os"] = "Android";
            ret["$os_version"] = get_system_property("ro.build.version.sdk");

            // Missing $carrier
            // Missing $has_nfc
            // Missing $has_telephone
            // Missing $google_play_services

            auto screen_size = get_screen_size();
            if (screen_size.first)
            {
                ret["$screen_width"] = screen_size.first;
                ret["$screen_height"] = screen_size.second;
            }

            return ret;
        }

        Value PlatformHelpers::collect_automatic_people_properties()
        {
            Value ret;

            ret["$android_os"] = "Android";
            ret["$android_os_version"] = get_system_property("ro.build.version.sdk");
            ret["$android_brand"] = get_system_property("ro.product.brand");
            ret["$android_device"] = get_system_property("ro.product.device");
            ret["$android_manufacturer"] = get_system_property("ro.product.manufacturer");
            ret["$android_model"] = get_system_property("ro.product.model");

            return ret;
        }
    #elif defined(WIN32)
#if WINAPI_FAMILY_DESKTOP_APP == WINAPI_FAMILY
        std::string PlatformHelpers::get_uuid()
        {
            UUID uuid;
            UuidCreate(&uuid);

            unsigned char * str;
            UuidToStringA(&uuid, &str);

            std::string ret = std::string((char*)str);

            RpcStringFreeA(&str);

            return ret;
        }


        //! get a path to a directory where we can write to. note: token might be ignored
        std::string PlatformHelpers::get_storage_directory(const std::string& token)
        {
            static std::wstring base_path;

            if (base_path.empty())
            {
                wchar_t szPath[MAX_PATH];
                if (SUCCEEDED(SHGetFolderPathW(NULL, CSIDL_APPDATA, NULL, 0, szPath)))
                {
                    base_path = szPath;
                }
                else
                {
                    std::clog << "Failed to get CSIDL_APPDATA path." << std::endl;
                    base_path = L"./";
                    assert(false);
                }

                base_path += L"\\Mixpanel\\" + utf8_to_wstring(token.substr(0, token.size() / 2)) + L"\\";

                int result = SHCreateDirectoryExW(NULL, base_path.c_str(), NULL);

                if (
                    result != ERROR_SUCCESS &&
                    result != ERROR_FILE_EXISTS &&
                    result != ERROR_ALREADY_EXISTS
                    )
                {
                    std::clog << "Failed to create directory." << std::endl;
                    assert(false);
                }
            }

            assert(!base_path.empty());

            return wstring_to_utf8(base_path);
        }


        std::string PlatformHelpers::get_os_name()
        {
            return "Windows";
        }


        std::string PlatformHelpers::get_device_model()
        {
            return "PC";
        }

        bool PlatformHelpers::is_ios()     { return false; }
        bool PlatformHelpers::is_osx()     { return false; }
        bool PlatformHelpers::is_android() { return false; }
        bool PlatformHelpers::is_windows() { return true; }
        bool PlatformHelpers::is_desktop() { return true; }
        bool PlatformHelpers::is_mobile()  { return false; }

        static std::string get_executable_version(const std::wstring& executable)
        {
            DWORD version_buf_len, handle;
            if ((version_buf_len = GetFileVersionInfoSizeW(executable.c_str(), &handle)) == 0)
            {
                auto eid = GetLastError();
                std::cerr << "GetFileVersionInfoSize failed: " << eid << '\n';
                return{};
            }

            // pre-allocated buffer for version information to be stored
            std::vector<BYTE> version_buf(version_buf_len);
            if (!GetFileVersionInfoW(executable.c_str(), 0, version_buf_len, version_buf.data()))
            {
                auto eid = GetLastError();
                std::cerr << "GetFileVersionInfo failed: " << eid << '\n';
                return {};
            }

            VS_FIXEDFILEINFO *p_verinfo = NULL; // pointer to location within version_buf
            UINT len = 0;

            // query version value
            if (!VerQueryValue(version_buf.data(), "\\", reinterpret_cast<LPVOID *>(&p_verinfo), &len))
            {
                auto eid = GetLastError();
                std::cerr << "VerQueryValue failed: " << eid << '\n';
                return{};
            }

            std::stringstream s;
            if (p_verinfo->dwSignature == 0XFEEF04BD)
            {
                // TODO - fix version info. last number may not be correct for OS information!
                s << HIWORD(p_verinfo->dwProductVersionMS) << "."
                    << LOWORD(p_verinfo->dwProductVersionMS) << "."
                    << HIWORD(p_verinfo->dwProductVersionLS) << "."
                    << LOWORD(p_verinfo->dwProductVersionLS);
            }
            return s.str();
        }

        static std::wstring get_current_executable_path()
        {
            std::vector<wchar_t> path(MAX_PATH);
            auto length = GetModuleFileNameW(NULL, path.data(), path.size());
            return{ path.begin(), path.begin() + length };
        }

        static std::string get_computer_name()
        {
            wchar_t buffer[MAX_COMPUTERNAME_LENGTH+1];
            DWORD length = sizeof(buffer) / sizeof(buffer[0]);
            if (TRUE == GetComputerNameW(buffer, &length))
            {
                return PlatformHelpers::wstring_to_utf8({buffer, buffer+length});
            }
            return{};
        }

        Value PlatformHelpers::collect_automatic_properties()
        {
            Value ret;

            ret["$brand"] = ret["$manufacturer"] = "Microsoft";
            ret["$model"] = "PC";
            ret["$os"] = "Windows";
            ret["$os_version"] = get_executable_version(L"kernel32");

            auto computer_name = get_computer_name();
            if (!computer_name.empty())
            {
                ret["$device"] = computer_name;
            }

            auto app_version = get_executable_version(get_current_executable_path());
            if (!app_version.empty())
            {
                ret["$app_version"] = ret["$app_release"] = app_version;
            }

            ret["$screen_width"] = GetSystemMetrics(SM_CXSCREEN);
            ret["$screen_height"] = GetSystemMetrics(SM_CYSCREEN);

            auto screen = GetDC(0);
            ret["$screen_dpi"] = (GetDeviceCaps(screen, LOGPIXELSX) + GetDeviceCaps(screen, LOGPIXELSY)) / 2;
            ReleaseDC(0, screen);

            return ret;
        }

        Value PlatformHelpers::collect_automatic_people_properties()
        {
            Value ret;

            ret["$windows_os_version"] = get_executable_version(L"kernel32");

            auto app_version = get_executable_version(get_current_executable_path());
            if (!app_version.empty())
            {
                ret["$windows_app_version"] = ret["$windows_app_release"] = app_version;
            }

            return ret;
        }
#else // windows phone WinRT
        std::string PlatformHelpers::get_uuid()
        {
            GUID guid;
            if (S_OK != CoCreateGuid(&guid))
                return "GUID-CREATION-FAILED";

            OLECHAR* bstrGuid;
            StringFromCLSID(guid, &bstrGuid);

            typedef std::codecvt_utf8<wchar_t> convert_type;
            std::wstring_convert<convert_type, wchar_t> converter;

            ret = converter.to_bytes(bstrGuid);

            // ensure memory is freed
            ::CoTaskMemFree(bstrGuid);
        }

        //! get a path to a directory where we can write to. note: token might be ignored
        std::string PlatformHelpers::get_storage_directory(const std::string& token)
        {
            std::wstring wbase_path = Windows::Storage::ApplicationData::Current->LocalFolder->Path->Data();
            typedef std::codecvt_utf8<wchar_t> convert_type;
            std::wstring_convert<convert_type, wchar_t> converter;
            auto base_path = converter.to_bytes(wbase_path);
            return base_path + "\\";
        }

        std::string PlatformHelpers::get_os_name()
        {
            return "Windows Phone";
        }

        std::string PlatformHelpers::get_device_model()
        {
            return "Windows Phone";
        }

        bool PlatformHelpers::is_ios()     { return false; }
        bool PlatformHelpers::is_osx()     { return false; }
        bool PlatformHelpers::is_android() { return false; }
        bool PlatformHelpers::is_windows() { return true; }
        bool PlatformHelpers::is_desktop() { return false; }
        bool PlatformHelpers::is_mobile()  { return true; }

        Value PlatformHelpers::collect_automatic_properties()
        {
            Value ret;
            return ret;
        }

        Value PlatformHelpers::collect_automatic_people_properties()
        {
            Value ret;
            return ret;
        }
#endif
        #endif /* __ANDROID__ */
    } // namespace detail
} // namespace mixpanel
