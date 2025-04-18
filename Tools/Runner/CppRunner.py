# -*- coding: utf-8 -*-
import os
import sys
from CSharpRunner import CSharpRunner
from RunnerBase import RunnerBase
from Builder.CppBuilder import CppBuilder
from Const.ProjectConfig import CppProjectConfig, CppConfigs, CSharpConfigs
from Utils import PathUtils, RunnerUtils

class CppRunner(RunnerBase):
    BUILDER_CLASS = CppBuilder

    @property
    def NeedRun(self) -> bool:
        return self.projectConfig.needRun

    @staticmethod
    def GetConfigByPath(filepath: str) -> CppProjectConfig:
        if PathUtils.SameRoot(filepath, "CLua"):
            return CppConfigs["CLua"]
        elif PathUtils.SameRoot(filepath, "CppPlayground"):
            return CppConfigs["CppPlayground"]
        else:
            raise Exception("Unknown C++ project.")

if __name__ == "__main__":
    RunnerUtils.Launch(CppRunner)
