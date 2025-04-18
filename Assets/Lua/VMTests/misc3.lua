local results = {}

----------------------------------------------------------------------
-- Test table literals with numeric indices, key-value pairs, 
-- and nested tables.
----------------------------------------------------------------------
local function test_table_literal()
    local t = {
        10, 20, 30,  -- Numeric indices 1,2,3
        a = 100,
        b = 200,
        subtable = {  -- Nested table literal
            x = 1,
            y = 2,
            z = { 3, 4, 5 }  -- Nested numeric array
        }
    }
    local cond1 = (t[1] == 10 and t[2] == 20 and t[3] == 30)
    local cond2 = (t.a == 100 and t.b == 200)
    local cond3 = (t.subtable and t.subtable.x == 1 and t.subtable.y == 2 
                   and t.subtable.z and t.subtable.z[1] == 3 
                   and t.subtable.z[2] == 4 and t.subtable.z[3] == 5)
    results[#results+1] = cond1 and cond2 and cond3
end

----------------------------------------------------------------------
-- Test numeric for loop: sum numbers from 1 to 100.
----------------------------------------------------------------------
local function test_numeric_for()
    local sum = 0
    for i = 1, 100 do
        sum = sum + i
    end
    results[#results+1] = (sum == 5050)
end

----------------------------------------------------------------------
-- Test generic for loop using pairs over a key-value table.
----------------------------------------------------------------------
local function test_generic_for_pairs()
    local t = {
        alpha = 10,
        beta = 20,
        gamma = 30
    }
    local total = 0
    for key, value in pairs(t) do
        total = total + value
    end
    results[#results+1] = (total == 60)
end

----------------------------------------------------------------------
-- Test generic for loop with a custom iterator function.
----------------------------------------------------------------------
local function customIterator(t)
    local i = 0
    local n = #t
    return function()
        i = i + 1
        if i <= n then
            return t[i]
        end
    end
end

local function test_generic_for_custom()
    local arr = {2, 4, 6, 8, 10}
    local prod = 1
    for val in customIterator(arr) do
        prod = prod * val
    end
    -- Expected product: 2 * 4 * 6 * 8 * 10 = 3840
    results[#results+1] = (prod == 3840)
end

----------------------------------------------------------------------
-- Run all tests.
----------------------------------------------------------------------
test_table_literal()
test_numeric_for()
test_generic_for_pairs()
test_generic_for_custom()

return results
