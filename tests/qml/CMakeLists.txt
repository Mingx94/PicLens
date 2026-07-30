qt_add_executable(piclens_qml_tests
    tst_qml_main.cpp
)

target_link_libraries(
    piclens_qml_tests
    PRIVATE
        PicLens::App
        piclens_qml
        piclens_qmlplugin
        Qt6::Qml
        Qt6::QuickControls2
        Qt6::QuickTest
)
set_target_properties(piclens_qml_tests PROPERTIES WIN32_EXECUTABLE FALSE)
piclens_enable_warnings(piclens_qml_tests)
target_compile_definitions(
    piclens_qml_tests
    PRIVATE QUICK_TEST_SOURCE_DIR="${CMAKE_CURRENT_SOURCE_DIR}"
)

add_test(
    NAME qml_components
    COMMAND piclens_qml_tests
)
set_tests_properties(qml_components PROPERTIES
    ENVIRONMENT "QT_QPA_PLATFORM=offscreen"
    LABELS "qml;unit;gui"
    TIMEOUT 20
)
