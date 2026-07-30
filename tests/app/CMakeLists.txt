piclens_add_qt_test(piclens_app_tests
    SOURCES
        tst_app_controller.cpp
    LIBRARIES
        PicLens::App
    LABELS
        app
        integration
)

set(PICLENS_RUNTIME_DATA_ROOT "${CMAKE_BINARY_DIR}/Testing/runtime-data")
add_test(
    NAME qml_runtime_data_prepare
    COMMAND ${CMAKE_COMMAND} -E rm -rf ${PICLENS_RUNTIME_DATA_ROOT}
)
set_tests_properties(
    qml_runtime_data_prepare
    PROPERTIES LABELS "app;runtime"
)

add_test(
    NAME qml_runtime_smoke
    COMMAND piclens
        --smoke-ms 750
        --data-root ${PICLENS_RUNTIME_DATA_ROOT}
        --folder ${CMAKE_SOURCE_DIR}/assets
)
set_tests_properties(
    qml_runtime_smoke
    PROPERTIES
        DEPENDS qml_runtime_data_prepare
        ENVIRONMENT "QT_QPA_PLATFORM=offscreen"
        LABELS "app;runtime;smoke"
        TIMEOUT 20
)

add_test(
    NAME qml_runtime_data_contract
    COMMAND ${CMAKE_COMMAND}
        -DROOT=${PICLENS_RUNTIME_DATA_ROOT}
        -P ${CMAKE_CURRENT_SOURCE_DIR}/check_runtime_data.cmake
)
set_tests_properties(
    qml_runtime_data_contract
    PROPERTIES
        DEPENDS qml_runtime_smoke
        LABELS "app;runtime"
)
