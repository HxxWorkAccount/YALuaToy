# -*- coding: utf-8 -*-
import os
import sys
from typing import Any

from Builder.BuilderBase import BuilderBase, BuildResult
from Utils import CommandUtils

class RunnerBase:
    BUILDER_CLASS = BuilderBase

    def __init__(self, projectConfig, args: list):
        self.projectConfig = projectConfig
        self.args = args

    @property
    def NeedRun(self) -> bool:
        raise NotImplemented

    def Run(self):
        Builder = self.BUILDER_CLASS
        builder = Builder(self.projectConfig)
        self.BeforeRun()
        buildResult = builder.TryBuild()
        if buildResult.Failed:
            print(f"Build failed, result: {buildResult.result}")
            return

        if self.NeedRun:
            self.DoRun(buildResult)
            self.AfterRun(True)
        else:
            print("Only build.")
            self.AfterRun(False)

    def DoRun(self, buildResult: BuildResult):
        command = [buildResult.output] # 默认实现，把 outputData 当作可执行文件路径
        command.extend(self.args)
        CommandUtils.TryExecuteCommand(command)

    def BeforeRun(self):
        pass

    def AfterRun(self, ran: bool):
        pass

    @staticmethod
    def GetConfigByPath(filepath: str) -> Any:
        """注意，该接口不应"""
        raise NotImplemented
