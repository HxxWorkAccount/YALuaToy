
# (deprecated)
function(CommonUtils_SetRPath target)
    if (APPLE)
        set_target_properties(${target} PROPERTIES
            BUILD_WITH_INSTALL_RPATH TRUE
            INSTALL_RPATH "@loader_path"
            INSTALL_NAME_DIR "@loader_path"
        )
    elseif (UNIX)
        set_target_properties(lua53 PROPERTIES
            BUILD_RPATH "$ORIGIN"
            INSTALL_RPATH "$ORIGIN"
        )
    endif()
endfunction()
