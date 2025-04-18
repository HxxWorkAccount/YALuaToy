# -*- coding: utf-8 -*-
import os
import sys
import platform

def GetExecutable(path: str) -> str:
    if platform.system() == "Windows":
        if not path.endswith("exe"):
            return path + ".exe"
    return path

def SameRoot(path: str, directory: str) -> bool:
    path = os.path.realpath(path)
    directory = os.path.realpath(directory)
    return os.path.commonpath([path, directory]) == directory

def CheckDir(dir: str, create: bool = False) -> bool:
    if not os.path.exists(dir):
        if create:
            os.makedirs(dir)
            return True
        return False
    return True

def CheckFile(file: str, create: bool = False) -> bool:
    if not os.path.exists(file):
        if create:
            with open(file, "w") as f:
                f.write("")
            return True
        return False
    return True

def GetTop(path: str) -> str:
    norm = os.path.normpath(path)
    parts = norm.split(os.sep)
    for part in parts:
        if part != "":
            return part
    return ""

def CopyDirContents(fromDir: str, toDir: str):
    pass
