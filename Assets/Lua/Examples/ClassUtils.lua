local ClassUtils = {}

-- 以下方法会被转发到类里实现（如：类实现方法 `__add__`，就相当于在元表里实现了 `__add`）
-- 不过目前还没想好 "__index", "__newindex", "__gc", "__mode"  这几个怎么处理（__gc 怕有性能问题）
local _BinTagMethods = { "__add", "__sub", "__mul", "__div", "__mod", "__pow", "__idiv", "__band", "__bor", "__bxor", "__shl", "__shr", "__concat", "__eq", "__lt", "__le" }
local _OtherTagMethods = { "__len", "__unm", "__bnot", "__call", "__str" }
local _TagMethodHandlers = {}

-- 初始化 BinTagMethodHandler
for i, tagmethod in ipairs(_BinTagMethods) do
    local function TagMethodHandler(...)
        for _, arg in ipairs({...}) do -- 找到第一个有对应元方法的对象
            local tm = tagmethod.."__"
            if arg[tm] then
                return arg[tm](arg, ...)
            end
        end
    end
    _TagMethodHandlers[tagmethod] = TagMethodHandler
end

-- 初始化 OtherTagMethodHandler
for i, tagmethod in ipairs(_OtherTagMethods) do
    local function TagMethodHandler(ins, ...)
        return ins[tagmethod.."__"](ins, ...)
    end
    _TagMethodHandlers[tagmethod] = TagMethodHandler
end


--[ ================== Base ================== ]--


-- 万物基类
local object = {}
ClassUtils.object = object

    function object:__init__()
    end


--[ ================== MRO ================== ]--


local function _MroSearch(symbol, class)
    for i, parent in ipairs(class.__mro__) do
        value = rawget(parent, symbol)  -- 不能直接用索引访问，因为元表的 __index 会被调用，可能调用到基类的东西
        if value ~= nil then
            return value
        end
    end
    return nil
    -- error("Symbol not found: " .. symbol)
end

local function _CreateMro(class, parents)
    local marks = {}
    local function AppendParents(results, parents)
        if results == nil or parents == nil then
            return
        end
        uniqueParentList = {}
        for i, parent in ipairs(parents) do
            parent = parents[i]
            if marks[parent] == nil then  -- 处理菱形继承
                table.insert(uniqueParentList, parent)
                marks[parent] = true
            end
        end
        for i, parent in ipairs(uniqueParentList) do  -- 把结果复制到 results
            table.insert(results, parent)
        end
        for i, parent in ipairs(uniqueParentList) do
            AppendParents(results, parent.__mro__)
        end
    end

    local mro = {}
    AppendParents(mro, parents)
    class.__mro__ = mro
end


--[ ================== ClassUtils ================== ]--


function ClassUtils.class(...)
    local metaclass = {}
    local class = {}

    -- 若不指定基类，则默认继承 object
    parents = {...}
    if #parents == 0 then
        parents = {object}
    end

    -- 继承
    _CreateMro(class, parents)
    function metaclass.__index(table, symbol) 
        return _MroSearch(symbol, class)
    end

    -- 创建类实例
    function class:__new__()
        local instance = {}
        self.__index = function(ins, symbol)
            -- print("index", ins, symbol)
            return self[symbol]
        end
        setmetatable(instance, self)
        return instance
    end

    -- 构造函数
    function metaclass.__call(tbl, ...)
        local instance = class:__new__()
        instance:__init__(...)
        return instance
    end

    -- 转发元方法
    for tagmethod, handler in pairs(_TagMethodHandlers) do
        rawset(class, tagmethod, handler)
    end

    -- class.__index = class
    setmetatable(class, metaclass)
    return class
end

function ClassUtils.super(class, instance)
    local mro = instance.__mro__
    local superclass = mro[1]
    for i, parent in ipairs(mro) do
        if parent == class then
            superclass = mro[i + 1]
            break
        end
    end

    -- print("super", class, superclass, object)
    local function index(_, funcname)  -- super 后面只允许调用函数
        local func = rawget(superclass, funcname)
        if func == nil then
            error("Function not found: " .. funcname)
        end
        return function(_, ...) return func(instance, ...) end
    end

    return setmetatable({}, {__index = index})
end

function ClassUtils.callable(instance)
    if type(instance) == "function" then
        return true
    elseif type(instance) == "table" then
        mt = getmetatable(instance)
        return mt and mt.__call ~= nil
    end
    return false
end

function ClassUtils.isinstance(instance, class)
    if getmetatable(instance) == class then
        return true
    end
    for i, parent in ipairs(instance.__mro__) do
        if parent == class then
            return true
        end
    end
    return false
end

return ClassUtils
