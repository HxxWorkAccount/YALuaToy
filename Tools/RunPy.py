# -*- coding: utf-8 -*-
# NOTE: all .py under "Tools/" must be run by this scripts,
#       in order to properly set 'PYTHONPATH'.

import os
import sys
import platform

if __name__ == "__main__":
    toolsDir = os.path.dirname(os.path.realpath(__file__))

    env = os.environ.copy()
    if platform.system() == "Windows":
        env["PYTHONPATH"] = ";".join([env.get("PYTHONPATH", ""), toolsDir])
    else:
        env["PYTHONPATH"] = ":".join([env.get("PYTHONPATH", ""), toolsDir])

    sys.path.append(toolsDir)
    from Utils import CommandUtils

    i = 0
    quiet = False
    for i in range(1, len(sys.argv)):
        arg = sys.argv[i]
        if (arg.startswith("-")):
            if arg[1:] == "q" or arg[1:] == "quiet":
                quiet = True
                continue
            else:
                raise Exception(f"Unknown argument: {arg}")
        break

    CommandUtils.ExecuteCommand(["python", *sys.argv[i:]], env=env, quiet=quiet)
