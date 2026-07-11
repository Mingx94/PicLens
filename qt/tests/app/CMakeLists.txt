qt_add_executable(piclens_app_tests
    tst_app_controller.cpp
)

target_link_libraries(piclens_app_tests
    PRIVATE
        PicLens::App
        Qt6::Test
)

if(MSVC)
    target_compile_options(piclens_app_tests PRIVATE /W4 /permissive-)
else()
    target_compile_options(piclens_app_tests PRIVATE -Wall -Wextra -Wpedantic)
endif()

add_test(NAME piclens_app_tests COMMAND piclens_app_tests)
set_tests_properties(piclens_app_tests PROPERTIES LABELS "app;presentation;parity")
