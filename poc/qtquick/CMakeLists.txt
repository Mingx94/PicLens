cmake_minimum_required(VERSION 3.21)

project(PicLensQtQuickPoc VERSION 0.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

find_package(Qt6 6.5 REQUIRED COMPONENTS Concurrent Quick QuickControls2)

qt_standard_project_setup(REQUIRES 6.5)

qt_add_executable(picLensQtQuickPoc
    src/main.cpp
    src/imagemodel.cpp
    src/imagemodel.h
    src/thumbnailprovider.cpp
    src/thumbnailprovider.h
)

set_source_files_properties(qml/Main.qml PROPERTIES QT_RESOURCE_ALIAS Main.qml)

qt_add_qml_module(picLensQtQuickPoc
    URI PicLens.Poc
    VERSION 1.0
    QML_FILES
        qml/Main.qml
)

target_link_libraries(picLensQtQuickPoc
    PRIVATE
        Qt6::Concurrent
        Qt6::Quick
        Qt6::QuickControls2
)

if(WIN32)
    set_target_properties(picLensQtQuickPoc PROPERTIES WIN32_EXECUTABLE TRUE)
endif()
