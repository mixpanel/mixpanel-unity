CMAKE_MINIMUM_REQUIRED (VERSION 3.0)

PROJECT (MixpanelTests)

add_subdirectory (gtest ${CMAKE_BINARY_DIR}/bin/gtest)
#add_subdirectory (../build/ ${CMAKE_BINARY_DIR}/bin/mixpanel)


file(GLOB_RECURSE TEST_SOURCES RELATIVE "${CMAKE_CURRENT_SOURCE_DIR}" "${CMAKE_CURRENT_SOURCE_DIR}/src/*.cpp")

add_executable(
    ${PROJECT_NAME}
    ${TARGET_EXTRA_ARG}
    ${TEST_SOURCES}
)

if(IOS)
	set_target_properties(${PROJECT_NAME} PROPERTIES XCODE_ATTRIBUTE_CODE_SIGN_IDENTITY "iPhone Developer")
endif()

# enable c++11 via -std=c++11 when compiler is clang or gcc
if( "\"${CMAKE_CXX_COMPILER_ID}\"" MATCHES AppleClang)
    add_definitions("-std=c++11")
    #target_compile_features(${PROJECT_NAME} PRIVATE cxx_nonstatic_member_init)
elseif( "\"${CMAKE_CXX_COMPILER_ID}\"" MATCHES Clang)
    target_compile_features(${PROJECT_NAME} PRIVATE cxx_nonstatic_member_init)
elseif( "\"${CMAKE_CXX_COMPILER_ID}\"" MATCHES GNU)
	#target_compile_features(${PROJECT_NAME} PRIVATE cxx_nonstatic_member_init)
elseif( "\"${CMAKE_CXX_COMPILER_ID}\"" MATCHES Intel)
  target_link_libraries(${PROJECT_NAME} Rpcrt4.lib)
elseif( "\"${CMAKE_CXX_COMPILER_ID}\"" MATCHES MSVC)
  target_link_libraries(${PROJECT_NAME} Rpcrt4.lib)
endif()

target_include_directories(
    ${PROJECT_NAME}
    PRIVATE
    "${CMAKE_CURRENT_SOURCE_DIR}/../source/dependencies/jsoncpp/"
    "${CMAKE_CURRENT_SOURCE_DIR}/../source/"
)
target_include_directories(${PROJECT_NAME} PRIVATE "${SOURCE_DIR}/../source/dependencies/mbedtls/include")
add_definitions(-DHAVE_MBEDTLS=1)

if (APPLE)
    TARGET_LINK_LIBRARIES (${PROJECT_NAME} "-framework Foundation")
    if(IOS)
        TARGET_LINK_LIBRARIES(${PROJECT_NAME} "-framework UIKit")
        TARGET_LINK_LIBRARIES(${PROJECT_NAME} "-framework CoreTelephony")
        set_xcode_property (${PROJECT_NAME} IPHONEOS_DEPLOYMENT_TARGET "7.0")
    else()
        TARGET_LINK_LIBRARIES (${PROJECT_NAME} "-framework AppKit")
    endif()
endif()

TARGET_LINK_LIBRARIES(${PROJECT_NAME} gtest mixpanel)
