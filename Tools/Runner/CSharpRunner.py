# -*- coding: utf-8 -*-
import os
import sys
from copy import copy
from Const.ProjectConfig import CSharpProjectConfig, CSharpConfigs
from Runner.RunnerBase import RunnerBase
from Builder.BuilderBase import BuildResult
from Builder.CSharpBuilder import CSharpBuilder
from Utils import CommandUtils, RunnerUtils, PathUtils

class CSharpRunner(RunnerBase):
    BUILDER_CLASS = CSharpBuilder

    @property
    def NeedRun(self) -> bool:
        return self.projectConfig.needRun

    def DoRun(self, buildResult: BuildResult):
        assert(isinstance(self.projectConfig, CSharpProjectConfig))
        command = ["dotnet", "run", "--project", self.projectConfig.csprojPath, "--"]
        command.extend(self.args)
        CommandUtils.TryExecuteCommand(command)

    def AfterRun(self, ran: bool):
        assert(isinstance(self.projectConfig, CSharpProjectConfig))
        if self.projectConfig.coverlet: # 生成覆盖率报告
            command = [
                "reportgenerator",
                f"-reports:{self.projectConfig.name}/out/coverage.cobertura.xml",
                f"-targetdir:{self.projectConfig.name}/out/html",
                "-reporttypes:Html"
            ]
            CommandUtils.TryExecuteCommand(command)
        if self.projectConfig.buildCommand == "publish": # 如果是 publish，则交叉编译一下 macOS 和 windows
            if self.projectConfig.System != "Windows":
                self._CrossCompileHelper("Windows")
            if self.projectConfig.System != "Darwin":
                self._CrossCompileHelper("Darwin")

    @staticmethod
    def GetConfigByPath(filepath: str) -> CSharpProjectConfig:
        for projectConfig in CSharpConfigs.values():
            if not PathUtils.SameRoot(filepath, os.path.dirname(projectConfig.csprojPath)):
                continue
            if projectConfig.name == "YALuaToy.Tests" and not filepath.endswith(".csproj"):
                testConfig = copy(projectConfig)
                testClassName = os.path.splitext(os.path.basename(filepath))[0]
                testConfig.testFilter = f"FullyQualifiedName~{testClassName}"
                return testConfig
            return projectConfig
        raise Exception("Unknown C# project.")

    def _CrossCompileHelper(self, system: str):
        projectConfig = copy(self.projectConfig)
        projectConfig._system = system
        buildResult = self.BUILDER_CLASS(projectConfig).TryBuild()
        if (buildResult.Failed):
            print(f"Cross compile failed, target platform: {projectConfig._system}, result: {buildResult.result}")


if __name__ == "__main__":
    RunnerUtils.Launch(CSharpRunner)
