cmake_minimum_required(VERSION 3.10)
project(CppPlayground)

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

add_subdirectory("${CWD}/CLua" "${CMAKE_BINARY_DIR}/CLua")
include_directories("${CWD}/CLua")

file(GLOB CPP_PLAYGROUND_SRC "${CMAKE_CURRENT_SOURCE_DIR}/*.cpp")
message("CPP_PLAYGROUND_SRC: ${CPP_PLAYGROUND_SRC}")

# -------------------- Target -------------------- #

add_executable(Run ${CPP_PLAYGROUND_SRC})
target_link_libraries(Run
    PRIVATE lua536
)

