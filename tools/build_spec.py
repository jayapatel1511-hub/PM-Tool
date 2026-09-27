#!/usr/bin/env python3
"""Rebuild the combined specification and its HTML page from spec-parts/.

    python3 tools/build_spec.py           rewrite the combined .md and .html
    python3 tools/build_spec.py --check   exit 1 if either is out of date

spec-parts/*.md is the source. The combined Markdown adds the table of contents; the HTML
page keeps its title block and styles and regenerates the navigation and body with pandoc
(https://pandoc.org), reproducing the original generator's output, including its quirks.
"""
import pathlib
import re
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
PARTS = ROOT / "spec-parts"
MD = ROOT / "Engineering-Project-Coordination-Hub-Specification.md"
HTML = ROOT / "Engineering-Project-Coordination-Hub-Specification.html"
LABELS = [("MVP-Required", "req"), ("MVP-Recommended", "rec"), ("Phase 2", "p2"), ("Phase 3", "p3"),
          ("Out of Scope", "out"), ("P2", "p2"), ("P3", "p3")]
TBD = "TBD — Business Decision Required"


def slug(text):
    return re.sub(r"[\s-]+", "-", re.sub(r"[^\w\s-]", "", text.lower()).strip())


def combined_markdown():
    parts = [p.read_text(encoding="utf-8") for p in sorted(PARTS.glob("*.md"))]
    text = "\n\n".join(parts)
    first = text.index("\n## 1. ") + 1
    heads = re.findall(r"^## (.+)$", text[first:], flags=re.M)
    toc = "## Table of Contents\n\n" + "".join(f"- [{h}](#{slug(h)})\n" for h in heads) + "\n---\n\n"
    return text[:first] + toc + text[first:]


def body_html(md):
    body = md[md.index("### How to read this document"):]
    body = re.sub(r"## Table of Contents\n.*?\n---\n", "", body, count=1, flags=re.S)
    # The original page was made with Python-Markdown, where a list needs a blank line before it
    # and nested lists need 4 spaces; anything else stays paragraph text. Reproduce that.
    item = re.compile(r"^(\s*)([-*+]|\d+\.)(\s)")

    def literal(line):
        return item.sub(lambda m: m.group(1) + (m.group(2)[:-1] + "\\." if m.group(2)[0].isdigit()
                                                else "\\" + m.group(2)) + m.group(3), line, count=1)

    out, prev, in_list, fence = [], "", False, False
    for line in body.split("\n"):
        if line.startswith("```"):
            fence = not fence
        elif not fence:
            m = item.match(line)
            if not line.strip():
                in_list = False
            elif m and in_list and 0 < len(m.group(1)) < 4:
                line = f"ZZSP{len(m.group(1))}ZZ" + literal(line).lstrip()
            elif m and (not prev.strip() or in_list or prev.startswith("#")):
                in_list = True
            elif m:
                line = literal(line)
        out.append(line)
        prev = line
    body = "\n".join(out)
    body = re.sub(r"(?m)^(\|.*)$", lambda m: m.group(1).replace("\\|", "ZZPIPEZZ"), body)
    h = subprocess.run(["pandoc", "-f", "gfm", "-t", "html", "--wrap=preserve", "--syntax-highlighting=none"],
                       input=body, capture_output=True, text=True, check=True).stdout

    h = h.replace("︎", "").replace("ZZPIPEZZ", "\\|")
    h = re.sub(r"ZZSP(\d)ZZ", lambda m: " " * int(m.group(1)), h)
    h = re.sub(r'<pre class="(?!mermaid)([\w-]+)"><code>', r'<pre><code class="language-\1">', h)
    h = re.sub(r'(<h[234] id=")([^"]+)"', lambda m: m.group(1) + re.sub(r"-{2,}", "-", m.group(2)) + '"', h)
    h = h.replace("<table>", '<div class="tbl"><table>').replace("</table>", "</table></div>")

    def h2(m):
        ident, text = m.groups()
        if n := re.match(r"(\d+)\.\s+(.*)", text, re.S):
            return f'<h2 id="{ident}"><span class="num">{n[1]}</span><span class="ttl">{n[2]}</span></h2>'
        if a := re.match(r"Appendix ([A-Z]) — (.*)", text, re.S):
            return f'<h2 id="{ident}"><span class="num">Appendix {a[1]}</span><span class="ttl">{a[2]}</span></h2>'
        return f'<h2 id="{ident}">{text}</h2>'

    h = re.sub(r'<h2 id="([^"]+)">(.*?)</h2>', h2, h, flags=re.S)
    h = re.sub(r'<h3 id="([^"]+)">(\d+\.\d+)\s+(.*?)</h3>', r'<h3 id="\1"><span class="num">\2</span> \3</h3>', h, flags=re.S)
    for code, cls in LABELS[:5]:
        h = h.replace(f"<strong>[{code}]</strong>", f'<span class="tag {cls}">{code}</span>')
    h = h.replace("<strong>Recommendation:</strong>", '<strong class="lbl rec">Recommendation</strong>')
    h = h.replace("<strong>Assumption:</strong>", '<strong class="lbl asm">Assumption</strong>')
    h = h.replace(f"<strong>{TBD}</strong>", f'<span class="tbd">{TBD}</span>')
    h = h.replace(f"<strong>{TBD}:</strong>", f'<strong><span class="tbd">{TBD}</span>:</strong>')
    h = re.sub(r"<strong>(TBD(?: — [^<:]*)?)</strong>", r'<span class="tbd">\1</span>', h)
    for code, cls in LABELS:
        h = h.replace(f"[{code}]", f'<span class="tag {cls}">{code}</span>')
    h = re.sub(r"<blockquote>\n<p><strong>Design note\.</strong>",
               '<blockquote class="note"><p><strong class="lbl note">Design note</strong>', h)
    h = re.sub(r'<pre class="mermaid"><code>(.*?)</code></pre>',
               lambda m: f'<div class="fig"><pre class="mermaid">{m[1]}\n</pre></div>', h, flags=re.S)
    h = re.sub(r'(<pre(?: class="[^"]*")?><code(?: class="[^"]*")?>)(.*?)</code></pre>',
               lambda m: f"{m[1]}{m[2]}\n</code></pre>", h, flags=re.S)
    h = h.replace('<ol type="1">', "<ol>")
    return re.sub(r"<li><p>(.*?)</p></li>", lambda m: f"<li>\n<p>{m[1]}</p>\n</li>", h, flags=re.S)


def nav_html(body):
    links, group = [], "numbered"
    for ident, inner in re.findall(r'<h2 id="([^"]+)">(.*?)</h2>', body, flags=re.S):
        num = re.search(r'<span class="num">(?:Appendix )?([^<]+)</span>', inner)
        title = re.sub(r"<[^>]+>", "", re.sub(r'<span class="num">.*?</span>', "", inner))
        kind = "appendix" if inner.startswith('<span class="num">Appendix') else "numbered" if num else "other"
        if kind != group:
            links.append('<div class="sep"></div>')
            group = kind
        links.append(f'<a href="#{ident}"><span class="n">{num[1] if num else "›"}</span><span>{title}</span></a>')
    return "\n".join(links) + "\n"


def page(md):
    old = HTML.read_text(encoding="utf-8")
    body = body_html(md)
    nav_start = old.index("<h2>Sections</h2>\n") + len("<h2>Sections</h2>\n")
    nav_end = old.index("    </div></details>", nav_start)
    a = old.index("<main>") + len("<main>")
    b = old.index("</main>")
    inner = old[a:b]
    lead, trail = inner[:len(inner) - len(inner.lstrip())], inner[len(inner.rstrip()):]
    return old[:nav_start] + nav_html(body) + old[nav_end:a] + lead + body.strip() + trail + old[b:]


def main():
    md = combined_markdown()
    html = page(md)
    stale = [p.name for p, new in ((MD, md), (HTML, html)) if p.read_text(encoding="utf-8") != new]
    if "--check" in sys.argv:
        print("out of date: " + ", ".join(stale) if stale else "up to date")
        return 1 if stale else 0
    MD.write_text(md, encoding="utf-8")
    HTML.write_text(html, encoding="utf-8")
    print("rebuilt: " + ", ".join(stale) if stale else "already up to date")
    return 0


if __name__ == "__main__":
    sys.exit(main())
