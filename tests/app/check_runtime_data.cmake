if(NOT DEFINED ROOT OR ROOT STREQUAL "")
    message(FATAL_ERROR "ROOT is required")
endif()

foreach(required_path IN ITEMS
    "${ROOT}/piclens-settings.json"
    "${ROOT}/Thumbnails"
)
    if(NOT EXISTS "${required_path}")
        message(FATAL_ERROR "Qt runtime data contract path was not created: ${required_path}")
    endif()
endforeach()

file(GLOB root_entries RELATIVE "${ROOT}" "${ROOT}/*")
foreach(required_entry IN ITEMS "piclens-settings.json" "Thumbnails")
    list(FIND root_entries "${required_entry}" entry_index)
    if(entry_index EQUAL -1)
        message(FATAL_ERROR "Qt runtime data contract entry has incorrect casing: ${required_entry}")
    endif()
endforeach()

foreach(obsolete_entry IN ITEMS "settings.json" "piclens.log" "thumbnails")
    list(FIND root_entries "${obsolete_entry}" entry_index)
    if(NOT entry_index EQUAL -1)
        message(FATAL_ERROR "Obsolete diagnostic-only data entry was created: ${obsolete_entry}")
    endif()
endforeach()
