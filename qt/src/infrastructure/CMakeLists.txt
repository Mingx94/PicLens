qt_add_library(piclens_infrastructure STATIC
    include/piclens/infrastructure/app_data_paths.h
    include/piclens/infrastructure/file_operation_service.h
    include/piclens/infrastructure/file_app_logger.h
    include/piclens/infrastructure/folder_scanner.h
    include/piclens/infrastructure/json_settings_store.h
    include/piclens/infrastructure/platform_file_manager.h
    include/piclens/infrastructure/thumbnail_service.h
    src/app_data_paths.cpp
    src/file_app_logger.cpp
    src/file_operation_service.cpp
    src/folder_scanner.cpp
    src/json_settings_store.cpp
    src/platform_file_manager.cpp
    src/thumbnail_service.cpp
)

if(WIN32)
    target_link_libraries(piclens_infrastructure PRIVATE shell32)
endif()

add_library(PicLens::Infrastructure ALIAS piclens_infrastructure)

target_include_directories(piclens_infrastructure
    PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}/include
)

target_link_libraries(piclens_infrastructure
    PUBLIC
        PicLens::Core
        Qt6::Core
        Qt6::Gui
)

if(MSVC)
    target_compile_options(piclens_infrastructure PRIVATE /W4 /permissive-)
else()
    target_compile_options(piclens_infrastructure PRIVATE -Wall -Wextra -Wpedantic)
endif()
