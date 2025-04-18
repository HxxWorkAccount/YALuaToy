# -*- coding: utf-8 -*-
import sys
from typing import Optional, Type
from Runner.RunnerBase import RunnerBase

def Launch(Runner: Type[RunnerBase], projectConfig=None, argv: Optional[list]=None):
    if projectConfig is None:
        filePath = sys.argv[1]
        projectConfig = Runner.GetConfigByPath(filePath)
    if argv is None:
        argv = [] if len(sys.argv) <= 2 else sys.argv[2:]
    runner = Runner(projectConfig, argv)
    runner.Run()
