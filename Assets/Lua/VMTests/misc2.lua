local results = {}

-- Simple arithmetic
local function add(a, b)
    return a + b
end

local function test_arithmetic()
    local r = add(3, 4)  -- Expect 7.
    results[#results+1] = (r == 7)
end

-- Table manipulation test
local function test_tables()
    local t = {}           -- Create a table.
    t[1] = "one"
    t[2] = "two"
    results[#results+1] = (t[1] == "one" and t[2] == "two")
end

-- Loop test (summing numbers 1 to 5)
local function test_loop()
    local sum = 0
    for i = 1, 5 do
        sum = sum + i
    end
    results[#results+1] = (sum == 15)
end

-- Closure test: a simple counter
local function test_closure()
    local function makeCounter()
        local count = 0
        return function()
            count = count + 1
            return count
        end
    end
    local counter = makeCounter()
    local a = counter()  -- Expect 1.
    local b = counter()  -- Expect 2.
    results[#results+1] = (a == 1 and b == 2)
end

-- Tail recursion test: factorial using tail call optimization
local function test_tailcall()
    local function fac(n, acc)
        if n == 0 then 
            return acc 
        else 
            return fac(n - 1, n * acc)
        end
    end
    results[#results+1] = (fac(5, 1) == 120)
end

-- Vararg test: sum all arguments
local function test_vararg()
    local function sumAll(...)
        local s = 0
        local args = {...}
        for i = 1, #args do
            s = s + args[i]
        end
        return s
    end
    results[#results+1] = (sumAll(1, 2, 3, 4, 5) == 15)
end

-- Run all tests
test_arithmetic()
test_tables()
test_loop()
test_closure()
test_tailcall()
test_vararg()

-- Return test results as a table of booleans.
return results
