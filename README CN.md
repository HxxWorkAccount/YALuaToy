# README

## About

YALuaToy（全称：Yet Another Lua Toy）是一个旨在深入理解 Lua 虚拟机原理的**学习**项目。本项目使用 C# 语言对 CLua 内核和前端进行了重新实现，在保持架构一致性的同时，通过现代编程语言的特性提升了代码的可读性和可维护性。

本项目对帮助我理解 Lua 原理有非常大的帮助，如果有其他人没有了解但想了解 Lua 的实现原理，也可以参考本项目。有以下好处：
- 相比 CLua 实现，具有更好的类型系统和命名规范
- 有更多的注释
- 前端一致程度相当高，内核则有一定区别

**注意事项**：
1. 本仓库为代码快照版本，暂不接受 PR（无法合并）
1. 维护更新可能较慢，但如果有人愿意 issue 我会十分感激

## Feature

### 语言相关
- 相对完整的 Lua 虚拟机实现
- 完整支持基础库，除了 `collectgarbage`
- 支持 require lua 文件（但不支持加载动态连接库，需要静态连接）
- 协程全功能支持
- 和 CLua 几乎完全一样的前端（包括解析和代码生成，有非常高强度的测试），目前已知唯一的区别是不支持非法 utf8 转义
- 使用 ANTLR4 实现解析器，有更精准的行号信息

### 开发相关
- 严格的类型系统
- 完全支持 C# 异常
  - 支持原生 try-catch 语句，也可以使用 LuaState 的 PCall。如果使用 throw 抛出异常，不需要提前往栈里压入错误对象
  - 如果想使用栈顶对象抛出异常，也可以使用 state.Error 实现
- 直接使用 C# GC 的内存管理，这使得与 Lua 内核的交互非常轻松，而且**可以共用一套异常系统**

### 其他
- 完善的单元测试（覆盖率达到 80%，内核关键代码覆盖率有 90%）
- 简易解释器支持两种模式：
  - 文件执行模式
  - 交互式 REPL

## Problem
- **不支持非法 utf8 字符串，不支持二进制数据**。YALuaToy 的字符串使用了 C# 的 UTF-16 string，若不这样做，与 C# 交互会非常麻烦（而且也有性能问题）
- 表使用 C# Dictionary 实现，对于 Next 接口部分情况性能不太友好

## Todo

### Short-Term
- [ ] 实现 dump/undump 功能（在 YALuaToy.Tests 里实现了一个简易的 dump 用于测试）
- [ ] 支持 Debug 模块，目前无法反射运行信息
- [ ] 支持 hook
- [ ] C# 与 Lua 的 FII 组件
- [ ] 支持加载动态连接库

### Long-Term
- [ ] 导出非托管 API（兼容lua.h/lauxlib.h），这样就可以使用其他所有 Lua 库了（包括官方标准库）。

  我分析了一下，导出非托管 API 并没有大的技术困难，主要是性能问题（比如字符串，需要在托管堆和非托管堆重复维护）。

- [ ] 实现非托管 API 后，实现 UserData（目前没必要实现，我不理解为什么要在 C# 宿主侧使用 UserData（毕竟都是同一个 GC。。。）
- [ ] 完整标准库支持（可能直接复用官方实现）

## Build

### Dependencies
注意，遗下只是我使用的版本，我认为更低的版本也能使用：
- .NET 9.0
- CMake 3.10
- Python 3.7
- Java 23

### Build Steps
构建非常简单，直接 cd 到项目目录，然后运行该指令即可：
```bash
python Tools/RunPy.py Tools/Project/BuildInterpreter.py
```

### Execution

Assets/Lua.misc.lua 和 Assets/Lua/Tests 下的脚本都是可以执行的。Assets/Lua/Examples 里提供了一个 Lua 面向对象的实现。

#### Windows
```powershell
# 启动交互模式
./out/YALuaToy.Interpreter/Windows/YALuaToy.Interpreter.exe

# 执行脚本文件
./out/YALuaToy.Interpreter/Windows/YALuaToy.Interpreter.exe Assets/Lua/misc.lua
```

#### macOS
```bash
# 启动交互模式
./out/YALuaToy.Interpreter/Darwin/YALuaToy.Interpreter

# 执行脚本文件
./out/YALuaToy.Interpreter/Darwin/YALuaToy.Interpreter Assets/Lua/misc.lua
```

## Tests

单元测试项目是 YALuaToy.Tests，测试覆盖率达到 80%，支持输出测试报告。

需要先编译 CLua（用于对比测试结果）：
```bash
python -u Tools/RunPy.py Tools/Runner/CppRunner.py CLua/CMakeLists.txt
```

启动命令：
```bash
python -u Tools/RunPy.py Tools/Runner/CSharpRunner.py "YALuaToy.Tests/YALuaToy.Tests.csproj"
```
