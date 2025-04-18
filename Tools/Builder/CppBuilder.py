# -*- coding: utf-8 -*-
import sys
import os
from Builder.CSharpBuilder import CSharpBuilder
from Const.ProjectConfig import CppProjectConfig, CSharpConfigs
from Builder.BuilderBase import BuildResult, BuilderBase
from Utils import RunnerUtils, CommandUtils, PathUtils

class CppBuilder(BuilderBase):

    def BeforeBuild(self):
        assert isinstance(self.projectConfig, CppProjectConfig)
        # if self.projectConfig.name in ("CLua", "CppPlayground"):  # 暂时不做 CAPI 支持了
        #     builder = CSharpBuilder(CSharpConfigs["YALuaToy"])
        #     builder.Build()

    def DoBuild(self) -> BuildResult:
        assert isinstance(self.projectConfig, CppProjectConfig)
        cmakeDir = self.projectConfig.cmakeDir
        targetName = self.projectConfig.targetName
        buildType = self.projectConfig.buildType

        outputDir = os.path.join(cmakeDir, "build")
        # path to the target executable
        rawTargetPath = os.path.join(outputDir, buildType, "bin", targetName)

        PathUtils.CheckDir(outputDir, True)

        CommandUtils.ExecuteCommand([
            "cmake",
            "-B", outputDir,
            "-S", cmakeDir,
            f"-DCWD={os.path.abspath('.')}",
            f"-DCMAKE_BUILD_TYPE={buildType}",
            "-DCMAKE_EXPORT_COMPILE_COMMANDS=ON"
        ], errorHint="Failed to configure CMake project.")
        CommandUtils.ExecuteCommand([
            "cmake",
            "--build", outputDir,
            "--config", buildType,
            "-j", "10"
        ])
        return BuildResult(0, rawTargetPath)
