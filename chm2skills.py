#!/usr/bin/env python3
"""
chm2skills — Full pipeline:  CHM  ->  Word/HTML  ->  categorize  ->  Agent Skills.

Builds on chm2word.py. For every .chm it:

  1. extracts the CHM and parses its table of contents (.hhc);
  2. splits the content into CATEGORIES using the CHM's own top-level
     TOC nodes (so the result is already "分类好的" / categorized);
  3. emits, per category, a Word .docx and an .html file, plus one
     full-document .docx per CHM;
  4. generates a Claude-Code-style SKILL directory per CHM:
        skills/<chm>/SKILL.md            (skill manifest + reference index)
        skills/<chm>/references/<cat>.md (Markdown the agent reads on demand)
     so an Agent can answer questions from this content.

Output layout (under OUTPUT):
    word/<chm>/_FULL.docx
    word/<chm>/<Category>.docx
    html/<chm>/<Category>.html
    skills/<chm-slug>/SKILL.md
    skills/<chm-slug>/references/<category-slug>.md

Usage:
    python chm2skills.py "C:\\Help\\E3_FDC.chm" "C:\\out"
    python chm2skills.py "C:\\Help"            "C:\\out"   # batch a folder

Run `python chm2skills.py --help` for options.
"""

from __future__ import annotations

import argparse
import html
import logging
import re
import shutil
import subprocess
import sys
import tempfile
import unicodedata
from dataclasses import dataclass, field
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import chm2word as c2w  # noqa: E402

log = logging.getLogger("chm2skills")


# --------------------------------------------------------------------------- #
# Helpers
# --------------------------------------------------------------------------- #

def slugify(text: str, fallback: str = "section") -> str:
    text = unicodedata.normalize("NFKC", text or "").strip().lower()
    text = re.sub(r"[^\w一-鿿]+", "-", text, flags=re.U)
    text = text.strip("-")
    return text or fallback


def safe_name(text: str, fallback: str = "section") -> str:
    """Filesystem-safe display name (keeps spaces & CJK, drops separators)."""
    text = (text or "").strip()
    text = re.sub(r'[<>:"/\\|?*\x00-\x1f]+', " ", text).strip()
    text = re.sub(r"\s+", " ", text)
    return text[:120] or fallback


# --------------------------------------------------------------------------- #
# Categorization
# --------------------------------------------------------------------------- #

@dataclass
class Category:
    title: str
    slug: str
    entries: list = field(default_factory=list)  # list[c2w.TocEntry]


def categorize(toc: list, split_level: str) -> list[Category]:
    """Group TOC entries into categories. A new category begins at every
    entry whose level is <= the cut depth; deeper entries belong to the
    category currently open."""
    if not toc:
        return []
    min_lvl = min(e.level for e in toc)
    if split_level == "auto":
        cut = min_lvl
        tops = [e for e in toc if e.level == min_lvl]
        if len(tops) <= 1 and any(e.level > min_lvl for e in toc):
            cut = min_lvl + 1  # single root -> split one level deeper
    else:
        cut = int(split_level)

    cats: list[Category] = []
    cur: Category | None = None
    used: set[str] = set()
    for e in toc:
        if e.level <= cut or cur is None:
            base = slugify(e.title)
            sl, i = base, 2
            while sl in used:
                sl, i = f"{base}-{i}", i + 1
            used.add(sl)
            cur = Category(safe_name(e.title), sl, [e])
            cats.append(cur)
        else:
            cur.entries.append(e)
    return cats


def rebase(entries: list) -> list:
    """Shift heading levels so each category starts at H1."""
    if not entries:
        return entries
    base = min(e.level for e in entries)
    return [c2w.TocEntry(max(1, min(6, e.level - base + 1)), e.title, e.local)
            for e in entries]


# --------------------------------------------------------------------------- #
# HTML -> Markdown (for skill reference files)
# --------------------------------------------------------------------------- #

def html_to_md(html_path: Path, md_path: Path, resource_root: Path,
                backend: str) -> None:
    if backend in ("auto", "pandoc") and c2w._which("pandoc"):
        subprocess.run(
            ["pandoc", str(html_path), "-f", "html", "-t", "gfm",
             "--wrap=none", "--markdown-headings=atx",
             "--resource-path", str(resource_root), "-o", str(md_path)],
            check=True, stderr=subprocess.PIPE)
        return
    if backend == "pandoc":
        raise RuntimeError("pandoc requested for markdown but not on PATH")
    md_path.write_text(_py_html_to_md(html_path), encoding="utf-8")


def _py_html_to_md(html_path: Path) -> str:
    """Modest pure-Python HTML->Markdown fallback (headings, lists, tables,
    code, emphasis). Used only when pandoc is unavailable."""
    try:
        from bs4 import BeautifulSoup  # type: ignore
    except Exception as e:  # pragma: no cover
        raise RuntimeError(
            f"python markdown fallback needs beautifulsoup4+lxml ({e})")

    soup = BeautifulSoup(html_path.read_text(encoding="utf-8"), "lxml")
    out: list[str] = []

    def inline(node) -> str:
        s = ""
        for c in node.children:
            nm = getattr(c, "name", None)
            if nm is None:
                s += html.unescape(str(c))
            elif nm in ("b", "strong"):
                s += f"**{inline(c)}**"
            elif nm in ("i", "em"):
                s += f"*{inline(c)}*"
            elif nm == "code":
                s += f"`{c.get_text()}`"
            elif nm == "br":
                s += "\n"
            elif nm == "a":
                s += inline(c)
            else:
                s += inline(c)
        return re.sub(r"[ \t]+", " ", s).strip()

    body = soup.body or soup
    for el in body.find_all(
            ["h1", "h2", "h3", "h4", "h5", "h6", "p", "ul", "ol",
             "pre", "table"], recursive=True):
        if el.find_parent(["ul", "ol", "table", "pre"]):
            continue
        if el.name and el.name[0] == "h" and el.name[1:].isdigit():
            out.append("#" * int(el.name[1]) + " " + el.get_text(strip=True))
        elif el.name == "p":
            t = inline(el)
            if t:
                out.append(t)
        elif el.name in ("ul", "ol"):
            for i, li in enumerate(el.find_all("li", recursive=False), 1):
                bullet = "- " if el.name == "ul" else f"{i}. "
                out.append(bullet + inline(li))
        elif el.name == "pre":
            out.append("```\n" + el.get_text() + "\n```")
        elif el.name == "table":
            rows = el.find_all("tr")
            if not rows:
                continue
            grid = [[c.get_text(" ", strip=True)
                     for c in r.find_all(["td", "th"])] for r in rows]
            ncol = max(len(r) for r in grid)
            grid = [r + [""] * (ncol - len(r)) for r in grid]
            out.append("| " + " | ".join(grid[0]) + " |")
            out.append("| " + " | ".join(["---"] * ncol) + " |")
            for r in grid[1:]:
                out.append("| " + " | ".join(r) + " |")
        out.append("")
    return "\n".join(out).strip() + "\n"


# --------------------------------------------------------------------------- #
# Skill generation
# --------------------------------------------------------------------------- #

def write_skill(skill_dir: Path, chm_title: str, cats: list[Category],
                 ref_files: dict[str, Path]) -> None:
    skill_dir.mkdir(parents=True, exist_ok=True)
    name = slugify(chm_title, "chm-help")
    topics = ", ".join(c.title for c in cats[:8])
    desc = (f"Use this skill when answering questions about {chm_title} "
            f"(converted from its CHM help). Covers: {topics}"
            f"{' and more' if len(cats) > 8 else ''}. Consult the matching "
            f"reference file in references/ before answering.")
    desc = desc.replace("\n", " ")

    lines = [
        "---",
        f"name: {name}",
        f"description: {desc}",
        "---",
        "",
        f"# {chm_title}",
        "",
        "Knowledge converted from the CHM help file. The full content of "
        "each topic area is in `references/`. To answer a question:",
        "",
        "1. Find the most relevant category below.",
        "2. Read its reference file in `references/`.",
        "3. Answer from that content; cite the category title.",
        "",
        "## Reference index",
        "",
    ]
    for c in cats:
        rel = ref_files.get(c.slug)
        if not rel:
            continue
        lines.append(f"- **{c.title}** — `references/{rel.name}` "
                     f"({len(c.entries)} topic(s))")
    lines.append("")
    (skill_dir / "SKILL.md").write_text("\n".join(lines), encoding="utf-8")
    log.info("wrote skill manifest %s/SKILL.md", skill_dir.name)


# --------------------------------------------------------------------------- #
# Per-CHM pipeline
# --------------------------------------------------------------------------- #

def process_chm(chm: Path, out: Path, args) -> None:
    stem = chm.stem
    word_dir = out / "word" / safe_name(stem)
    html_dir = out / "html" / safe_name(stem)
    skill_dir = out / "skills" / slugify(stem, "chm-help")
    refs_dir = skill_dir / "references"
    for d in (word_dir, html_dir, refs_dir):
        d.mkdir(parents=True, exist_ok=True)

    tmp = Path(tempfile.mkdtemp(prefix="chm2skills_"))
    try:
        extract_dir = tmp / "chm"
        c2w.extract_chm(chm, extract_dir, args.backend)

        hhc = c2w.find_hhc(extract_dir)
        toc = c2w.parse_hhc(hhc) if hhc else c2w.fallback_toc(extract_dir)
        log.info("%s: %d TOC entries (%s)", stem, len(toc),
                 hhc.name if hhc else "no .hhc, flat order")

        # full-document Word (the original CHM->Word deliverable)
        full_asm = c2w.assemble(extract_dir, toc, stem, tmp / "_full.html")
        full_docx = word_dir / "_FULL.docx"
        if args.overwrite or not full_docx.exists():
            c2w.to_docx(full_asm, full_docx, args.docx_backend, add_toc=True)
            log.info("wrote %s", full_docx.relative_to(out))

        # categorized outputs + skill references
        cats = categorize(toc, args.split_level)
        log.info("%s: %d categories", stem, len(cats))
        ref_files: dict[str, Path] = {}
        for c in cats:
            ent = rebase(c.entries)
            asm = c2w.assemble(extract_dir, ent, c.title,
                               tmp / f"cat_{c.slug}.html")

            cat_html = html_dir / f"{safe_name(c.title)}.html"
            shutil.copyfile(asm.html_path, cat_html)

            cat_docx = word_dir / f"{safe_name(c.title)}.docx"
            if args.overwrite or not cat_docx.exists():
                try:
                    c2w.to_docx(asm, cat_docx, args.docx_backend, add_toc=False)
                except Exception as e:
                    log.error("docx failed for category '%s': %s", c.title, e)

            md = refs_dir / f"{c.slug}.md"
            try:
                html_to_md(asm.html_path, md, extract_dir, args.md_backend)
                ref_files[c.slug] = md
            except Exception as e:
                log.error("markdown failed for category '%s': %s", c.title, e)

        write_skill(skill_dir, stem, cats, ref_files)
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


# --------------------------------------------------------------------------- #
# CLI
# --------------------------------------------------------------------------- #

def build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(
        prog="chm2skills",
        description="CHM -> Word/HTML -> categorized -> Agent Skills pipeline.")
    p.add_argument("input", type=Path,
                   help="a .chm file OR a directory containing .chm files")
    p.add_argument("output", type=Path, help="output root directory")
    p.add_argument("--backend",
                   choices=["auto", "hh", "7z", "chmlib", "pychm"],
                   default="auto", help="CHM extraction backend (default auto)")
    p.add_argument("--docx-backend", choices=["auto", "pandoc", "python"],
                   default="auto", help="Word backend (default auto)")
    p.add_argument("--md-backend", choices=["auto", "pandoc", "python"],
                   default="auto", help="Markdown backend (default auto)")
    p.add_argument("--split-level", default="auto",
                   help="category depth: 'auto', or a TOC level like 1 / 2")
    p.add_argument("--no-recurse", dest="recurse", action="store_false",
                   help="do not recurse into subfolders when input is a dir")
    p.add_argument("--overwrite", action="store_true",
                   help="overwrite existing outputs")
    p.add_argument("-v", "--verbose", action="store_true")
    return p


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    logging.basicConfig(
        level=logging.DEBUG if args.verbose else logging.INFO,
        format="%(levelname)s: %(message)s")

    if not args.input.exists():
        log.error("input not found: %s", args.input)
        return 2
    chms = c2w.gather_chms(args.input, args.recurse)
    if not chms:
        log.error("no .chm files found at %s", args.input)
        return 2
    log.info("found %d CHM file(s)", len(chms))

    ok, failed = 0, []
    for chm in chms:
        log.info("=== %s", chm.name)
        try:
            process_chm(chm, args.output, args)
            ok += 1
        except Exception as e:
            log.error("FAILED %s: %s", chm.name, e)
            failed.append(chm.name)

    log.info("done: %d ok, %d failed", ok, len(failed))
    for f in failed:
        log.info("  failed: %s", f)
    return 0 if not failed else 1


if __name__ == "__main__":
    sys.exit(main())
