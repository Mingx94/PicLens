qt_add_library(piclens_core STATIC
    include/piclens/core/drag_interaction_rules.h
    include/piclens/core/file_rename_planner.h
    include/piclens/core/image_format_rules.h
    include/piclens/core/list_item_sorter.h
    include/piclens/core/models.h
    include/piclens/core/path_rules.h
    include/piclens/core/settings_rules.h
    include/piclens/core/zoom_math.h
    src/drag_interaction_rules.cpp
    src/file_rename_planner.cpp
    src/image_format_rules.cpp
    src/list_item_sorter.cpp
    src/models.cpp
    src/path_rules.cpp
    src/settings_rules.cpp
    src/zoom_math.cpp
)

add_library(PicLens::Core ALIAS piclens_core)

target_include_directories(piclens_core
    PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}/include
)

target_link_libraries(piclens_core PUBLIC Qt6::Core)

if(MSVC)
    target_compile_options(piclens_core PRIVATE /W4 /permissive-)
else()
    target_compile_options(piclens_core PRIVATE -Wall -Wextra -Wpedantic)
endif()
