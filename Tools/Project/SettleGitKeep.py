# -*- coding: utf-8 -*-
# It's too troublesome to deal with relative paths, and I don’t want to use absolute paths all places,
# so I gave up the feature of specifying the target directory.
# Please execute the script in the target directory directly.

import sys
import os
import glob
import fnmatch

__PROJECT_DIR = "" # Absolute Path
__IGNORE_DIR_LIST = [] # Relative Path
__IGNORE_FILE_PATH_LIST = [] # Relative Path
__IGNORE_FILE_PATTERN_LIST = [] # File Name Pattern
__NOTHING_CHANGE = True


def __Init():
    global __PROJECT_DIR

    if len(sys.argv) > 1:
        raise RuntimeError("No input parameters allowed")

    # Init __PROJECT_DIR
    __PROJECT_DIR = os.path.abspath(".") # Startup path (not the path where the script is located)

    # Init pattern
    gitIgnoreFilePath = os.path.join(__PROJECT_DIR, ".gitignore")
    gitIgnorePatternList = []
    with open(gitIgnoreFilePath) as file:
        for line in file:
            if line != "" and line != "\n" and line[0] != "#":
                gitIgnorePatternList.append(line.rstrip()) # remove \n

    # Init 3 list
    for pattern in gitIgnorePatternList:
        if ("/" in pattern):
            if (pattern[len(pattern) - 1] == "/"): # 目录
                __IGNORE_DIR_LIST.extend(glob.glob(pattern, root_dir=__PROJECT_DIR, recursive=True))
            else: # 指定路径的文件
                __IGNORE_FILE_PATH_LIST.extend(
                    glob.glob(pattern, root_dir=__PROJECT_DIR, recursive=True)
                )
        else: # 文件模式串
            __IGNORE_FILE_PATTERN_LIST.append(pattern)


def __IsIgnoreDir(dir: str) -> bool:
    if (os.path.basename(dir) == ".git"): return True
    for ignoreDir in __IGNORE_DIR_LIST:
        if (os.path.samefile(ignoreDir, dir)): return True
    return False


def __IsIgnoreFile(filepath: str) -> bool:
    for ignoreFilePath in __IGNORE_FILE_PATH_LIST:
        if (os.path.samefile(filepath, ignoreFilePath)): return True
    for pattern in __IGNORE_FILE_PATTERN_LIST:
        if (fnmatch.fnmatch(os.path.basename(filepath), pattern)):
            return True
    return False


def __NeedGitKeep(dir: str) -> bool:
    sublist = os.listdir(dir)

    for subbase in sublist:
        subpath = os.path.join(dir, subbase)

        if (os.path.isdir(subpath)):
            if (not __IsIgnoreDir(subpath)):
                # print("Not ignore dir: {}".format(subpath)) # debug
                return False
        else:
            if (not __IsIgnoreFile(subpath) and subbase != ".gitkeep"):
                # print("Not ignore file: {}".format(subpath)) # debug
                return False

    return True


def __SettleGitKeep(dir: str) -> bool:
    """
    return : False means nothing modified; True means create or delete .gitkeep
    """
    if (__IsIgnoreDir(dir)): return False

    gitkeepPath = os.path.join(dir, ".gitkeep")
    modified = False

    if (__NeedGitKeep(dir)):
        if (not os.path.exists(gitkeepPath)):
            file = open(gitkeepPath, 'w')
            file.close()
            modified = True
            print("create: {}".format(os.path.relpath(gitkeepPath, __PROJECT_DIR)))
    elif (os.path.exists(gitkeepPath)):
        os.remove(gitkeepPath)
        modified = True
        print("delete: {}".format(os.path.relpath(gitkeepPath, __PROJECT_DIR)))

    sublist = os.listdir(dir)
    for subbase in sublist:
        subpath = os.path.join(dir, subbase)
        if (os.path.isdir(subpath)):
            modified = __SettleGitKeep(subpath) or modified

    return modified


def Main():
    __Init()

    # print(__IGNORE_DIR_LIST)
    # print(__IGNORE_FILE_PATH_LIST)
    # print(__IGNORE_FILE_PATTERN_LIST)

    return 1 if __SettleGitKeep(__PROJECT_DIR) else 0


sys.exit(Main())
