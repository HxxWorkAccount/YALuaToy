local foobar = require "Assets.Lua.foobar"
local ClassUtils = require "Assets.Lua.Examples.ClassUtils"
local class, super = ClassUtils.class, ClassUtils.super

print(foobar.hello)

local TestFather = class()

    function TestFather:__add__(lhs, rhs)
        print("call Father __add__", self, lhs, rhs)
        -- return self.val + other.val
        return 2
    end

local TestClassA = class(TestFather)

local TestClassB = class()

    function TestClassB:__init__(name, val)
        self.name = name
        self.val  =val
    end
    
    function TestClassB:PrintName()
        print(self.name)
    end

    function TestClassB:__call__(i, j)
        print(i + j)
    end

    function TestClassB:__str__()
        return "Hahahahaha"
    end

local a = TestClassA()
local b = TestClassB("b", 233)
local c = a + b
c = b + a
