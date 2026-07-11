qt_add_executable(piclens_qml_tests
    tst_qml_main.cpp
)

target_link_libraries(piclens_qml_tests PRIVATE Qt6::QuickControls2 Qt6::QuickTest)
set_target_properties(piclens_qml_tests PROPERTIES WIN32_EXECUTABLE FALSE)
target_compile_definitions(
    piclens_qml_tests
    PRIVATE QUICK_TEST_SOURCE_DIR="${CMAKE_CURRENT_SOURCE_DIR}"
)
add_dependencies(piclens_qml_tests piclens)

set(PICLENS_TEST_IMPORT_DIR "${CMAKE_CURRENT_BINARY_DIR}/imports/PicLens")
file(MAKE_DIRECTORY "${PICLENS_TEST_IMPORT_DIR}")
configure_file(
    "${CMAKE_CURRENT_SOURCE_DIR}/../../qml/PicLens/Theme.qml"
    "${PICLENS_TEST_IMPORT_DIR}/Theme.qml"
    COPYONLY
)
configure_file(
    "${CMAKE_CURRENT_SOURCE_DIR}/../../qml/PicLens/ToolbarButton.qml"
    "${PICLENS_TEST_IMPORT_DIR}/ToolbarButton.qml"
    COPYONLY
)
configure_file(
    "${CMAKE_CURRENT_SOURCE_DIR}/../../qml/PicLens/HistoryMouseHandler.qml"
    "${PICLENS_TEST_IMPORT_DIR}/HistoryMouseHandler.qml"
    COPYONLY
)
file(WRITE "${PICLENS_TEST_IMPORT_DIR}/qmldir"
    "module PicLens\nsingleton Theme 1.0 Theme.qml\nToolbarButton 1.0 ToolbarButton.qml\nHistoryMouseHandler 1.0 HistoryMouseHandler.qml\n"
)

add_test(
    NAME qml_components
    COMMAND piclens_qml_tests
        -input ${CMAKE_CURRENT_SOURCE_DIR}
        -import ${CMAKE_CURRENT_BINARY_DIR}/imports
)
set_tests_properties(qml_components PROPERTIES
    ENVIRONMENT "QT_QPA_PLATFORM=offscreen"
    TIMEOUT 20
)
