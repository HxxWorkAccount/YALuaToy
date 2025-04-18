#include "lua.hpp"
#include <fstream>
#include <iostream>
#include <sstream>

extern "C" {
#include "lua536/ldebug.h"
#include "lua536/ldo.h"
#include "lua536/lgc.h"
#include "lua536/llex.h"
#include "lua536/lobject.h"
#include "lua536/lstate.h"
#include "lua536/lstring.h"
#include "lua536/ltable.h"
#include "lua536/lzio.h"
}

using namespace std;

const string TOKEN_NAMES[] = {
    "and", "break", "do", "else",   "elseif", "end",  "false", "for",      "function",  "goto",   "if",       "in",  "local",
    "nil", "not",   "or", "repeat", "return", "then", "true",  "until",    "while",     "//",     "..",       "...", "==",
    ">=",  "<=",    "~=", "<<",     ">>",     "::",   "<eof>", "<number>", "<integer>", "<name>", "<string>",
};

/* 根据 Token 类型返回 source */
string TokenSource(Token token) {
    stringstream ss = stringstream();
    switch (token.token) {
    case TK_FLT:
        ss << fixed << defaultfloat << token.seminfo.r;
        return ss.str();
    case TK_INT:
        return to_string(token.seminfo.i);
    case TK_NAME:
        return string(getstr(token.seminfo.ts), tsslen(token.seminfo.ts));
    case TK_STRING: {
        // string s = string(getstr(token.seminfo.ts), tsslen(token.seminfo.ts));
        // for (unsigned char c: s)
        //     ss << "0x" << hex << setw(2) << setfill('0') << static_cast<int>(c) << " ";
        // return ss.str();
        return string(getstr(token.seminfo.ts), tsslen(token.seminfo.ts));
    }
    default:
        if (token.token < FIRST_RESERVED)
            return string(1, static_cast<char>(token.token));
        else
            return TOKEN_NAMES[token.token - FIRST_RESERVED];
    }
}

string TokenName(Token token) {
    if (token.token < FIRST_RESERVED)
        return TokenSource(token);
    else
        return TOKEN_NAMES[token.token - FIRST_RESERVED];
}

const int          CHUNK_SIZE = 512;
static const char* FileReader(lua_State* L, void* data, size_t* size) {
    ifstream* fstream = static_cast<ifstream*>(data);
    if (!fstream || !fstream->good()) {
        *size = 0;
        return nullptr;
    }
    static string buffer; /* 使用 static 保证返回的指针在下一次调用前依然有效 */
    char          temp[CHUNK_SIZE];
    fstream->read(temp, CHUNK_SIZE);
    streamsize count = fstream->gcount();
    if (count <= 0) {
        *size = 0;
        return nullptr;
    }
    buffer.assign(temp, count);
    *size = static_cast<size_t>(count);
    return buffer.c_str();
}

int main(int argc, char* argv[]) {
    /* 从启动参数中读取文件 */
    if (argc < 2) {
        cout << "Usage: dumplexer <lua_source_file>" << endl;
        return 1;
    }
    string filepath = argv[1];

    /* 创建 Lua 状态机（这里仅用于内部词法分析，不开启标准库） */
    lua_State* L = luaL_newstate();
    if (!L) {
        cerr << "Failed to create Lua state." << endl;
        return 1;
    }

    /* 创建输入流 */
    ZIO      z;
    ifstream fileStream(filepath, ios::in | ios::binary); /* 直接把二进制数据传给 Lua 即可 */
    if (!fileStream.is_open()) {
        cerr << "Cannot open fstream: " << filepath << endl;
        lua_close(L);
        return 1;
    }
    luaZ_init(L, &z, FileReader, (void*)&fileStream);

    /* 初始化缓冲区 */
    Mbuffer buff;
    luaZ_initbuffer(L, &buff);

    /* 初始化词法分析状态 */
    LexState ls;
    ls.h = luaH_new(L);         /* 创建 LexState 的编译期常量表；create table for scanner */
    sethvalue(L, L->top, ls.h); /* 放在栈顶上，避免被 GC；anchor it */
    luaD_inctop(L);             /* 栈顶++ */
    ls.buff = &buff;
    ls.L    = L;
    luaX_setinput(L, &ls, &z, luaS_new(L, filepath.c_str()), 0); /* 第一个字符就不读取了，不需要这一步 */

    cout << "Dumping tokens from: " << filepath << endl;

    /* 循环读取 token，直到遇到文件结束 token（TK_EOS） */
    int ignoreLine = -1;
    int lastLine   = -1;
    int prevLine   = -1;
    do {
        luaX_next(&ls);
        Token token = ls.t;
        if (prevLine != ls.linenumber)
            lastLine = prevLine;
        /* 输出包含三个内容：行号-Token类型-源 */
        if (ls.linenumber == ignoreLine)
            continue;
        if (token.token == 0 || token.token == TK_FLT)
            continue;
        if (prevLine != ls.linenumber && TokenName(token)[0] == '#') {
            ignoreLine = ls.linenumber;
            continue;
        }
        // cout << "Line " << ls.linenumber << ", " << TokenName(token) << "[" << token.token << "], '" << TokenSource(token) << '\'' << endl;
        cout << "Line " << ls.linenumber << ", " << TokenName(token) << ", '" << TokenSource(token) << '\'' << endl;
        prevLine = ls.linenumber;
    } while (ls.t.token != TK_EOS);

    lua_close(L);
    return 0;
}
