qt_add_library(piclens_app STATIC
    include/piclens/app/app_controller.h
    src/app_controller.cpp
)

add_library(PicLens::App ALIAS piclens_app)

target_include_directories(piclens_app
    PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}/include
)

target_link_libraries(piclens_app
    PUBLIC
        PicLens::Infrastructure
        PicLens::Presentation
        Qt6::Core
    PRIVATE
        Qt6::Concurrent
)

if(MSVC)
    target_compile_options(piclens_app PRIVATE /W4 /permissive-)
else()
    target_compile_options(piclens_app PRIVATE -Wall -Wextra -Wpedantic)
endif()

set(PICLENS_QML_FILES
    ${CMAKE_CURRENT_SOURCE_DIR}/../../qml/PicLens/Theme.qml
    ${CMAKE_CURRENT_SOURCE_DIR}/../../qml/PicLens/AppIcon.qml
    ${CMAKE_CURRENT_SOURCE_DIR}/../../qml/PicLens/LensMark.qml
    ${CMAKE_CURRENT_SOURCE_DIR}/../../qml/PicLens/CompactComboBox.qml
    ${CMAKE_CURRENT_SOURCE_DIR}/../../qml/PicLens/ToolbarButton.qml
    ${CMAKE_CURRENT_SOURCE_DIR}/../../qml/PicLens/GalleryTile.qml
    ${CMAKE_CURRENT_SOURCE_DIR}/../../qml/PicLens/HistoryMouseHandler.qml
    ${CMAKE_CURRENT_SOURCE_DIR}/../../qml/PicLens/FolderTreePane.qml
    ${CMAKE_CURRENT_SOURCE_DIR}/../../qml/PicLens/LibraryPane.qml
    ${CMAKE_CURRENT_SOURCE_DIR}/../../qml/PicLens/ViewerPointerSurface.qml
    ${CMAKE_CURRENT_SOURCE_DIR}/../../qml/PicLens/ViewerOverlay.qml
    ${CMAKE_CURRENT_SOURCE_DIR}/../../qml/PicLens/Main.qml
)

set_source_files_properties(
    ${CMAKE_CURRENT_SOURCE_DIR}/../../qml/PicLens/Theme.qml
    PROPERTIES
        QT_QML_SINGLETON_TYPE TRUE
        QT_RESOURCE_ALIAS Theme.qml
)
foreach(qml_file IN LISTS PICLENS_QML_FILES)
    get_filename_component(qml_name "${qml_file}" NAME)
    set_source_files_properties("${qml_file}" PROPERTIES QT_RESOURCE_ALIAS "${qml_name}")
endforeach()

set(PICLENS_RESOURCE_FILES
    ${CMAKE_CURRENT_SOURCE_DIR}/../../assets/AppIcon.ico
    ${CMAKE_CURRENT_SOURCE_DIR}/../../assets/Square150x150Logo.scale-200.png
)
set_source_files_properties(
    ${CMAKE_CURRENT_SOURCE_DIR}/../../assets/AppIcon.ico
    PROPERTIES QT_RESOURCE_ALIAS assets/AppIcon.ico
)
set_source_files_properties(
    ${CMAKE_CURRENT_SOURCE_DIR}/../../assets/Square150x150Logo.scale-200.png
    PROPERTIES QT_RESOURCE_ALIAS assets/AppLogo.png
)

qt_add_executable(piclens
    include/piclens/app/qml_registration.h
    src/main.cpp
)

if(WIN32)
    get_filename_component(
        PICLENS_WINDOWS_ICON
        "${CMAKE_CURRENT_SOURCE_DIR}/../../assets/AppIcon.ico"
        ABSOLUTE
    )
    configure_file(
        resources/windows/PicLens.rc.in
        "${CMAKE_CURRENT_BINARY_DIR}/PicLens.rc"
        @ONLY
    )
    target_sources(piclens PRIVATE "${CMAKE_CURRENT_BINARY_DIR}/PicLens.rc")
endif()

qt_add_qml_module(piclens
    URI PicLens
    VERSION 1.0
    QML_FILES ${PICLENS_QML_FILES}
    RESOURCES ${PICLENS_RESOURCE_FILES}
)

target_link_libraries(piclens
    PRIVATE
        PicLens::App
        Qt6::Gui
        Qt6::Qml
        Qt6::Quick
        Qt6::QuickControls2
)
if(NOT TARGET Qt6::QWebpPlugin)
    message(FATAL_ERROR "PicLens requires the Qt Image Formats qwebp plugin")
endif()
qt_import_plugins(piclens INCLUDE Qt6::QWebpPlugin)
if(WIN32)
    target_link_libraries(piclens PRIVATE psapi)
endif()
target_include_directories(piclens PRIVATE ${CMAKE_CURRENT_SOURCE_DIR}/include/piclens/app)
target_compile_definitions(piclens PRIVATE PICLENS_VERSION="${PROJECT_VERSION}")

set_target_properties(piclens PROPERTIES
    OUTPUT_NAME PicLens
    WIN32_EXECUTABLE TRUE
)
if(UNIX AND NOT APPLE)
    set_target_properties(piclens PROPERTIES INSTALL_RPATH "$ORIGIN/../${CMAKE_INSTALL_LIBDIR}")
endif()

if(MSVC)
    target_compile_options(piclens PRIVATE /W4 /permissive-)
else()
    target_compile_options(piclens PRIVATE -Wall -Wextra -Wpedantic)
endif()

install(TARGETS piclens
    BUNDLE DESTINATION .
    RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
)
if(NOT PICLENS_USE_SYSTEM_QT)
    qt_generate_deploy_qml_app_script(
        TARGET piclens
        OUTPUT_SCRIPT piclens_deploy_script
        NO_TRANSLATIONS
        NO_UNSUPPORTED_PLATFORM_ERROR
        DEPLOY_USER_QML_MODULES_ON_UNSUPPORTED_PLATFORM
    )
    install(SCRIPT ${piclens_deploy_script})
endif()
install(
    FILES
        ${CMAKE_CURRENT_SOURCE_DIR}/../../LICENSE
        ${CMAKE_CURRENT_SOURCE_DIR}/../../THIRD_PARTY_NOTICES.txt
    DESTINATION ${CMAKE_INSTALL_DATADIR}/licenses/PicLens
)
if(PICLENS_SYSTEM_PACKAGE)
    set(PICLENS_ICON_DESTINATION "/usr/share/icons/hicolor/300x300/apps")
    set(PICLENS_DESKTOP_DESTINATION "/usr/share/applications")
else()
    set(PICLENS_ICON_DESTINATION "${CMAKE_INSTALL_DATADIR}/icons/hicolor/300x300/apps")
    set(PICLENS_DESKTOP_DESTINATION "${CMAKE_INSTALL_DATADIR}/applications")
endif()
install(
    FILES ${CMAKE_CURRENT_SOURCE_DIR}/../../assets/Square150x150Logo.scale-200.png
    DESTINATION ${PICLENS_ICON_DESTINATION}
    RENAME piclens.png
)
install(
    FILES ${CMAKE_CURRENT_SOURCE_DIR}/../../packaging/piclens.desktop
    DESTINATION ${PICLENS_DESKTOP_DESTINATION}
)
get_filename_component(PICLENS_QT_INSTALL_PREFIX "${Qt6_DIR}/../../.." ABSOLUTE)
if(EXISTS "${PICLENS_QT_INSTALL_PREFIX}/LICENSES")
    install(
        DIRECTORY "${PICLENS_QT_INSTALL_PREFIX}/LICENSES/"
        DESTINATION ${CMAKE_INSTALL_DATADIR}/licenses/Qt
    )
elseif(EXISTS "${PICLENS_QT_SOURCE_ROOT}/qtbase/LICENSES" AND
       EXISTS "${PICLENS_QT_SOURCE_ROOT}/qtdeclarative/LICENSES" AND
       EXISTS "${PICLENS_QT_SOURCE_ROOT}/qtimageformats/LICENSES")
    install(
        DIRECTORY "${PICLENS_QT_SOURCE_ROOT}/qtbase/LICENSES/"
        DESTINATION ${CMAKE_INSTALL_DATADIR}/licenses/Qt/qtbase
    )
    install(
        DIRECTORY "${PICLENS_QT_SOURCE_ROOT}/qtdeclarative/LICENSES/"
        DESTINATION ${CMAKE_INSTALL_DATADIR}/licenses/Qt/qtdeclarative
    )
    install(
        DIRECTORY "${PICLENS_QT_SOURCE_ROOT}/qtimageformats/LICENSES/"
        DESTINATION ${CMAKE_INSTALL_DATADIR}/licenses/Qt/qtimageformats
    )
    if(EXISTS "${PICLENS_QT_SOURCE_ROOT}/qtimageformats/src/3rdparty/libwebp/COPYING")
        install(
            FILES "${PICLENS_QT_SOURCE_ROOT}/qtimageformats/src/3rdparty/libwebp/COPYING"
            DESTINATION ${CMAKE_INSTALL_DATADIR}/licenses/Qt/qtimageformats
            RENAME libwebp-COPYING
        )
    endif()
endif()

if(BUILD_TESTING)
    set(PICLENS_RUNTIME_DATA_ROOT "${CMAKE_BINARY_DIR}/Testing/runtime-data")
    add_test(
        NAME qml_runtime_data_prepare
        COMMAND ${CMAKE_COMMAND} -E rm -rf ${PICLENS_RUNTIME_DATA_ROOT}
    )
    add_test(
        NAME qml_runtime_smoke
        COMMAND piclens
            --smoke-ms 750
            --data-root ${PICLENS_RUNTIME_DATA_ROOT}
            --folder ${CMAKE_SOURCE_DIR}/assets
    )
    set_tests_properties(qml_runtime_smoke PROPERTIES
        DEPENDS qml_runtime_data_prepare
        ENVIRONMENT "QT_QPA_PLATFORM=offscreen"
        TIMEOUT 20
    )
    add_test(
        NAME qml_runtime_data_contract
        COMMAND ${CMAKE_COMMAND}
            -DROOT=${PICLENS_RUNTIME_DATA_ROOT}
            -P ${CMAKE_CURRENT_SOURCE_DIR}/../../tests/app/check_runtime_data.cmake
    )
    set_tests_properties(qml_runtime_data_contract PROPERTIES
        DEPENDS qml_runtime_smoke
    )
endif()
