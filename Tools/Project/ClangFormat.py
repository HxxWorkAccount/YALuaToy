# -*- coding: utf-8 -*-

import sys
import os
from Utils import CommandUtils

if __name__ == "__main__":
    filePath = sys.argv[1]
    command = [
        "clang-format",
        filePath
    ]
    CommandUtils.ExecuteCommand(command, quiet=True)

