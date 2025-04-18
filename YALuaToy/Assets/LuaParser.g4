parser grammar LuaParser;

options {
    tokenVocab = LuaLexer;
}

start
    : chunk EOF
    ;

chunk
    : block
    ;

block
    : stat* retstat?
    ;

stat
    : ';'                                                                      # EmptyStat
    | varlist '=' explist                                                      # Assign
    | functioncall                                                             # FunctionCallStat
    | label                                                                    # LabelStat
    | 'break'                                                                  # Break
    | 'goto' NAME                                                              # Goto
    | 'do' block 'end'                                                         # Do
    | 'while' exp 'do' block 'end'                                             # While
    | 'repeat' block 'until' exp                                               # Repeat
    | 'if' exp 'then' block ('elseif' exp 'then' block)* ('else' block)? 'end' # If
    | 'for' NAME '=' exp ',' exp (',' exp)? 'do' block 'end'                   # NumericFor
    | 'for' namelist 'in' explist 'do' block 'end'                             # GenericFor
    | 'function' funcname funcbody                                             # GlobalFunction
    | 'local' 'function' NAME funcbody                                         # LocalFunction
    | 'local' attnamelist ('=' explist)?                                       # LocalAttr
    ;

attnamelist
    : NAME attrib (',' NAME attrib)*
    ;

attrib
    : ('<' NAME '>')?
    ;

/* 虽然标准 BNF 并未规定 break 作为结束语句，但其实 break 后面的代码也是无效的 */
retstat
    // : ('return' explist? | 'break') ';'?
    : 'return' explist? ';'?
    ;

label
    : '::' NAME '::'
    ;

funcname
    : NAME ('.' NAME)* (':' NAME)?
    ;

varlist
    : var (',' var)*
    ;

namelist
    : NAME (',' NAME)*
    ;

explist
    : exp (',' exp)*
    ;

/* 注意，下面的运算符部分要按优先级排序 */
exp
    : 'nil'
    | 'false'
    | 'true'
    | number
    | string
    | '...'
    | functiondef
    | prefixexp
    | tableconstructor
    | <assoc= right> left=exp ('^') right=exp
    | ('not' | '#' | '-' | '~') exp
    | left=exp ('*' | '/' | '%' | '//') right=exp
    | left=exp ('+' | '-') right=exp
    | <assoc= right> left=exp ('..') right=exp
    | left=exp ('<<' | '>>') right=exp
    | left=exp ('&') right=exp
    | left=exp ('~') right=exp
    | left=exp ('|') right=exp
    | left=exp ('<' | '>' | '<=' | '>=' | '~=' | '==') right=exp
    | left=exp ('and') right=exp
    | left=exp ('or') right=exp
    ;

var
    : var_name
    | prefixexp ('[' exp ']' | '.' NAME)
    ;
var_name : NAME ; /* 帮 prefixexp 消除左递归 */

/* 消除间接左递归 prefixexp ::= var | functioncall | '(' exp ')' */
prefixexp
    : functioncall prefixexp_
    | prefixexp_without_functioncall
    ;
prefixexp_without_functioncall /* 帮 function 消除左递归 */
    : var_name prefixexp_
    | '(' exp ')' prefixexp_
    ;
prefixexp_
    : ('[' exp ']' | '.' NAME) prefixexp_ /* var */
    |
    ;

/* 消除间接左递归 functioncall ::= prefixexp (args | ':' Name args) */
functioncall_
    : prefixexp_ (args | ':' NAME args) functioncall_
    |
    ;
functioncall
    : prefixexp_without_functioncall (args | ':' NAME args) functioncall_
    ;

args
    : '(' explist? ')'
    | tableconstructor
    | string
    ;

functiondef
    : 'function' funcbody
    ;

funcbody
    : '(' parlist ')' block 'end'
    ;

/* lparser.c says "is 'parlist' not empty?"
 * That code does so by checking la(1) == ')'.
 * This means that parlist can derive empty.
 */
parlist
    : namelist (',' '...')?
    | '...'
    |
    ;

tableconstructor
    : '{' fieldlist? '}'
    ;

fieldlist
    : field (fieldsep field)* fieldsep?
    ;

field
    : '[' exp ']' '=' exp
    | NAME '=' exp
    | exp
    ;

fieldsep
    : ','
    | ';'
    ;

number
    : INT
    | HEX /* 注意 HEX 可能是浮点数，也可能是整数 */
    | FLOAT
    | HEX_FLOAT
    ;

string
    : NORMALSTRING
    | CHARSTRING
    | LONGSTRING
    ;
