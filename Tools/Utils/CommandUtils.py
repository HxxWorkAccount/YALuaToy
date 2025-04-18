# -*- coding: utf-8 -*-
import os
import sys
import platform
import subprocess

def TryExecuteCommand(command: list, env=None, quiet: bool=False, encoding=None) -> int:
    if not quiet: print("\nExecuteCommand:", *command)
    process = subprocess.Popen(command, stdin=sys.stdin, stdout=sys.stdout, stderr=sys.stderr, text=True, shell=False, env=env, encoding=encoding)
    try:
        process.wait()
    except KeyboardInterrupt:
        pass
    except Exception as e:
        raise e

    if process.returncode != 0:
        print(f"Execute failed, return code: {process.returncode}.")
    return process.returncode

def ExecuteCommand(command: list, env=None, errorHint: str="", quiet: bool=False, encoding=None):
    returncode = TryExecuteCommand(command, env=env, quiet=quiet, encoding=encoding)
    if returncode != 0:
        raise Exception(f"Execute failed, return code: {returncode}. Hint: {errorHint}")
    return 0

def ExecuteCommands(command: list[list], env=None, errorHint: str="", quiet: bool=False, encoding=None):
    for command in command:
        result = ExecuteCommand(command, env=env, errorHint=errorHint, quiet=quiet, encoding=encoding)
    return 0
