qt_add_executable(piclens_infrastructure_tests
    tst_persistence.cpp
)

target_link_libraries(piclens_infrastructure_tests
    PRIVATE
        PicLens::Infrastructure
        Qt6::Test
)

if(MSVC)
    target_compile_options(piclens_infrastructure_tests PRIVATE /W4 /permissive-)
else()
    target_compile_options(piclens_infrastructure_tests PRIVATE -Wall -Wextra -Wpedantic)
endif()

add_test(NAME piclens_infrastructure_tests COMMAND piclens_infrastructure_tests)
set_tests_properties(piclens_infrastructure_tests PROPERTIES LABELS "infrastructure;parity")

qt_add_executable(piclens_folder_scanner_tests
    tst_folder_scanner.cpp
)

target_link_libraries(piclens_folder_scanner_tests
    PRIVATE
        PicLens::Infrastructure
        Qt6::Test
)

if(MSVC)
    target_compile_options(piclens_folder_scanner_tests PRIVATE /W4 /permissive-)
else()
    target_compile_options(piclens_folder_scanner_tests PRIVATE -Wall -Wextra -Wpedantic)
endif()

add_test(NAME piclens_folder_scanner_tests COMMAND piclens_folder_scanner_tests)
set_tests_properties(piclens_folder_scanner_tests PROPERTIES LABELS "infrastructure;scanner;parity")

qt_add_executable(piclens_file_operation_tests
    tst_file_operations.cpp
)

target_link_libraries(piclens_file_operation_tests
    PRIVATE
        PicLens::Infrastructure
        Qt6::Test
)

if(MSVC)
    target_compile_options(piclens_file_operation_tests PRIVATE /W4 /permissive-)
else()
    target_compile_options(piclens_file_operation_tests PRIVATE -Wall -Wextra -Wpedantic)
endif()

add_test(NAME piclens_file_operation_tests COMMAND piclens_file_operation_tests)
set_tests_properties(piclens_file_operation_tests PROPERTIES LABELS "infrastructure;file-operations;parity")

qt_add_executable(piclens_platform_file_manager_smoke
    platform_file_manager_smoke.cpp
)
target_link_libraries(piclens_platform_file_manager_smoke PRIVATE PicLens::Infrastructure Qt6::Core)
set_target_properties(piclens_platform_file_manager_smoke PROPERTIES WIN32_EXECUTABLE FALSE)

if(MSVC)
    target_compile_options(piclens_platform_file_manager_smoke PRIVATE /W4 /permissive-)
else()
    target_compile_options(piclens_platform_file_manager_smoke PRIVATE -Wall -Wextra -Wpedantic)
endif()

qt_add_executable(piclens_thumbnail_tests
    tst_thumbnail_service.cpp
)

target_link_libraries(piclens_thumbnail_tests
    PRIVATE
        PicLens::Infrastructure
        Qt6::Test
)

if(MSVC)
    target_compile_options(piclens_thumbnail_tests PRIVATE /W4 /permissive-)
else()
    target_compile_options(piclens_thumbnail_tests PRIVATE -Wall -Wextra -Wpedantic)
endif()

add_test(NAME piclens_thumbnail_tests COMMAND piclens_thumbnail_tests)
set_tests_properties(piclens_thumbnail_tests PROPERTIES LABELS "infrastructure;thumbnail;parity")
