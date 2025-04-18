# README

## About

YALuaToy (full name: Yet Another Lua Toy) is a learning project aimed at deepening the understanding of Lua Virtual Machine principles. This project re-implements both the CLua core and frontend in C#, maintaining architectural consistency while leveraging modern language features to improve code readability and maintainability.

This project has been very helpful for my own understanding of Lua. It can also serve as a reference for others interested in how Lua is implemented. Its advantages include:
- Compared to the CLua implementation, it features a better type system and naming conventions.
- It has more comments.
- The frontend is almost identical, while the core has some differences.

**Notes**:
1. This repository is a snapshot version; pull requests are not accepted.
1. Maintenance updates might be slow, but I would greatly appreciate any issues raised.

## Features

### Language-Related
- A nearly complete Lua virtual machine implementation.
- Full support for the standard libraries, except for `collectgarbage`.
- Supports requiring Lua files (however, dynamic library loading is not supported; only static linking is available).
- Complete coroutine support.
- A frontend nearly identical to CLua (including parser and code generation, with rigorous testing); the only known difference is that it does not support illegal UTF-8 escapes.
- Parser implemented with ANTLR4, offering more precise line number information.

### Development-Related
- A strict type system.
- Full support for C# exceptions:
  - Supports native `try-catch` statements and LuaState's `PCall`. When using `throw` to raise exceptions, there is no need to pre-push the error object onto the stack.
  - If you prefer to throw an exception using the top-of-stack object, you can use `state.Error`.
- Direct use of C# garbage collection for memory management, which simplifies interaction with the Lua core and allows sharing a unified exception system.

### Other
- Comprehensive unit tests (80% coverage overall, with key core components reaching 90%).
- A simple interpreter that supports two modes:
  - File execution mode.
  - Interactive REPL.

## Problems
- **Does not support illegal UTF-8 strings or binary data.** YALuaToy uses C# UTF-16 strings; deviating from this makes interactions with C# cumbersome (and may also cause performance issues).
- Tables are implemented using C# Dictionary, which might not offer the best performance for certain cases involving the `Next` interface.

## Todo

### Short-Term
- [ ] Implement dump/undump functionality (a simple dump is implemented in YALuaToy.Tests for testing purposes).
- [ ] Support a Debug module, as runtime information reflection is currently not available.
- [ ] Support hooks.
- [ ] Provide FFI components for C# and Lua.
- [ ] Support dynamic library loading.

### Long-Term
- [ ] Export an unmanaged API (compatible with lua.h/lauxlib.h), so that other Lua libraries (including the official standard libraries) can be reused.

  I have analyzed that exporting an unmanaged API does not pose significant technical challenges; the main issues are performance-related (e.g., maintaining string representations on both managed and unmanaged heaps).

- [ ] After implementing the unmanaged API, support UserData (currently, this is not necessary; I don't understand why UserData would be needed on the C# hosting side since the same GC is used).
- [ ] Provide complete support for the standard libraries (possibly by directly reusing the official implementation).

## Build

### Dependencies
Note: The versions listed below are the ones I use; lower versions are also highly likely to work:
- .NET 9.0
- CMake 3.10
- Python 3.7
- Java 23

### Build Steps
The build process is very straightforward. Simply navigate to the project directory and run the following command:
```bash
python Tools/RunPy.py Tools/Project/BuildInterpreter.py
```

### Execution

Scripts in Assets/Lua.misc.lua and under Assets/Lua/Tests can be executed. Assets/Lua/Examples provides an object-oriented implementation of Lua.

#### Windows
```powershell
# Launch interactive mode
./out/YALuaToy.Interpreter/Windows/YALuaToy.Interpreter.exe

# Execute a lua script
./out/YALuaToy.Interpreter/Windows/YALuaToy.Interpreter.exe Assets/Lua/misc.lua
```

#### macOS
```bash
# Launch interactive mode
./out/YALuaToy.Interpreter/Darwin/YALuaToy.Interpreter

# Execute a lua script
./out/YALuaToy.Interpreter/Darwin/YALuaToy.Interpreter Assets/Lua/misc.lua
```

## Tests

The unit testing project is YALuaToy.Tests, with an overall coverage of 80% and up to 90% for the critical sections of the core. It also supports generating test reports.

Need to compile CLua first (for comparative testing of results):
```bash
python -u Tools/RunPy.py Tools/Runner/CppRunner.py CLua/CMakeLists.txt
```

Run the tests using:
```bash
python -u Tools/RunPy.py Tools/Runner/CSharpRunner.py "YALuaToy.Tests/YALuaToy.Tests.csproj"
```
