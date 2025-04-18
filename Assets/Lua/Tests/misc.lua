local foo = require "Assets.Lua.Tests.foobar"

local a = foo.a
local b = a + foo.a
print(a + b)
assert(a + b == 9)
assert(a + b == 9, "wrong calculation")
collectgarbage("stop", 3);

local whh = { 1, 2, 3, 4, 5, a=3, b=2, c=1 }
local whhMt = getmetatable(whh)
assert (whhMt == nil)

print("test ipairs")
for i, v in ipairs(whh) do
    print(i, v)
    assert(i == v)
end

print("test pairs")
for k, v in pairs(whh) do
    print(k, v)
end

local func = load("print(1,2,3) return \"haha\"", "test", "t")
print(func())

local codes = {
    "print",
    "(4,5",
    ",6)",
    " print(a)"
}
local index = 0
local func2 = load(function ()
    index = index + 1
    return codes[index]
end, "func2", "t", { print=print, a=233 })
func2()

local foobar = loadfile("Assets/Lua/foobar.lua", "t")()
print(foobar.a)

print(type(foobar))

-- 测试用例：验证 rawequal、rawlen、rawget 和 rawset 的功能

print("=== Lua 原始函数测试 ===")

----------------------------------
-- 测试 rawequal
----------------------------------
print("\n[rawequal 测试]")
local t1 = {}
local t2 = {}
local t3 = t1  -- 同一个表

print("t1 与 t2 是否相等？", rawequal(t1, t2))  -- false：不同表
print("t1 与 t3 是否相等？", rawequal(t1, t3))  -- true：同一表引用

----------------------------------
-- 测试 rawset 和 rawget
----------------------------------
print("\n[rawset 和 rawget 测试]")
local tbl = { key1 = "初始值" }
print("修改前，tbl.key1 =", rawget(tbl, "key1"))

-- 使用 rawset 修改 tbl 中 key1 的值
rawset(tbl, "key1", "新值")
print("使用 rawset 修改后，tbl.key1 =", rawget(tbl, "key1"))

-- 使用 rawset 添加新键值对
rawset(tbl, "key2", 42)
print("添加新键，tbl.key2 =", rawget(tbl, "key2"))
print("直接访问，tbl.key2 =", tbl.key2)

----------------------------------
-- 测试 rawlen
----------------------------------
print("\n[rawlen 测试]")
local arr = { 1, 2, 3, 4, 5 }
print("初始数组长度，#arr =", #arr)
print("初始数组 rawlen(arr) =", rawlen(arr))

-- 修改数组，使中间出现 nil
arr[3] = nil
print("设置 arr[3] = nil 后，#arr =", #arr)
print("设置 arr[3] = nil 后，rawlen(arr) =", rawlen(arr))

-- 使用 __len 元方法，rawlen 忽略 __len
local mt = {
    __len = function(t)
        return 999
    end
}
setmetatable(arr, mt)
print("\n设置 __len 元方法后：")
print("使用 # 运算符，数组长度 =", #arr)         -- 会调用 __len 元方法
print("使用 rawlen(arr) =", rawlen(arr))            -- 忽略元方法，返回实际长度

print("=== 测试 next 和 select ===")

-------------------------------
-- 测试 next
-------------------------------
print("\n[测试 next]")
local testNextTable = { apple = "red", banana = "yellow", grape = "purple" }
print("遍历表 t 的所有键值对：")
local key, value = next(testNextTable)
while key do
    print(key, value)
    key, value = next(testNextTable, key)
end

-------------------------------
-- 测试 select
-------------------------------
print("\n[测试 select]")
local function testSelect(...)
    local argCount = select("#", ...)
    print("传入参数总数:", argCount)
    for i = 1, argCount do
        local arg = select(i, ...)
        print("参数:", i, tostring(arg))
    end
end

print("调用函数 testSelect:")
testSelect("Lua", 123, true, nil, { x = 10 })

print("=== Testing tonumber ===")

local function test_tonumber(input, base, expected)
    local result
    if base then
        result = tonumber(input, base)
        print("tonumber(%q, %d) -> %s", input, base, tostring(result))
    else
        result = tonumber(input)
        print("tonumber(%q) -> %s", input, tostring(result))
    end
    if (expand) then
        assert(result == expected, "not matched"..tostring(result).." != "..tostring(expected))
    end
end

-- 测试不带 base 参数的转换
test_tonumber("123")            -- 整数
test_tonumber("45.67")          -- 浮点数
test_tonumber("  789  ")        -- 含有空白字符
test_tonumber("0x1A")           -- 十六进制字符串（Lua 5.3 及以上版本支持）

-- 测试带 base 的转换
test_tonumber("1010", 2, 10)        -- 二进制，结果应为 10
test_tonumber("377", 8, 255)         -- 八进制，结果应为 255
test_tonumber("1A", 16, 26)         -- 十六进制，结果应为 26
test_tonumber("1234", 5, 5)        -- 5 进制中字符 '3' 和 '4' 超出范围，返回 nil

-- 测试无效输入
test_tonumber("hello")
test_tonumber("", 10)

print("=== Testing pcall and xpcall ===")

-- 定义一个正常返回结果的函数
local function goodFunc(a, b)
    return a + b
end

-- 定义一个在执行时报错的函数
local function badFunc(a, b)
    error("badFunc triggered an error!")
end

--------------------------
-- 使用 pcall 测试函数
--------------------------
print("\n[pcall] 正常调用 goodFunc:")
local status, result = pcall(goodFunc, 2, 3)
print("Status:", status)       -- true
print("Result:", result)       -- 5

print("\n[pcall] 调用 badFunc（预期报错）:")
local status, err = pcall(badFunc, 2, 3)
print("Status:", status)       -- false
print("Error:", err)

--------------------------
-- 使用 xpcall 测试函数，并带有错误处理函数
--------------------------
-- 错误处理函数：用于格式化错误信息
local function errorHandler(e)
    return "Handled error: " .. tostring(e)
end

print("\n[xpcall] 正常调用 goodFunc:")
status, result = xpcall(goodFunc, errorHandler, 4, 5)
print("Status:", status)       -- true
print("Result:", result)       -- 9

print("\n[xpcall] 调用 badFunc（预期报错）:")
status, err = xpcall(badFunc, errorHandler, 4, 5)
print("Status:", status)       -- false
print("Error:", err)

-- error("test error");

print(collectgarbage("count"))
print(collectgarbage())
print(collectgarbage("count"))

print("\n=== 测试结束 ===")

