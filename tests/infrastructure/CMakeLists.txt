piclens_add_qt_test(piclens_infrastructure_tests
    SOURCES
        tst_persistence.cpp
    LIBRARIES
        PicLens::Infrastructure
    LABELS
        infrastructure
        unit
        persistence
)

piclens_add_qt_test(piclens_folder_scanner_tests
    SOURCES
        tst_folder_scanner.cpp
    LIBRARIES
        PicLens::Infrastructure
    LABELS
        infrastructure
        unit
        scanner
)

piclens_add_qt_test(piclens_file_operation_tests
    SOURCES
        tst_file_operations.cpp
    LIBRARIES
        PicLens::Infrastructure
    LABELS
        infrastructure
        unit
        file-operations
)

qt_add_executable(piclens_platform_file_manager_smoke
    platform_file_manager_smoke.cpp
)
target_link_libraries(
    piclens_platform_file_manager_smoke
    PRIVATE
        PicLens::Infrastructure
        Qt6::Core
)
set_target_properties(
    piclens_platform_file_manager_smoke
    PROPERTIES WIN32_EXECUTABLE FALSE
)
piclens_enable_warnings(piclens_platform_file_manager_smoke)

piclens_add_qt_test(piclens_thumbnail_tests
    SOURCES
        tst_thumbnail_service.cpp
    LIBRARIES
        PicLens::Infrastructure
    LABELS
        infrastructure
        unit
        thumbnail
)
