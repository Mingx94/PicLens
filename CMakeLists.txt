cmake_minimum_required(VERSION 3.21)

file(STRINGS "${CMAKE_CURRENT_SOURCE_DIR}/VERSION" PICLENS_VERSION LIMIT_COUNT 1)
string(STRIP "${PICLENS_VERSION}" PICLENS_VERSION)
if(NOT PICLENS_VERSION MATCHES "^[0-9]+\\.[0-9]+\\.[0-9]+$")
    message(FATAL_ERROR "VERSION must contain a semantic version, got: ${PICLENS_VERSION}")
endif()

project(PicLensQt VERSION ${PICLENS_VERSION} LANGUAGES CXX)

if(WIN32)
    enable_language(RC)
endif()

option(PICLENS_SYSTEM_PACKAGE "Install desktop integration into system locations for DEB/RPM" OFF)
option(PICLENS_USE_SYSTEM_QT "Use distribution Qt packages instead of deploying Qt libraries" OFF)
set(PICLENS_QT_SOURCE_ROOT "" CACHE PATH "Optional Qt source root used to package license texts")
if(UNIX AND PICLENS_SYSTEM_PACKAGE)
    set(
        CMAKE_INSTALL_PREFIX "/opt/piclens"
        CACHE PATH "Fixed PicLens system-package application prefix" FORCE
    )
endif()
include(GNUInstallDirs)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin")

find_package(Qt6 6.5 REQUIRED COMPONENTS Concurrent Core Gui Qml Quick QuickControls2)
qt_standard_project_setup(REQUIRES 6.5)

include(CTest)

add_subdirectory(src/core)
add_subdirectory(src/infrastructure)
add_subdirectory(src/presentation)
add_subdirectory(src/app)

if(BUILD_TESTING)
    find_package(Qt6 6.5 REQUIRED COMPONENTS QuickTest Test)
    add_subdirectory(tests/core)
    add_subdirectory(tests/infrastructure)
    add_subdirectory(tests/presentation)
    add_subdirectory(tests/app)
    add_subdirectory(tests/qml)
endif()

set(CPACK_PACKAGE_NAME "PicLens")
set(CPACK_PACKAGE_VENDOR "PicLens")
set(CPACK_PACKAGE_DESCRIPTION_SUMMARY "PicLens image organizer and viewer")
set(CPACK_PACKAGE_VERSION "${PROJECT_VERSION}")
set(CPACK_PACKAGING_INSTALL_PREFIX "/opt/piclens")
set(CPACK_SET_DESTDIR ON)
set(CPACK_VERBATIM_VARIABLES ON)
set(CPACK_DEBIAN_FILE_NAME DEB-DEFAULT)
set(CPACK_DEBIAN_PACKAGE_NAME "piclens")
set(CPACK_DEBIAN_PACKAGE_MAINTAINER "PicLens")
set(CPACK_DEBIAN_PACKAGE_SECTION "graphics")
set(CPACK_DEBIAN_PACKAGE_SHLIBDEPS ON)
set(CPACK_RPM_FILE_NAME RPM-DEFAULT)
set(CPACK_RPM_PACKAGE_NAME "piclens")
set(CPACK_RPM_PACKAGE_LICENSE "MIT")
set(CPACK_RPM_PACKAGE_GROUP "Applications/Multimedia")
set(CPACK_RPM_PACKAGE_AUTOREQPROV ON)
include(CPack)
