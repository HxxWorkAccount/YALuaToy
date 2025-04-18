# -*- coding: utf-8 -*-

import sys
import os
from Utils import PathUtils

PROJECT = "YALuaToy"
IGNORE_ITEMS = ["obj", "bin", "Assets", "Generated_Antlr"]

if __name__ == "__main__":
    projectPath = PROJECT
    testProjectPath = f"{PROJECT}.Tests"

    def ScanDir(reldir: str, ignoreFile: bool):
        projectDir = os.path.join(projectPath, reldir)
        testDir = os.path.join(testProjectPath, reldir)
        for item in os.listdir(projectDir):
            if (item in IGNORE_ITEMS): continue
            itemPath = os.path.join(projectDir, item)
            if (os.path.isfile(itemPath)):
                if (ignoreFile or not item.endswith(".cs")): continue
                testItemPath = os.path.join(testDir, item.replace(".cs", "Tests.cs"))
                print(f"check file: {testItemPath}")
                PathUtils.CheckFile(testItemPath, True)
            else:
                testDir = os.path.join(testProjectPath, item)
                print(f"check dir: {testDir}")
                PathUtils.CheckDir(testDir, True)
                ScanDir(os.path.join(reldir, item), False)
    
    ScanDir("", True)

        
