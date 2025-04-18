# -*- coding: utf-8 -*-
import sys
import os
import platform
import shutil
from Const.ProjectConfig import AntlrProjectConfig, AntlrConfigs
from Builder.BuilderBase import BuildResult, BuilderBase
from Utils import CommandUtils, PathUtils

class AntlrBuilder(BuilderBase):

    def BeforeBuild(self):
        assert(isinstance(self.projectConfig, AntlrProjectConfig))
        # 如果编译 parser，则要先编译 lexer
        if self.projectConfig.grammarType == "parser":
            name = self.projectConfig.name
            lexerBuilder = AntlrBuilder(AntlrConfigs[f"{name[:-6]}Lexer"])
            lexerBuilder.Build()

    def DoBuild(self) -> BuildResult:
        assert(isinstance(self.projectConfig, AntlrProjectConfig))
        assert(PathUtils.SameRoot(self.projectConfig.g4Path, ".")) # g4 文件必须是项目中的文件
        g4path = os.path.relpath(self.projectConfig.g4Path, ".")
        filename = os.path.basename(g4path).split(".")[0]
        PathUtils.CheckDir(self.projectConfig.outputDir, create=True)
        command = [
            "java", "-jar", "3rd/Antlr4/antlr-4.13.2-complete.jar",
            "-package", self.projectConfig.package,
            "-Dlanguage=CSharp",
            "-o", f"{self.projectConfig.outputDir}",
        ]
        # if self.projectConfig.template != "":
        #     command.append(f"-templates '{self.projectConfig.template}'")
        command.append(g4path) # 使用绝对路径会导致生成的代码里包含绝对路径（不利于版本同步）
        result = CommandUtils.TryExecuteCommand(command)

        # 把输出结果手动挪到正确位置
        if result == 0:
            targetDir = os.path.join(self.projectConfig.outputDir, os.path.dirname(g4path))
            topDir = PathUtils.GetTop(g4path)
            assert(topDir != "")
            tempDir = os.path.join(self.projectConfig.outputDir, topDir)
            shutil.copytree(targetDir, self.projectConfig.outputDir, dirs_exist_ok=True)
            shutil.rmtree(tempDir)
            

        # 编译 lexer 要把 .tokens 文件拷回 g4 所在目录，以便编译 parser 文件
        g4Dir = os.path.dirname(g4path)
        if result == 0 and self.projectConfig.grammarType == "lexer":
            tokensFile = f"{self.projectConfig.outputDir}/{filename}.tokens"
            if os.path.exists(tokensFile):
                shutil.copy(tokensFile, g4Dir)
                print(f"copy '{tokensFile}' to '{g4Dir}'")
            else:
                print(f"WARNING! tokens file '{tokensFile}' not found")

        return BuildResult(result, None)
