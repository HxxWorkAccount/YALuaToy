# -*- coding: utf-8 -*-
import os
import sys
from Const.ProjectConfig import CSharpConfigs, CSharpProjectConfig, AntlrConfigs
from Builder.AntlrBuilder import AntlrBuilder
from Runner.CSharpRunner import CSharpRunner
from Utils import RunnerUtils

if __name__ == "__main__":
    # 编译 Antlr 模版
    parserBuilder = AntlrBuilder(AntlrConfigs["YALuaToyParser"])
    parserBuilder.Build()

    # 编译 Interpreter
    RunnerUtils.Launch(CSharpRunner, CSharpConfigs["YALuaToy.Interpreter"])
    pass
