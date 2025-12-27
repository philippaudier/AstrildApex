import re, os
root = r"C:\Users\Philippe\Documents\Programming\AstrildApex"
main = os.path.join(root, r"Engine\Rendering\Shaders\Forward\TerrainForward.frag")
inc_dir = os.path.join(root, r"Engine\Rendering\Shaders\Includes")

def read_file(path):
    with open(path, 'rb') as f:
        return f.read().decode('utf-8', errors='replace')

text = read_file(main)
pattern = re.compile(r'^#include\s+"?([^\"\n\r]+)"?', re.MULTILINE)
seen=set()

def resolve_includes(base_dir, txt):
    def repl(m):
        inc = m.group(1)
        p = os.path.normpath(os.path.join(base_dir, inc))
        if not os.path.isfile(p):
            p2 = os.path.normpath(os.path.join(inc_dir, os.path.basename(inc)))
            if os.path.isfile(p2): p = p2
        if not os.path.isfile(p):
            return f"/* MISSING INCLUDE: {inc} */\n"
        if p in seen:
            return f"/* SKIP RECURSIVE INCLUDE: {inc} */\n"
        seen.add(p)
        content = read_file(p)
        return "\n" + resolve_includes(os.path.dirname(p), content) + "\n"
    return pattern.sub(repl, txt)

combined = resolve_includes(os.path.dirname(main), text)
out_path = os.path.join(root, 'Tools', 'TerrainForward.combined.frag')
with open(out_path, 'w', encoding='utf-8') as f:
    f.write(combined)
print('Wrote combined shader to', out_path)
