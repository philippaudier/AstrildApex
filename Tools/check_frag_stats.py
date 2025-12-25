p = r'Editor/bin/Debug/net8.0-windows/Engine/Logs/failed_frag_5c9bfdbb47c943488133c9ca17842a86.frag'
with open(p,'rb') as f:
    s = f.read().decode('utf-8',errors='replace')
print('dblquotes=', s.count('"'))
print("singlequotes=", s.count("'"))
print('blockcomment_open=', s.count('/*'), 'blockcomment_close=', s.count('*/'))
print('lines=', len(s.splitlines()))
print('lastchars=')
print(repr(s[-200:]))
