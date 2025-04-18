# -*- coding: utf-8 -*-
import platform
from dataclasses import dataclass, field
from typing import Literal, Optional

@dataclass
class CSharpProjectConfig:
    name: str = field()
    csprojPath: str = field()
    buildCommand: Literal["build", "publish", "test"] = field(default="build")
    buildType: Literal["Debug", "Release"] = field(default="Debug")
    needRun: bool = field(default=False)

    # Test Config
    testFilter: Optional[str] = field(default=None)
    coverlet: bool = field(default=False)

    # Misc Config (can be automatically obtained)
    _system: Optional[str] = field(default=None)

    @property
    def System(self) -> str:
        return platform.system() if self._system is None else self._system

CSharpConfigs = {
    "YALuaToy": CSharpProjectConfig("YALuaToy", "YALuaToy/YALuaToy.csproj", buildCommand="publish", needRun=False),
    "Playground": CSharpProjectConfig("Playground", "Playground/Playground.csproj", buildCommand="build", needRun=True),
    "YALuaToy.Tests": CSharpProjectConfig("YALuaToy.Tests", "YALuaToy.Tests/YALuaToy.Tests.csproj", buildCommand="test", needRun=False, buildType="Release", coverlet=True), # Test 项目用 Release 版本测试
    "YALuaToy.Interpreter": CSharpProjectConfig("YALuaToy.Interpreter", "YALuaToy.Interpreter/YALuaToy.Interpreter.csproj", buildCommand="publish", buildType="Release", needRun=False),
}

@dataclass
class CppProjectConfig:
    name: str = field()
    cmakeDir: str = field()
    targetName: str = field(default="Run")
    buildType: Literal["Debug", "Release"] = field(default="Debug")
    needRun: bool = field(default=False)

CppConfigs = {
    "CLua": CppProjectConfig("CLua", "CLua", needRun=False),
    "CppPlayground": CppProjectConfig("CppPlayground", "CppPlayground", needRun=True),
}

@dataclass
class AntlrProjectConfig:
    name: str = field()
    g4Path: str = field()
    outputDir: str = field()
    package: str = field()
    grammarType: Literal["lexer", "parser"] = field()
    template: str = field(default="")

AntlrConfigs = {
    "YALuaToyLexer": AntlrProjectConfig(
        "YALuaToyLexer", "YALuaToy/Assets/LuaLexer.g4", "YALuaToy/Compilation/Generated_Antlr",
        "YALuaToy.Compilation.Antlr", "lexer"),
    "YALuaToyParser": AntlrProjectConfig(
        "YALuaToyParser", "YALuaToy/Assets/LuaParser.g4", "YALuaToy/Compilation/Generated_Antlr",
        "YALuaToy.Compilation.Antlr", "parser"),
}
