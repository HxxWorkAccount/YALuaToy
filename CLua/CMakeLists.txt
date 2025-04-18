cmake_minimum_required(VERSION 3.10)
project(CLua)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_C_STANDARD 11)

set(CMAKE_RUNTIME_OUTPUT_DIRECTORY_DEBUG "${CMAKE_BINARY_DIR}/Debug/bin")
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/Release/bin")
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY_DEBUG "${CMAKE_BINARY_DIR}/Debug/lib")
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/Release/lib")
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY_DEBUG "${CMAKE_BINARY_DIR}/Debug/lib")
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/Release/lib")

if(WIN32)
    add_definitions(-DLUA_BUILD_AS_DLL)
else()
    add_compile_options(-fdeclspec)
    # if (APPLE)
    #     set(CMAKE_OSX_ARCHITECTURES "arm64")
    # else()  # UNIX
    # endif()
endif()

# -------------------- Source -------------------- #

# Lua536 Interpreter
file(GLOB LUA_INTERP_SRC "${CMAKE_CURRENT_SOURCE_DIR}/lua536/lua.c")
set_source_files_properties(${LUA_INTERP_SRC} PROPERTIES LANGUAGE C)
message("Lua Interpreter Source: ${LUA_INTERP_SRC}")

# Luac
file(GLOB LUAC_SRC "${CMAKE_CURRENT_SOURCE_DIR}/lua536/luac.c")
set_source_files_properties(${LUAC_SRC} PROPERTIES LANGUAGE C)
message("Luac Source: ${LUAC_SRC}")

# Lua536 Library
include_directories("${CMAKE_CURRENT_SOURCE_DIR}/lua536")
file(GLOB LUA_LIB_SRC "${CMAKE_CURRENT_SOURCE_DIR}/lua536/*.c")
list(REMOVE_ITEM LUA_LIB_SRC ${LUA_INTERP_SRC} ${LUAC_SRC})
set_source_files_properties(${LUA_LIB_SRC} PROPERTIES LANGUAGE C)
# message("Lua Library Source: ${LUA_LIB_SRC}")

# Lua536 Lexer Test
file(GLOB DUMP_CLUA_LEXER_SRC "${CMAKE_CURRENT_SOURCE_DIR}/CLuaLexerDumper.cpp")
set_source_files_properties(${DUMP_CLUA_LEXER_SRC} PROPERTIES LANGUAGE CXX)
message("Lua Lexer Dumper Source: ${DUMP_CLUA_LEXER_SRC}")

# Integrate the Standard Libraries of CLua with C#Lua

# set(YALUATOY_DIR "${CWD}/out")
# find_library(YALuaToy
#     NAMES "YALuaToy"
#     PATHS ${YALUATOY_DIR}
# )
# if(NOT YALuaToy)
#   message(FATAL_ERROR "YALuaToy library not found! Search dir: ${YALUATOY_DIR}")
# else()
#   message(STATUS "Found YALuaToy library: ${YALuaToy}")
# endif()
# # Lua Lib
# add_library(lua536 SHARED ${CLUA_LIB_SRC})
# target_link_libraries(lua536
#     PRIVATE ${YALuaToy}
# )

# -------------------- Targets -------------------- #

# Lua536 Library
add_definitions(-DLUA_USE_APICHECK)
add_library(lua536 SHARED ${LUA_LIB_SRC})
add_library(lua536s STATIC ${LUA_LIB_SRC})

# Lua536 Interpreter
add_executable(lua ${LUA_INTERP_SRC})
target_link_libraries(lua lua536)

# Luac
add_executable(luac ${LUAC_SRC})
target_link_libraries(luac lua536s)

# Lua Lexer Dump
add_executable(dumplexer ${DUMP_CLUA_LEXER_SRC})
target_link_libraries(dumplexer lua536s)

# -------------------- Install -------------------- #

install(TARGETS lua
    RUNTIME DESTINATION ${CWD}/out/CLua
    LIBRARY DESTINATION ${CWD}/out/CLua
)
