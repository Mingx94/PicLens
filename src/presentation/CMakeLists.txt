qt_add_library(piclens_presentation STATIC
    include/piclens/presentation/file_operation_controller.h
    include/piclens/presentation/folder_tree_model.h
    include/piclens/presentation/library_controller.h
    include/piclens/presentation/library_item_model.h
    include/piclens/presentation/thumbnail_coordinator.h
    include/piclens/presentation/viewer_controller.h
    src/file_operation_controller.cpp
    src/folder_tree_model.cpp
    src/library_controller.cpp
    src/library_item_model.cpp
    src/thumbnail_coordinator.cpp
    src/viewer_controller.cpp
)

add_library(PicLens::Presentation ALIAS piclens_presentation)

target_include_directories(piclens_presentation
    PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}/include
)

target_link_libraries(piclens_presentation
    PUBLIC
        PicLens::Core
        Qt6::Core
    PRIVATE
        Qt6::Concurrent
)

if(MSVC)
    target_compile_options(piclens_presentation PRIVATE /W4 /permissive-)
else()
    target_compile_options(piclens_presentation PRIVATE -Wall -Wextra -Wpedantic)
endif()
