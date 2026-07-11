qt_add_executable(piclens_presentation_tests
    tst_library_controller.cpp
)

target_link_libraries(piclens_presentation_tests
    PRIVATE
        PicLens::Presentation
        Qt6::Test
)

if(MSVC)
    target_compile_options(piclens_presentation_tests PRIVATE /W4 /permissive-)
else()
    target_compile_options(piclens_presentation_tests PRIVATE -Wall -Wextra -Wpedantic)
endif()

add_test(NAME piclens_presentation_tests COMMAND piclens_presentation_tests)
set_tests_properties(piclens_presentation_tests PROPERTIES LABELS "presentation;parity")

qt_add_executable(piclens_file_operation_controller_tests
    tst_file_operation_controller.cpp
)

target_link_libraries(piclens_file_operation_controller_tests
    PRIVATE
        PicLens::Presentation
        Qt6::Test
)

if(MSVC)
    target_compile_options(piclens_file_operation_controller_tests PRIVATE /W4 /permissive-)
else()
    target_compile_options(piclens_file_operation_controller_tests PRIVATE -Wall -Wextra -Wpedantic)
endif()

add_test(NAME piclens_file_operation_controller_tests COMMAND piclens_file_operation_controller_tests)
set_tests_properties(piclens_file_operation_controller_tests PROPERTIES LABELS "presentation;file-operations;parity")

qt_add_executable(piclens_folder_tree_tests
    tst_folder_tree_model.cpp
)

target_link_libraries(piclens_folder_tree_tests
    PRIVATE
        PicLens::Presentation
        Qt6::Test
)

if(MSVC)
    target_compile_options(piclens_folder_tree_tests PRIVATE /W4 /permissive-)
else()
    target_compile_options(piclens_folder_tree_tests PRIVATE -Wall -Wextra -Wpedantic)
endif()

add_test(NAME piclens_folder_tree_tests COMMAND piclens_folder_tree_tests)
set_tests_properties(piclens_folder_tree_tests PROPERTIES LABELS "presentation;folder-tree;parity")

qt_add_executable(piclens_thumbnail_coordinator_tests
    tst_thumbnail_coordinator.cpp
)

target_link_libraries(piclens_thumbnail_coordinator_tests
    PRIVATE
        PicLens::Presentation
        Qt6::Test
)

if(MSVC)
    target_compile_options(piclens_thumbnail_coordinator_tests PRIVATE /W4 /permissive-)
else()
    target_compile_options(piclens_thumbnail_coordinator_tests PRIVATE -Wall -Wextra -Wpedantic)
endif()

add_test(NAME piclens_thumbnail_coordinator_tests COMMAND piclens_thumbnail_coordinator_tests)
set_tests_properties(piclens_thumbnail_coordinator_tests PROPERTIES LABELS "presentation;thumbnail;parity")

qt_add_executable(piclens_viewer_controller_tests
    tst_viewer_controller.cpp
)
target_link_libraries(piclens_viewer_controller_tests PRIVATE PicLens::Presentation Qt6::Test)
if(MSVC)
    target_compile_options(piclens_viewer_controller_tests PRIVATE /W4 /permissive-)
else()
    target_compile_options(piclens_viewer_controller_tests PRIVATE -Wall -Wextra -Wpedantic)
endif()
add_test(NAME piclens_viewer_controller_tests COMMAND piclens_viewer_controller_tests)
set_tests_properties(piclens_viewer_controller_tests PROPERTIES LABELS "presentation;viewer;parity")
