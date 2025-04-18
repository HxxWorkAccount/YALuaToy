# -*- coding: utf-8 -*-
import os
import sys
from copy import copy
from Const.ProjectConfig import AntlrProjectConfig, AntlrConfigs
from RunnerBase import RunnerBase
from Builder.BuilderBase import BuildResult
from Builder.AntlrBuilder import AntlrBuilder
from Utils import CommandUtils, RunnerUtils, PathUtils

class AntlrRunner(RunnerBase):
    BUILDER_CLASS = AntlrBuilder

    @property
    def NeedRun(self) -> bool:
        return False

    @staticmethod
    def GetConfigByPath(filepath: str) -> AntlrProjectConfig:
        for projectConfig in AntlrConfigs.values():
            if os.path.samefile(filepath, projectConfig.g4Path):
                return projectConfig
        for projectConfig in AntlrConfigs.values():
            if projectConfig.grammarType == "parser":
                dir = os.path.dirname(filepath)
                parserDir = os.path.dirname(projectConfig.g4Path)
                if os.path.samefile(dir, parserDir):
                    return projectConfig
        raise Exception("Unknown antlr project.")

if __name__ == "__main__":
    RunnerUtils.Launch(AntlrRunner)
