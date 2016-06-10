CMAKE_MINIMUM_REQUIRED(VERSION 3.0)

set(CMAKE_CXX_FLAGS_DEBUG "${CMAKE_CXX_FLAGS_DEBUG} -DDEBUG -D_DEBUG")
set(CMAKE_CXX_FLAGS_RELEASE "${CMAKE_CXX_FLAGS_RELEASE} -DNDEBUG")

PROJECT (Mixpanel)

#set(CMAKE_MODULE_PATH ${CMAKE_MODULE_PATH} "${CMAKE_CURRENT_SOURCE_DIR}/../../jenkins/CMakeIncludes")
message(STATUS ${CMAKE_MODULE_PATH})
# useful macro for recursive search of files
#include("create_source_groups_macro")

set(SOURCE_DIR "${PROJECT_SOURCE_DIR}/../source")

if(WIN32)
	if( CMAKE_SIZEOF_VOID_P EQUAL 8 )
		set(PLATFORM win64)
	else()
		set(PLATFORM win32)
	endif()
	set(CMAKE_CXX_FLAGS_RELEASE "${CMAKE_CXX_FLAGS_RELEASE} /MT")
	set(CMAKE_CXX_FLAGS_DEBUG "${CMAKE_CXX_FLAGS_DEBUG} /MTd")
else()
		set(PLATFORM osx)
endif()

message(STATUS "********************** platform is ${PLATFORM}")

FILE(GLOB_RECURSE SOURCES
	"${SOURCE_DIR}/*.cpp"
	"${SOURCE_DIR}/*.hpp"
	"${SOURCE_DIR}/*.c"
	"${SOURCE_DIR}/../include/*.hpp"
)

if("${PLATFORM}" STREQUAL "win32")
	SET(CMAKE_CXX_FLAGS  "${CMAKE_CXX_FLAGS} /MP" ) # Multiprocessor compilation
	add_definitions("-D_CRT_SECURE_NO_WARNINGS") #  warning C4996
	set(UNITY_PLUGIN_FILE_NAME "x86/MixpanelSDK.dll")
elseif("${PLATFORM}" STREQUAL "win64")
	MESSAGE("******* WIN64")
	SET(CMAKE_CXX_FLAGS  "${CMAKE_CXX_FLAGS} /MP" ) # Multiprocessor compilation
	add_definitions("-D_CRT_SECURE_NO_WARNINGS") #  warning C4996
	set(UNITY_PLUGIN_FILE_NAME "x86_64/MixpanelSDK.dll")
elseif(IOS)
	message("***** iOS")
	set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -std=c++11 -stdlib=libc++ -fembed-bitcode")
	set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -fembed-bitcode")
	set(UNITY_PLUGIN_FILE_NAME "iOS/MixpanelSDK.a")
	set(TARGET_EXTRA_ARG "MACOSX_BUNDLE")
	set(MACOSX_BUNDLE_GUI_IDENTIFIER "com.mixpanel.\${PRODUCT_NAME:identifier}")

	set(STRIP_ARGS -S -x)
    set(SOURCES ${SOURCES} "${SOURCE_DIR}/mixpanel/detail/platform_helpers.mm")
elseif("${CMAKE_SYSTEM_NAME}" STREQUAL "Android")
	message("****** Android")
	set(UNITY_PLUGIN_FILE_NAME "Android/libs/${ANDROID_ABI}/libMixpanelSDK.so")

	set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -std=c++11 -fvisibility=default -fPIE")

	# prevent linking against libstdc++
	set(CMAKE_CXX_CREATE_SHARED_LIBRARY "${CMAKE_CXX_CREATE_SHARED_LIBRARY} -nodefaultlibs -lm -lc -ldl -lgcc")
	set(CMAKE_CXX_CREATE_SHARED_MODULE  "${CMAKE_CXX_CREATE_SHARED_MODULE} -nodefaultlibs -lm -lc -ldl -lgcc")
	set(CMAKE_CXX_LINK_EXECUTABLE       "${CMAKE_CXX_LINK_EXECUTABLE} -nodefaultlibs -lm -lc -ldl -lgcc -rdynamic -fPIE -pie")

	set(STRIP_ARGS --strip-unneeded)
elseif("${PLATFORM}" STREQUAL "osx")
	set(CMAKE_CXX_FLAGS "-std=c++11 -stdlib=libc++")
	set(CMAKE_OSX_ARCHITECTURES "x86_64;i386")
	set(UNITY_PLUGIN_FILE_NAME "MixpanelSDK.bundle/Contents/MacOS/MixpanelSDK")

	# ugly workaround for Version 7.0
	set(CMAKE_OSX_SYSROOT "/Applications/Xcode.app/Contents/Developer/Platforms/MacOSX.platform/Developer/SDKs/MacOSX10.11.sdk")

	set(STRIP_ARGS -S -x)
    set(SOURCES ${SOURCES} "${SOURCE_DIR}/mixpanel/detail/platform_helpers.mm")
endif("${PLATFORM}" STREQUAL "win32")

if(NOT CMAKE_STRIP)
	set(CMAKE_STRIP "echo")
endif()

if(${CMAKE_STRIP} STREQUAL "CMAKE_STRIP-NOTFOUND")
	set(CMAKE_STRIP "echo")
endif()

add_subdirectory(../tests/ ${CMAKE_BINARY_DIR}/bin/mixpanel)


ADD_LIBRARY (mixpanel STATIC ${SOURCES})

target_include_directories(mixpanel PUBLIC "${SOURCE_DIR}/../include")
target_include_directories(mixpanel PRIVATE "${SOURCE_DIR}/../source/dependencies/mbedtls/include")
add_definitions(-DHAVE_MBEDTLS=1)

FIND_PACKAGE(SWIG REQUIRED)
MESSAGE(${SWIG_USE_FILE})
#INCLUDE(${SWIG_USE_FILE})
INCLUDE(toolchains/UseSWIG.cmake)

SET(CMAKE_SWIG_FLAGS -namespace mixpanel.detail -Wall -v) #-oldvarnames -noproxy
SET(CMAKE_SWIG_OUTDIR "${PROJECT_SOURCE_DIR}/../../deployments/UnityMixpanel/Assets/Mixpanel/detail")
SET(SWIG_MODULE_MixpanelSDK_EXTRA_FLAGS "-Wall -v -namespace mixpanel.internal")
SET_SOURCE_FILES_PROPERTIES(${PROJECT_SOURCE_DIR}/mixpanel.i PROPERTIES CPLUSPLUS ON)
SWIG_ADD_MODULE(MixpanelSDK csharp ${PROJECT_SOURCE_DIR}/mixpanel.i ${SOURCES})
#set_target_properties(MixpanelSDK PROPERTIES OSX_ARCHITECTURES "x86_64;i386")

if(IOS)
    set_xcode_property (MixpanelSDK IPHONEOS_DEPLOYMENT_TARGET "7.0")
    set_xcode_property (mixpanel IPHONEOS_DEPLOYMENT_TARGET "7.0")
    TARGET_LINK_LIBRARIES (MixpanelSDK "-framework CoreTelephony")
    TARGET_LINK_LIBRARIES (mixpanel "-framework CoreTelephony")
endif()

target_include_directories(MixpanelSDK PUBLIC "${SOURCE_DIR}/../include")
target_include_directories(MixpanelSDK PRIVATE "${SOURCE_DIR}/../source/dependencies/mbedtls/include")

if (APPLE)
	TARGET_LINK_LIBRARIES (MixpanelSDK "-framework Foundation")
	TARGET_LINK_LIBRARIES (MixpanelSDK "-framework AppKit")
endif()

set(UNITY_ASSETS_DIR "${PROJECT_SOURCE_DIR}/../../deployments/UnityMixpanel/Assets/")
ADD_CUSTOM_COMMAND(TARGET MixpanelSDK
          PRE_BUILD
          COMMAND ${CMAKE_COMMAND} -E remove ${UNITY_ASSETS_DIR}/Plugins/Mixpanel/detail/mixpanelCSHARP_wrap.cxx ${UNITY_ASSETS_DIR}/Plugins/Mixpanel/detail/mixpanelCSHARP_wrap.h
)

ADD_CUSTOM_COMMAND(TARGET MixpanelSDK
          POST_BUILD
          COMMAND ${CMAKE_COMMAND} -E make_directory ${UNITY_ASSETS_DIR}/Plugins
          COMMAND ${CMAKE_COMMAND} -E copy "$<TARGET_FILE:MixpanelSDK>" "${UNITY_ASSETS_DIR}/Plugins/${UNITY_PLUGIN_FILE_NAME}"
          COMMAND ${CMAKE_COMMAND} -E remove ${UNITY_ASSETS_DIR}/Mixpanel/detail/MixpanelSDK.cs
          COMMAND ${CMAKE_COMMAND} -E remove ${UNITY_ASSETS_DIR}/Mixpanel/detail/mixpanelCSHARP_wrap.cxx
          COMMAND ${CMAKE_COMMAND} -E remove ${UNITY_ASSETS_DIR}/Mixpanel/detail/mixpanelCSHARP_wrap.h
          COMMAND ${CMAKE_STRIP} ${STRIP_ARGS} "${UNITY_ASSETS_DIR}/Plugins/${UNITY_PLUGIN_FILE_NAME}"
)

# enable c++11 via -std=c++11 when compiler is clang or gcc
if( "\"${CMAKE_CXX_COMPILER_ID}\"" MATCHES AppleClang)
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -std=c++11")
    #target_compile_features(mixpanel PRIVATE cxx_nonstatic_member_init)
	#target_compile_features(MixpanelSDK PRIVATE cxx_nonstatic_member_init)
elseif( "\"${CMAKE_CXX_COMPILER_ID}\"" MATCHES Clang)
	target_compile_features(mixpanel PRIVATE cxx_nonstatic_member_init)
	target_compile_features(MixpanelSDK PRIVATE cxx_nonstatic_member_init)
elseif( "\"${CMAKE_CXX_COMPILER_ID}\"" MATCHES GNU)
	#target_compile_features(mixpanel PRIVATE cxx_nonstatic_member_init)
	#target_compile_features(MixpanelSDK PRIVATE cxx_nonstatic_member_init)
elseif( "\"${CMAKE_CXX_COMPILER_ID}\"" MATCHES Intel)
  # using Intel C++
elseif( "\"${CMAKE_CXX_COMPILER_ID}\"" MATCHES MSVC)
  # using Visual Studio C++
endif()

# fixes compilation with cmake > 3.2.0
# http://stackoverflow.com/questions/26841603/arm-linux-androideabi-bin-ld-fatal-error-soname-must-take-a-non-empty-argume
string(REPLACE "<CMAKE_SHARED_LIBRARY_SONAME_CXX_FLAG><TARGET_SONAME>" "" CMAKE_CXX_CREATE_SHARED_MODULE "${CMAKE_CXX_CREATE_SHARED_MODULE}")

file(GLOB SAMPLE_FILES "${SOURCE_DIR}/../samples/*.cpp")

foreach(SAMPLE_FILE ${SAMPLE_FILES})
	get_filename_component(SAMPLE_NAME ${SAMPLE_FILE} NAME_WE)

	ADD_EXECUTABLE (
		${SAMPLE_NAME}
		${TARGET_EXTRA_ARG}
		${SAMPLE_FILE}
	)

	if(IOS)
		set_target_properties(${SAMPLE_NAME} PROPERTIES XCODE_ATTRIBUTE_CODE_SIGN_IDENTITY "iPhone Developer")
        TARGET_LINK_LIBRARIES (${SAMPLE_NAME} "-framework UIKit")
        set_xcode_property (${SAMPLE_NAME} IPHONEOS_DEPLOYMENT_TARGET "7.0")
	endif()

	TARGET_LINK_LIBRARIES (${SAMPLE_NAME} mixpanel)

    if (APPLE)
		TARGET_LINK_LIBRARIES (${SAMPLE_NAME} "-framework Foundation")
		if(IOS)
		else()
			TARGET_LINK_LIBRARIES (${SAMPLE_NAME} "-framework AppKit")
		endif()
    endif()
endforeach()

install(TARGETS mixpanel
        RUNTIME DESTINATION "${PROJECT_SOURCE_DIR}/../../deployments/native/lib"
        LIBRARY DESTINATION "${PROJECT_SOURCE_DIR}/../../deployments/native/lib"
        ARCHIVE DESTINATION "${PROJECT_SOURCE_DIR}/../../deployments/native/lib")

install(DIRECTORY "${SOURCE_DIR}/../include/" DESTINATION "${PROJECT_SOURCE_DIR}/../../deployments/native/include")
install(DIRECTORY "${SOURCE_DIR}/../samples/" DESTINATION "${PROJECT_SOURCE_DIR}/../../deployments/native/samples")
