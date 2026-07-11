qt_add_executable(piclens_core_tests
    tst_core_domain.cpp
)

target_link_libraries(piclens_core_tests
    PRIVATE
        PicLens::Core
        Qt6::Test
)

if(MSVC)
    target_compile_options(piclens_core_tests PRIVATE /W4 /permissive-)
else()
    target_compile_options(piclens_core_tests PRIVATE -Wall -Wextra -Wpedantic)
endif()

add_test(NAME piclens_core_tests COMMAND piclens_core_tests)
set_tests_properties(piclens_core_tests PROPERTIES LABELS "core;parity")
