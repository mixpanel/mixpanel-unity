#include "platform_helpers.hpp"
#include <mixpanel/mixpanel.hpp>
#include <iostream>
#ifdef __APPLE__
#include "TargetConditionals.h"

#if TARGET_OS_IPHONE || TARGET_IPHONE_SIMULATOR
#	import <UIKit/UIKit.h>
#   import <CoreTelephony/CTTelephonyNetworkInfo.h>
#   import <CoreTelephony/CTCarrier.h>
#elif TARGET_OS_MAC
#	import <Foundation/Foundation.h>
#   import <AppKit/AppKit.h>
#   include <sys/stat.h>
#   include <vector>
#   include <iostream>


/* works like mkdir(1) used as "mkdir -p" */
static void mkdirp(const char *dir) {
    char tmp[PATH_MAX];
    char *p = NULL;
    size_t len;

    snprintf(tmp, sizeof(tmp),"%s",dir);
    len = strlen(tmp);
    if (tmp[len - 1] == '/')
    {
        tmp[len - 1] = 0;
    }
    
    for (p = tmp + 1; *p; p++)
    {
        if (*p == '/') 
        {
            *p = 0;
            mkdir(tmp, S_IRWXU | S_IRWXG);
            *p = '/';
        }
    }

    mkdir(tmp, S_IRWXU | S_IRWXG);
}

#endif

#include <sys/sysctl.h>

#ifdef bool
#   undef bool
#endif

namespace mixpanel
{
    namespace detail
    {
        static std::string sysctlbyname(const std::string name)
        {
            size_t length;
            std::string hw_model;
            ::sysctlbyname(name.c_str(), NULL, &length, NULL, 0);
            if (length)
            {
                std::vector<char> hw_string_(length, 0);
                ::sysctlbyname(name.c_str(), hw_string_.data(), &length, NULL, 0);
                std::copy(hw_string_.begin(), hw_string_.end() - 1, std::back_inserter(hw_model));
            }
            return hw_model;
        }

        #if TARGET_OS_IPHONE || TARGET_IPHONE_SIMULATOR
        std::string PlatformHelpers::get_uuid()
        {
            static std::string device_id;

            if (device_id.empty())
            {
                UIDevice *device = [UIDevice currentDevice];
                #pragma clang diagnostic ignored "-Wundeclared-selector"
                if ([UIDevice instancesRespondToSelector:@selector(identifierForVendor)]) {
                    device_id = [[[device identifierForVendor] UUIDString] UTF8String];
                } else {
                    #pragma clang diagnostic push
                    #pragma clang diagnostic ignored "-Wdeprecated-declarations"
                    device_id = [[[NSUUID UUID] UUIDString] UTF8String];
                }
            }
		    #pragma clang diagnostic pop

		    return device_id;
		}

        std::string PlatformHelpers::get_storage_directory(const std::string& token)
        {
            NSString *libraryPath = [NSSearchPathForDirectoriesInDomains(NSLibraryDirectory, NSUserDomainMask, YES) lastObject];
    	    return [libraryPath UTF8String];
        }

        std::string PlatformHelpers::get_os_name()
        {
            return "iOS";
        }

        std::string PlatformHelpers::get_device_model()
        {
            std::string hw_model = sysctlbyname("hw.machine");
            return hw_model.empty() ? "Unknown iOS Device" : hw_model;
        }

        bool PlatformHelpers::is_ios()     { return true; }
		bool PlatformHelpers::is_osx()     { return false; }
		bool PlatformHelpers::is_android() { return false; }
		bool PlatformHelpers::is_windows() { return false; }
		bool PlatformHelpers::is_desktop() { return false; }
		bool PlatformHelpers::is_mobile()  { return true; }

		static std::string ios_ifa()
		{
		    NSString *ifa = nil;
            #if !defined(MIXPANEL_NO_IFA)
            Class ASIdentifierManagerClass = NSClassFromString(@"ASIdentifierManager");
            if (ASIdentifierManagerClass) {
                SEL sharedManagerSelector = NSSelectorFromString(@"sharedManager");
                id sharedManager = ((id (*)(id, SEL))[ASIdentifierManagerClass methodForSelector:sharedManagerSelector])(ASIdentifierManagerClass, sharedManagerSelector);
                SEL advertisingIdentifierSelector = NSSelectorFromString(@"advertisingIdentifier");
                NSUUID *uuid = ((NSUUID* (*)(id, SEL))[sharedManager methodForSelector:advertisingIdentifierSelector])(sharedManager, advertisingIdentifierSelector);
                ifa = [uuid UUIDString];
            }
            #endif
            return ifa ? [ifa UTF8String] : "";
		}


        Value PlatformHelpers::collect_automatic_properties()
        {
            Value ret;

            NSString *app_build = [[NSBundle mainBundle] infoDictionary][@"CFBundleVersion"];
            if (app_build && ![app_build isEqualToString:@""]) {
                ret["$app_build_number"] = [app_build UTF8String];
            }
            
            NSString *app_version = [[NSBundle mainBundle] infoDictionary][@"CFBundleShortVersionString"];
            if (app_version && ![app_version isEqualToString:@""]) {
                ret["$app_version_string"] = [app_version UTF8String];
            }

            ret["$manufacturer"] = "Apple";
            ret["$device"] = [[[UIDevice currentDevice] name] UTF8String];
            ret["$model"] = get_device_model();
            ret["$os"] = [[[UIDevice currentDevice] systemName] UTF8String];
            ret["$os_version"] = [[[UIDevice currentDevice] systemVersion] UTF8String];

            // Missing $carrier (Need to edit build script to link against this framework)
            CTTelephonyNetworkInfo *info = [[CTTelephonyNetworkInfo alloc] init];
            CTCarrier *carrier = [info subscriberCellularProvider];
            ret["$carrier"] = carrier.carrierName;

            CGSize size = [UIScreen mainScreen].bounds.size;
            ret["$screen_width"] = size.width;
            ret["$screen_height"] = size.height;

            #if !defined(MIXPANEL_NO_IFA)
            ret["$ios_ifa"] = ios_ifa();
            #endif

            return ret;
        }

        Value PlatformHelpers::collect_automatic_people_properties()
        {
            Value ret;
            
            NSString *app_build = [[NSBundle mainBundle] infoDictionary][@"CFBundleVersion"];
            if (app_build && ![app_build isEqualToString:@""]) {
                ret["$ios_app_build_number"] = [app_build UTF8String];
            }
            
            NSString *app_version = [[NSBundle mainBundle] infoDictionary][@"CFBundleShortVersionString"];
            if (app_version && ![app_version isEqualToString:@""]) {
                ret["$ios_app_version_string"] = [app_version UTF8String];
            }

            ret["$ios_version"] = [[[UIDevice currentDevice] systemVersion] UTF8String];
            ret["$ios_device_model"] = get_device_model();

            #if !defined(MIXPANEL_NO_IFA)
            ret["$ios_ifa"] = ios_ifa();
            #endif

            return ret;
        }
        #elif TARGET_OS_MAC
        // trim from start
        static inline std::string &ltrim(std::string &s) {
            s.erase(s.begin(), std::find_if(s.begin(), s.end(), std::not1(std::ptr_fun<int, int>(std::isspace))));
            return s;
        }

        // trim from end
        static inline std::string &rtrim(std::string &s) {
            s.erase(std::find_if(s.rbegin(), s.rend(), std::not1(std::ptr_fun<int, int>(std::isspace))).base(), s.end());
            return s;
        }

        // trim from both ends
        static inline std::string &trim(std::string &s) {
            return ltrim(rtrim(s));
        }

        std::string PlatformHelpers::get_uuid()
        {
            static std::string device_id;

            if (device_id.empty())
            {
                NSArray * args = @[@"-rd1", @"-c", @"IOPlatformExpertDevice", @"|", @"grep", @"model"];
                NSTask * task = [NSTask new];
                [task setLaunchPath:@"/usr/sbin/ioreg"];
                [task setArguments:args];

                NSPipe * pipe = [NSPipe new];
                [task setStandardOutput:pipe];
                [task launch];

                NSArray * args2 = @[@"/IOPlatformUUID/ { split($0, line, \"\\\"\"); printf(\"%s\\n\", line[4]); }"];
                NSTask * task2 = [NSTask new];
                [task2 setLaunchPath:@"/usr/bin/awk"];
                [task2 setArguments:args2];

                NSPipe * pipe2 = [NSPipe new];
                [task2 setStandardInput:pipe];
                [task2 setStandardOutput:pipe2];
                NSFileHandle * fileHandle2 = [pipe2 fileHandleForReading];
                [task2 launch];

                NSData * data = [fileHandle2 readDataToEndOfFile];
                NSString * uuid = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];

                device_id = [uuid UTF8String];
                trim(device_id);
            }

            return device_id;
        }

        std::string PlatformHelpers::get_storage_directory(const std::string& token)
        {
            static std::string base_path;
            if (base_path.empty())
            {
                // the environment might not be correctly setup, then we store data in /tmp
                std::string writable_path("/tmp/Mixpanel");
                if (char *homedir = getenv("HOME"))
                    writable_path = homedir;

                // at least we're using only half the token. We might want to consider switching to hmac
                writable_path += "/Library/Application Support/Mixpanel/" + token.substr(0, token.size() / 2) + "/";

                struct stat s;
                if (0 != stat(writable_path.c_str(), &s)) // Check if directory exists
                {
                    mkdirp(writable_path.c_str());
                }

                base_path = writable_path;
            }
            return base_path;
        }

        std::string PlatformHelpers::get_os_name()
        {
            return "OS X";
        }

        static bool startswith(const std::string s, const std::string& with)
        {
            return 0 == s.compare(0, with.size(), with);
        }

        std::string PlatformHelpers::get_device_model()
        {
            std::string hw_model = sysctlbyname("hw.model");
            return hw_model.empty() ? "Unknown Mac" : hw_model;
        }

        bool PlatformHelpers::is_ios()     { return false; }
        bool PlatformHelpers::is_osx()     { return true; }
        bool PlatformHelpers::is_android() { return false; }
        bool PlatformHelpers::is_windows() { return false; }
        bool PlatformHelpers::is_desktop() { return true; }
        bool PlatformHelpers::is_mobile()  { return false; }

        Value PlatformHelpers::collect_automatic_properties()
        {
            Value ret;

            NSString *app_build = [[NSBundle mainBundle] infoDictionary][@"CFBundleVersion"];
            if (app_build && ![app_build isEqualToString:@""]) {
                ret["$app_build_number"] = [app_build UTF8String];
            }

            NSString *app_version = [[NSBundle mainBundle] infoDictionary][@"CFBundleShortVersionString"];
            if (app_version && ![app_version isEqualToString:@""]) {
                ret["$app_version_string"] = [app_version UTF8String];
            }

            ret["$brand"] = ret["$manufacturer"] = "Apple";
            ret["$model"] = get_device_model();

            ret["$device"] = [[[NSHost currentHost] localizedName] UTF8String];
            ret["$os"] = "Mac OS X";
            ret["$os_version"] = sysctlbyname("kern.osrelease");

            NSSize size = [[NSScreen mainScreen] frame].size;
            ret["$screen_width"] = size.width;
            ret["$screen_height"] = size.height;

            return ret;
        }

        Value PlatformHelpers::collect_automatic_people_properties()
        {
            Value ret;

            NSString *app_build = [[NSBundle mainBundle] infoDictionary][@"CFBundleVersion"];
            if (app_build && ![app_build isEqualToString:@""]) {
                ret["$mac_os_app_build_number"] = [app_build UTF8String];
            }
            
            NSString *app_version = [[NSBundle mainBundle] infoDictionary][@"CFBundleShortVersionString"];
            if (app_version && ![app_version isEqualToString:@""]) {
                ret["$mac_os_app_version_string"] = [app_version UTF8String];
            }

            ret["$mac_os_version"] = sysctlbyname("kern.osrelease");
            ret["$mac_os_device_model"] = get_device_model();

            return ret;
        }
        #endif
    }
}
#endif /* __APPLE__ */
