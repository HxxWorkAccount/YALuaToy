# -*- coding: utf-8 -*-
import sys
import os
import platform
import shutil
from Const.ProjectConfig import CSharpProjectConfig, AntlrConfigs
from Builder.AntlrBuilder import AntlrBuilder
from Builder.BuilderBase import BuildResult, BuilderBase
from Utils import CommandUtils, PathUtils

NET_VERSION = "9.0"

class CSharpBuilder(BuilderBase):

    @property
    def Machine(self) -> str:
        assert(isinstance(self.projectConfig, CSharpProjectConfig))
        system = self.projectConfig.System
        if system == "Darwin":
            return "osx-arm64"
        elif system == "Windows":
            return "win-x64"
        else:
            raise Exception(f"Unknown platform: {system}")

    def BeforeBuild(self):
        # 不自动重编 Antlr Parser 了，解析器代码已经非常稳定
        # parserBuilder = AntlrBuilder(AntlrConfigs["YALuaToyParser"])
        # parserBuilder.Build()
        pass

    def DoBuild(self) -> BuildResult:
        assert(isinstance(self.projectConfig, CSharpProjectConfig))
        csprojPath = self.projectConfig.csprojPath
        buildCommand = self.projectConfig.buildCommand
        buildType = self.projectConfig.buildType

        if buildCommand == "build":
            CommandUtils.ExecuteCommand(["dotnet", "build", csprojPath, "-c", buildType])
        elif buildCommand == "publish":
            machine = self.Machine
            csprojDir = os.path.dirname(csprojPath)
            csprojName = os.path.splitext(os.path.basename(csprojPath))[0]

            aot = platform.system() == self.projectConfig.System

            # publish command
            commands = ["dotnet", "publish", csprojPath, "-c", buildType, "-r", machine]
            if (aot):
                commands.append("-p:PublishAot=true")
            CommandUtils.ExecuteCommand(commands)

            # install to out
            nativeDir = os.path.join(csprojDir, "bin", buildType, f"net{NET_VERSION}", machine, "native")
            publishDir = os.path.join(csprojDir, "bin", buildType, f"net{NET_VERSION}", machine, "publish")
            outputDir = os.path.join("out", self.projectConfig.name, self.projectConfig.System)

            # iterate publishDir files:
            # if self.projectConfig.System == "Darwin":
            #     for item in os.listdir(publishDir):
            #         if os.path.isdir(item) or not item.endswith(".dylib"):
            #             continue
            #         filepath = os.path.join(publishDir, item)
            #         CommandUtils.ExecuteCommand(
            #             ["install_name_tool", "-id", f"@rpath/{item}", filepath],
            #             errorHint=f"install_name_tool failed"
            #         )
            # elif self.projectConfig.System == "Windows":
            #     for item in os.listdir(publishDir):
            #         if not item.endswith(".lib"): continue
            #         itemPath = os.path.join(publishDir, item)
            #         shutil.copy(itemPath, outputDir)

            shutil.rmtree(outputDir, ignore_errors=True) # remove out dir
            PathUtils.CheckDir(outputDir, create=True) # create out dir
            if (aot):
                shutil.copytree(nativeDir, outputDir, dirs_exist_ok=True) # copy publish files to out
            else:
                shutil.copytree(publishDir, outputDir, dirs_exist_ok=True) # copy publish files to out
            return BuildResult(0, outputDir)
        elif buildCommand == "test":
            command = [
                "dotnet", "test", csprojPath,
                "-c", buildType,
                "--logger", "console;verbosity=normal",
            ]
            if self.projectConfig.coverlet:
                command.append("/p:CollectCoverage=true")
                command.append("/p:CoverletOutput=out/coverage")
                # command.append("/p:MergeWith=out/coverage.json")
                command.append("/p:CoverletOutputFormat=\"cobertura,json\"")
            if self.projectConfig.testFilter is not None:
                command.append("--filter")
                command.append(self.projectConfig.testFilter)
            CommandUtils.TryExecuteCommand(command)
        else:
            raise Exception("Unknown build command.")

        return BuildResult(0, csprojPath)

