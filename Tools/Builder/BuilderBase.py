# -*- coding: utf-8 -*-
import os
import sys
import shutil
from typing import Optional, Any
from Utils import PathUtils

class BuildResult:

    def __init__(self, result: int, output, msg: str=""):
        self.result = result
        self.output = output
        self.msg = msg

    @property
    def Success(self):
        return self.result == 0

    @property
    def Failed(self):
        return self.result != 0

class BuilderBase:

    def __init__(self, projectConfig):
        self.projectConfig = projectConfig

    def TryBuild(self) -> BuildResult:
        self.BeforeBuild()
        buildResult = self.DoBuild()
        self.AfterBuild(buildResult)
        return buildResult
    
    def Build(self) -> BuildResult:
        self.BeforeBuild()
        buildResult = self.DoBuild()
        if buildResult.result != 0:
            raise Exception("Build failed.", buildResult.msg)
        self.AfterBuild(buildResult)
        return buildResult

    def DoBuild(self) -> BuildResult:
        raise NotImplemented

    def BeforeBuild(self):
        pass
    
    def AfterBuild(self, buildResult: BuildResult):
        pass
