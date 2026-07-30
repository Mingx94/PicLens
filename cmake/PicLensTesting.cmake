include(CMakeParseArguments)

function(piclens_add_qt_test target)
    cmake_parse_arguments(
        ARG
        ""
        "TIMEOUT"
        "SOURCES;LIBRARIES;LABELS"
        ${ARGN}
    )

    if(NOT ARG_SOURCES)
        message(FATAL_ERROR "piclens_add_qt_test(${target}) requires SOURCES")
    endif()

    qt_add_executable(${target} ${ARG_SOURCES})
    target_link_libraries(${target} PRIVATE ${ARG_LIBRARIES} Qt6::Test)
    piclens_enable_warnings(${target})

    add_test(NAME ${target} COMMAND ${target})
    if(ARG_LABELS)
        set_tests_properties(${target} PROPERTIES LABELS "${ARG_LABELS}")
    endif()
    if(ARG_TIMEOUT)
        set_tests_properties(${target} PROPERTIES TIMEOUT "${ARG_TIMEOUT}")
    endif()
endfunction()
