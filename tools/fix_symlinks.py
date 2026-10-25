"""Replace git symlinks checked out as small text files (Windows) with copies of their targets.

Usage: python fix_symlinks.py <repo_root>
Walks the repo and all submodules (git ls-files -s, mode 120000).
"""
import os
import shutil
import subprocess
import sys


def links(repo):
    out = subprocess.run(["git", "-C", repo, "ls-files", "-s"], capture_output=True, text=True).stdout
    for line in out.splitlines():
        meta, path = line.split("\t", 1)
        if meta.startswith("120000"):
            yield path


def submodules(repo):
    out = subprocess.run(["git", "-C", repo, "submodule", "foreach", "--recursive", "--quiet", "echo $displaypath"],
                         capture_output=True, text=True).stdout
    return [os.path.join(repo, p.strip()) for p in out.splitlines() if p.strip()]


def fix(repo):
    fixed = 0
    for rel in links(repo):
        link = os.path.join(repo, rel)
        if not os.path.isfile(link) or os.path.islink(link) or os.path.getsize(link) > 1024:
            continue
        with open(link, "r", encoding="utf-8", errors="replace") as f:
            target = f.read().strip()
        src = os.path.normpath(os.path.join(os.path.dirname(link), target))
        if os.path.isdir(src):
            os.remove(link)
            shutil.copytree(src, link)
        elif os.path.isfile(src):
            shutil.copyfile(src, link)
        else:
            print(f"missing target: {rel} -> {target}")
            continue
        fixed += 1
    return fixed


root = os.path.abspath(sys.argv[1])
total = 0
for repo in [root] + submodules(root):
    n = fix(repo)
    if n:
        print(f"{n:4} fixed in {os.path.relpath(repo, root)}")
    total += n
print(f"total fixed: {total}")
