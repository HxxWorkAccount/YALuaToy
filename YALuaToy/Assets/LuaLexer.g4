lexer grammar LuaLexer;

channels {
    CommentChannel
}

/* reserved single char */
SEMI:   ';';
ASSIGN: '=';
COMMA:  ',';
LT:     '<';
GT:     '>';
DOT:    '.';
SQUIG:  '~';
MINUS:  '-';
POUND:  '#';
OP:     '(';
CP:     ')';
AMP:    '&';
PER:    '%';
COL:    ':';
PLUS:   '+';
STAR:   '*';
OCU:    '{';
CCU:    '}';
OB:     '[';
CB:     ']';
PIPE:   '|';
CARET:  '^';
SLASH:  '/';

/* reserved multi char */
AND:      'and';
BREAK:    'break';
DO:       'do';
ELSE:     'else';
ELSEIF:   'elseif';
END:      'end';
FALSE:    'false';
FOR:      'for';
FUNCTION: 'function';
GOTO:     'goto';
IF:       'if';
IN:       'in';
LOCAL:    'local';
NIL:      'nil';
NOT:      'not';
OR:       'or';
REPEAT:   'repeat';
RETURN:   'return';
THEN:     'then';
TRUE:     'true';
UNTIL:    'until';
WHILE:    'while';
IDIV:     '//';
CONCAT:   '..';
DOTS:     '...';
EQ:       '==';
GE:       '>=';
LE:       '<=';
NE:       '~=';
SHL:      '<<';
SHR:      '>>';
DBCOLON:  '::';

NAME: [a-zA-Z_][a-zA-Z_0-9]*;

NORMALSTRING: '"' ( EscapeSequence | ~('\\' | '"'))* '"';

CHARSTRING: '\'' ( EscapeSequence | ~('\'' | '\\'))* '\'';

LONGSTRING: '[' NESTED_STR ']' ;

/* 按照 Antlr 的最长匹配规则，它可以合法的读取 '[[[[]]]]' 这样的输入，这种情况在 LuaLexerUtils.ReadLongString 里处理了 */
fragment NESTED_STR: '=' NESTED_STR '=' | '[' .*? ']';

INT: Digit+;

HEX: '0' [xX] HexDigit+;

FLOAT
    : Digit+ '.' Digit* ExponentPart?
    | '.' Digit+ ExponentPart?
    | Digit+ ExponentPart;

HEX_FLOAT
    : '0' [xX] HexDigit+ '.' HexDigit* HexExponentPart?
    | '0' [xX] '.' HexDigit+ HexExponentPart?
    | '0' [xX] HexDigit+ HexExponentPart;

fragment ExponentPart: [eE] [+-]? Digit+;

fragment HexExponentPart: [pP] [+-]? Digit+;

fragment EscapeSequence
    : '\\' [abfnrtvz"'|$#\\] // World of Warcraft Lua additionally escapes |$# 
    | '\\' '\r'? '\n'
    | DecimalEscape
    | HexEscape
    | UtfEscape;

fragment DecimalEscape
    : '\\' Digit
    | '\\' Digit Digit
    | '\\' [0-2] Digit Digit;

fragment HexEscape: '\\' 'x' HexDigit HexDigit;

fragment UtfEscape: '\\' 'u{' HexDigit+ '}';

fragment Digit: [0-9];

fragment HexDigit: [0-9a-fA-F];

fragment SingleLineInputCharacter: ~[\r\n\u0085\u2028\u2029];

COMMENT: ('--' LONGSTRING | '--' ~[\r\n]*) -> channel(CommentChannel);

WS: [ \t\u000C\r]+ -> channel(HIDDEN);

NL: [\n] -> skip;

SHEBANG
    : '#' { IsLine1Col0() }? '!'? SingleLineInputCharacter* -> channel(HIDDEN);
