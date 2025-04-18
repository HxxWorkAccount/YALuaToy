#include <iostream>
#include "lua.hpp"

extern "C" {
}

using namespace std;

int main(int argc, char* argv[]) {
    cout << "Enter Main\n" << endl;

    const char* filename = "Assets/Lua/misc.lua";
    lua_State* L = luaL_newstate(); 
    int status = luaL_loadfile(L, filename);
    if (status != LUA_OK) {
        cerr << "Error loading file: " << lua_tostring(L, -1) << endl;
        lua_close(L);
        return 1;
    }

    cout << "\nExit Main" << endl;
    return 0;
}
