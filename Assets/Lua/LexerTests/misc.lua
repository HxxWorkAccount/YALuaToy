-- testing special comment on first line
s = "\0 \x7F\z
     \xC2\x80 \xDF\xBF\z
     \xE0\xA0\x80 \xEF\xBF\xBF\z
     \xF0\x90\x80\x80  \xF4\x8F\xBF\xBF"
testloadfile("\xEF\xBB\xBFreturn 239", 239)
testloadfile("\xEF\xBB\xBF", nil)   -- empty file with a BOM
