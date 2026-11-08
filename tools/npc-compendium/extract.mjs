#!/usr/bin/env node
/**
 * Finds every NPC stat block in the Cyberpunk RED PDFs and writes a draft for the compendium:
 *
 * - tools/npc-compendium/out/blocks.json: one entry per block, with the header fields, the STAT row and the raw
 *   text of the block (up to the next block or the end of the page);
 * - tools/npc-compendium/out/report.txt: per book, how many blocks were found, the names, the possible generic
 *   sheets, the blocks that could not be fully read and the tables left out on purpose.
 *
 * The goal is that no sheet goes unnoticed, not perfect parsing: Task 8 fills data/source/npc-compendium.json by hand
 * from the PDFs, using this as the checklist.
 *
 * Run: node tools/npc-compendium/extract.mjs ["<folder with the PDFs>"]
 * Needs pdftotext (xpdf or poppler) on PATH. Default folder: %USERPROFILE%/Downloads/Cyberpunk PDFs.
 *
 * Blocks are found by their STAT row (INT REF DEX TECH COOL WILL MOVE BODY EMP), because the header changes from book to book:
 *   M: the number line "6 8 6 5 4 6 5 6 4 INT", labels on the next line (Mission Kit, Interface RED, Tales of the RED);
 *   N: the same, when only the label line can be recognised;
 *   I: "INT 5 REF 8(6) DEX 6(4) ..." inline (Danger Gal Dossier);
 *   C: "4 > INT  6 > REF ..." in two rows with Hit Points below (Core mooks, Interface RED 2 monsters).
 * Character creation tables ("Roll INT REF DEX ..."), the empty STAT diagram, STAT examples without Hit Points and blank
 * template sheets are not NPCs: they are left out and listed in the report.
 * Names of Danger Gal Dossier / Black Chrome sheets are big headings above the bio and are guesses; so are names whose
 * drop cap broke ("F name ife"): both are listed in the report. printedPage comes from the page footers, with
 * PAGE_OFFSET as fallback. preText is the page text above the first block of a page (bio of the sheet in the Dossier).
 */
import { execFileSync } from 'node:child_process';
import { mkdirSync, readdirSync, writeFileSync } from 'node:fs';
import { homedir } from 'node:os';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const PDFS = resolve(process.argv[2] ?? resolve(homedir(), 'Downloads', 'Cyberpunk PDFs'));
const OUT = resolve(here, 'out');

/** Printed page = PDF page + offset, used when the page footer cannot be read. Key: lowercase part of the file name. */
const PAGE_OFFSET = {
  'edgerunners-mission-kit': -85, // the footers are not text in this PDF; checked on Adam Smasher (PDF 123 = p.38) and Falco
  'karoake': 0, // no readable footer either; the page cannot be checked
};

/** Display name of a book, from its file name. */
const BOOKS = [
  [/edgerunners-mission-kit/i, 'Edgerunners Mission Kit'],
  [/danger gal dossier/i, 'Danger Gal Dossier'],
  [/DangerGalDossierPlus/i, 'Danger Gal Dossier+'],
  [/HopeRebornPlus/i, 'Hope Reborn+'],
  [/TotRPlus/i, 'Tales of the RED+'],
  [/tales of the red/i, 'Tales of the RED'],
  [/volume 01/i, 'Interface RED 1'],
  [/volume 2/i, 'Interface RED 2'],
  [/vol_3/i, 'Interface RED 3'],
  [/volume 4/i, 'Interface RED 4'],
  [/interface-5/i, 'Interface RED 5'],
  [/CyberpunkRedCore/i, 'Core Book'],
  [/Black Chrome \[/i, 'Black Chrome'],
  [/BlackChromePlus/i, 'Black Chrome+'],
  [/Karoake/i, 'Karaoke Night'],
  [/MixingDrinks/i, 'Mixing Drinks'],
];

const STATS = ['INT', 'REF', 'DEX', 'TECH', 'COOL', 'WILL', 'MOVE', 'BODY', 'EMP'];
const GENERIC = /\b(mooks?|gangers?|officers?|guards?|security|crews?|soldiers?|operatives?|enemy netrunner|bodyguards?|grunts?|thugs?|goons?|troopers?|cops?|recruits?|punks?|boosters?|reclaimers?|agents?|assassins?|pilots?|medtechs?)\b/i;

function pdftotext(file) {
  const out = execFileSync('pdftotext', ['-layout', '-enc', 'UTF-8', resolve(PDFS, file), '-'], { maxBuffer: 256 * 1024 * 1024 });
  return out.toString('utf8').replace(/\r/g, '');
}

const bookOf = (file) => BOOKS.find(([rx]) => rx.test(file))?.[1] ?? file.replace(/\.pdf$/i, '').replace(/^RTG-C\w+-(?:DLC-)?/, '');

// STAT rows

const VALUE = /-?\d+\s*\(\d+\)|-?\d+|[—–]/g;
const tokens = (text) => [...text.matchAll(VALUE)].map((m) => m[0]);

/** "8 (6)" -> [8, 6]; "—" -> [null, null]. */
function value(token) {
  const m = token.match(/^(-?\d+)\s*\((\d+)\)$/);
  if (m) return [Number(m[1]), Number(m[2])];
  return /^-?\d+$/.test(token) ? [Number(token), null] : [null, null];
}

function putStat(into, label, token) {
  const [base, armored] = value(token);
  into.stats[label] = base;
  if (armored !== null) into.armoredStats[label] = armored;
}

/**
 * Labels with their values around them ("4 > INT  > REF 8 (6)" or "INT 5 REF 8(6)"): a value belongs to the label
 * before it when that one has none yet, else to the label after it.
 */
function labelled(text, labels) {
  const found = [...text.matchAll(new RegExp(`\\b(${labels.join('|')})\\b`, 'g'))];
  if (!found.length) return null;
  const gaps = [];
  let from = 0;
  for (const m of found) {
    gaps.push(text.slice(from, m.index).replace(/[▶►]/g, ' '));
    from = m.index + m[0].length;
  }
  gaps.push(text.slice(from).replace(/[▶►]/g, ' '));
  const vals = new Array(found.length).fill(undefined);
  gaps.forEach((gap, k) => {
    let toks = tokens(gap);
    if (k === 0) toks = toks.slice(-1); // anything earlier is a page number or a name
    if (k > 0 && vals[k - 1] === undefined && toks.length) vals[k - 1] = toks.shift();
    if (k < found.length && toks.length) vals[k] = toks[toks.length - 1];
  });
  return found.map((m, k) => [m[1], vals[k]]);
}

/** Reads the STAT row that starts at lines[row]; returns { stats, armoredStats, start, end, issue? }. */
function readStatRow(lines, kind, row) {
  const into = { stats: {}, armoredStats: {} };
  if (kind === 'N' || kind === 'M') {
    // M: the number line itself ("6 8 6 5 4 6 5 6 4 INT"); N: the label line, with the numbers above or below it
    const clean = (l) => l.replace(/see page \d+ for biography/i, ' ').replace(/\b(INT|WILL|MOVE|BODY|EMP)\b/g, ' ');
    const cand = kind === 'M' ? [row] : [row - 1, row - 2, row + 1, row + 2].filter((i) => lines[i] !== undefined && !/\bRoll\b/i.test(lines[i]));
    const numRow = cand.find((i) => tokens(clean(lines[i])).length >= 8 && !/[a-z]{3}/.test(clean(lines[i])));
    if (numRow === undefined) return { ...into, start: row, end: row, issue: 'STAT numbers not found next to the labels' };
    const toks = tokens(clean(lines[numRow]));
    const span = { start: Math.min(row, numRow), end: Math.max(row, numRow) };
    if (toks.length !== 9) return { ...into, ...span, issue: `STAT row has ${toks.length} numbers instead of 9` };
    STATS.forEach((label, i) => putStat(into, label, toks[i]));
    return { ...into, ...span };
  }
  // I / C: labels and values on the line; when EMP is not on it, the next non-empty line carries WILL LUCK MOVE BODY EMP
  let end = row;
  const rows = [lines[row]];
  if (!/\bEMP\b/.test(lines[row])) for (let i = row + 1; i <= row + 3 && rows.length < 2; i++) if (lines[i]?.trim()) { rows.push(lines[i]); end = i; }
  for (const text of rows) {
    const got = labelled(text, [...STATS, 'LUCK']);
    if (got) for (const [label, tok] of got) if (tok !== undefined) putStat(into, label, tok);
  }
  return { ...into, start: row, end, twoRows: rows.length > 1 };
}

// anchors

const hasAll = (line, words) => words.every((w) => new RegExp(`\\b${w}\\b`).test(line));
const ROLL_TABLE = /\bRoll\b.*\bINT\b.*\bREF\b/;

/** What kind of STAT-row line is this? null when it is none. */
function anchorKind(line) {
  if (ROLL_TABLE.test(line)) return null;
  if (/[▶►]\s*INT\b/.test(line)) return 'C';
  if (/\bINT\s+[\d—–]/.test(line) && /\bREF\s+[\d—–]/.test(line)) return 'I';
  // the number line: only numbers and STAT labels, at least five numbers
  const bare = line.replace(/see page \d+ for biography/i, ' ').replace(/\b(INT|REF|DEX|TECH|COOL|WILL|MOVE|BODY|EMP)\b/g, ' ');
  if (/\bINT\b/.test(line) && tokens(bare).length >= 5 && /^[\s\d()—–-]*$/.test(bare)) return 'M';
  if (hasAll(line, ['REF', 'DEX', 'TECH', 'COOL', 'MOVE', 'EMP']) && !/\d/.test(line)) return 'N';
  return null;
}

// header

const HEADER_START = /^\s*(?:[A-Za-z]\s+)?(?:name|level|roles?)\b/i;
/** A sheet header: a NAME / LEVEL line with the "Seriously ... HP" box on it or within the next three lines (its tokens may sit on different lines). */
const HEADER_LINE = /^\s*(?:[A-Za-z]\s+){0,3}(?:name|level)\b/i;
function isHeader(lines, i) {
  return HEADER_LINE.test(lines[i] ?? '') && /Seriously\s+(?:HP\b|[\d—]+\s+[\d—]+)|[\d—]+\s+Seriously\s+HP\b/i.test(lines.slice(i, i + 4).join('\n'));
}
const HP_LINE = /([\d—]+)\s+([\d—]+)\s+HP\b/;
const num = (s) => (s === undefined || !/^\d+$/.test(s) ? null : Number(s));
const seg = (s) => s.trim().split(/\s{2,}/)[0].trim();

const NOT_ROLE = /^(wounded|save|death\b.*|\d+(\s+death.*)?)$/i;

/** Text after a label such as "level" / "role"; a dropped initial ("B level oss") is put back. */
function afterLabel(line, label) {
  const m = line.match(new RegExp(`^\\s*([A-Za-z])?\\s*${label}\\b\\s*(.*)`, 'i'));
  const rest = seg(m?.[2] ?? '');
  return m?.[1] && /^[a-z]/.test(rest) ? m[1] + rest : rest;
}

function header(lines, from, to) {
  const win = lines.slice(from, to);
  const text = win.join('\n');
  const h = {};
  const hp = text.match(HP_LINE);
  if (hp) [h.hp, h.seriouslyWounded] = [num(hp[1]), num(hp[2])];
  else {
    // "10 Seriously     HP" then "wounded     20": Seriously Wounded first, HP on the next line
    const split = text.match(/(\d+)\s+Seriously\s+HP[\s\S]{0,120}?wounded\s+(\d+)/i);
    if (split) [h.hp, h.seriouslyWounded] = [num(split[2]), num(split[1])];
  }
  const rep = text.match(/\b(\d+)\s*rep\b/i) ?? text.match(/\(rep\s*(\d+)\)/i) ?? text.match(/\brep\s+(\d+)/i) ?? text.match(/^\s*(\d+)\s+Death\b/im);
  if (rep) h.rep = Number(rep[1]);
  const lvlAt = win.findIndex((l) => /^\s*(?:[A-Za-z]\s+)?level\b/i.test(l));
  if (lvlAt >= 0) h.level = afterLabel(win[lvlAt], 'level');
  const roleAt = win.findIndex((l) => /^\s*(?:[A-Za-z]\s+)?roles?\b/i.test(l));
  if (roleAt >= 0) {
    // the role may sit on the label line or on the lines below it (Mission Kit, multi-role sheets)
    const parts = [afterLabel(win[roleAt], 'roles?')];
    for (let i = roleAt + 1; i <= roleAt + 3 && win[i]?.trim() && !/STATS/.test(win[i]); i++) parts.push(seg(win[i]));
    h.role = parts.filter((p) => p && !NOT_ROLE.test(p)).join(' / ') || undefined;
  }
  // Death Save: "Death 7" / "DEATH 6" on the line, else a lone number on the line next to "Death"
  const death = text.match(/\bDEATH\s+([\d—–]+)/i);
  if (death) h.deathSave = num(death[1]);
  else {
    const at = win.findIndex((l) => /\bDEATH\b/i.test(l));
    if (at >= 0) {
      const lone = [win[at - 1], win[at + 1]].find((l) => l && /^\s*\d+\s*$/.test(l));
      const trailing = win[at - 1]?.match(/\s{5,}(\d+)\s*$/)?.[1]; // "ROLE ... Nomad (...)      7" then "DEATH"
      if (lone) h.deathSave = Number(lone.trim());
      else if (trailing) h.deathSave = Number(trailing);
    }
  }
  const nameAt = win.findIndex((l) => /^\s*(?:[A-Za-z]\s+){0,3}name\b/i.test(l));
  if (nameAt >= 0) {
    const m = win[nameAt].match(/^\s*((?:[A-Za-z]\s+){0,3})name\b\s*(.*)/i);
    const rest = seg(m[2]).replace(/\s*\(\s*rep\s*\d+\s*\)\s*$/i, '').trim();
    const initials = m[1].replace(/\s/g, '');
    const dropCap = initials && /^[a-z]/.test(rest);
    h.name = dropCap ? initials + rest : rest;
    if (dropCap) h.nameGarbled = true; // initials of a drop cap, put back by guess
  } else {
    // Mission Kit sheet printed next to a text column: "...text  NAME     Meatwagon Gonk     0 REP"
    const m = text.match(/\bNAME\s{2,}(.+?)\s{2,}\d+\s+REP\b/);
    if (m) h.name = m[1].trim();
  }
  if ((h.name?.match(/(^|\s)[A-Za-z](?=\s|$)/g) ?? []).length >= 2) h.nameGarbled = true;
  return h;
}

const LABELS = /^(INT|REF|DEX|TECH|COOL|WILL|MOVE|BODY|EMP|LUCK)(\s+(INT|REF|DEX|TECH|COOL|WILL|MOVE|BODY|EMP|LUCK))*$/;
const NOT_NAME = /^(STATS|WEAPONS|ARMOR|GEAR|CYBERWARE|SKILL BASES|BODY HEAD|BODY|HEAD|ROF\d?|REF|DEX|INT|TECH|COOL|WILL|MOVE|EMP|LUCK|ATTACKS|IMPORTANT CYBERWARE)$/;

/**
 * Name of a Danger Gal Dossier / Black Chrome sheet, which is only a big heading above the bio: the first ALL-CAPS
 * (or "> Name") text between from and to. The running head on the first line of a page is skipped.
 */
function headingAbove(lines, from, to, pageStart) {
  const found = [];
  for (let i = Math.max(0, from); i < to; i++) {
    for (const s of (lines[i] ?? '').trim().split(/\s{3,}/)) {
      if (/^[\p{Lu}0-9][\p{Lu}0-9 .'’"&!\-]{2,}$/u.test(s) && (s.match(/\p{Lu}/gu) ?? []).length >= 3 && !NOT_NAME.test(s) && !LABELS.test(s)) found.push({ i, s });
      else if (/^[▶►]\s*\S/.test(s) && s.length < 40) found.push({ i, s: s.replace(/^[▶►]\s*/, '') });
    }
  }
  const firstLine = lines.findIndex((l) => l.trim());
  const named = pageStart ? found.filter((c) => c.i !== firstLine) : found;
  return (named[0] ?? found[0])?.s ?? null;
}

/** Core mook / Interface RED 2 sheets: the name is the text that starts a line of the block ("security officer  4 > INT ..."). */
function rowName(lines, from, to) {
  for (let i = Math.max(0, from); i <= to; i++) {
    const m = (lines[i] ?? '').match(/^\s*(?:\d{1,3}\s+)?([A-Za-z][A-Za-z' ]*?)\s{2,}(?:\d+\s+)?[▶►\d]/);
    if (m && !/^(Info on|Weapon|Quality|Hit|Seriously|Death|Armor|Skill|Cyberware)/i.test(m[1]) && !LABELS.test(m[1].trim())) return m[1].trim();
  }
  // monsters (Interface RED 2): "Bearwolf  Skill   Athletics 11, ..." or "Draglin   Abilities  ..." further down
  for (let i = Math.max(0, from); i <= to + 22; i++) {
    const m = (lines[i] ?? '').match(/^\s{0,8}([A-Z][A-Za-z' -]+?)\s{2,}(?:Skill|Abilities)\b/);
    if (m) return m[1].trim();
  }
  return null;
}

// page numbers

/** Lone numbers in the first and last lines of the page: candidates for the printed page. */
function footerNumbers(page) {
  const lines = page.split('\n').filter((l) => l.trim());
  return [...lines.slice(0, 3), ...lines.slice(-4)].map((l) => l.trim().match(/^(\d{1,3})$/)?.[1]).filter(Boolean).map(Number);
}

function pageOffsets(pages, fallback) {
  const votes = new Map();
  pages.forEach((p, i) => { for (const n of footerNumbers(p)) votes.set(n - (i + 1), (votes.get(n - (i + 1)) ?? 0) + 1); });
  const best = [...votes].sort((a, b) => b[1] - a[1])[0];
  const mode = best && best[1] >= 3 ? best[0] : fallback;
  return pages.map((p, i) => {
    const hit = footerNumbers(p).find((n) => Math.abs(n - (i + 1) - mode) <= 1 && n - (i + 1) === mode);
    return hit ?? i + 1 + mode;
  });
}

// scan

function scan(file) {
  const book = bookOf(file);
  const pages = pdftotext(file).split('\f');
  const fallback = Object.entries(PAGE_OFFSET).find(([k]) => file.toLowerCase().includes(k))?.[1] ?? 0;
  const printed = pageOffsets(pages, fallback);
  const blocks = [];
  const excluded = [];
  const warnings = [];
  pages.forEach((page, p) => {
    const lines = page.split('\n');
    const anchors = [];
    lines.forEach((line, i) => {
      if (ROLL_TABLE.test(line) && /\bEMP\b/.test(line)) { excluded.push({ printedPage: printed[p], what: 'character creation table (Roll column)' }); return; }
      const kind = anchorKind(line);
      if (!kind) return;
      if (kind === 'C' || (kind === 'I' && !/\bEMP\b/.test(line))) {
        // two-row STAT blocks (Core mooks, Interface RED 2) are followed by Hit Points; examples and diagrams are not
        const near = lines.slice(i, i + 8).join('\n');
        const filled = tokens(line.replace(/[▶►]/g, ' ')).length;
        if (!/\bHit\b/i.test(near)) { excluded.push({ printedPage: printed[p], what: filled ? 'STAT block example without Hit Points (character creation)' : 'empty STAT diagram' }); return; }
      }
      if (kind === 'N' && anchors.some((a) => a.kind === 'M' && a.row >= i - 2 && a.row < i)) return; // its numbers are already an M anchor
      anchors.push({ kind, row: i });
    });
    // sheet headers without a STAT row nearby: counted, so nothing is lost
    lines.forEach((line, i) => {
      if (!isHeader(lines, i)) return;
      if (anchors.some((a) => a.row >= i && a.row <= i + 14)) return;
      anchors.push({ kind: null, row: i + 7, hdr: i });
    });
    anchors.sort((a, b) => a.row - b.row);
    const used = new Set(); // header lines already taken by a block
    let prevEnd = 0;
    const read = [];
    for (const a of anchors) {
      const r = a.kind ? readStatRow(lines, a.kind, a.row) : { stats: {}, armoredStats: {}, start: a.row, end: a.row, issue: 'header without a STAT row' };
      const regionFrom = prevEnd;
      const twoRows = a.kind === 'C' || r.twoRows;
      // the header is the nearest NAME / LEVEL line above the STAT row that no other block took (sheets side by side
      // share their lines, so the search is not cut at the previous block)
      let hs = -1;
      if (a.hdr !== undefined) { hs = a.hdr; used.add(hs); } else if (!twoRows) {
        for (let i = r.start - 1; i >= Math.max(0, r.start - 30); i--) {
          if (/^\s*(?:[A-Za-z]\s+){0,3}(?:name|level)\b/i.test(lines[i]) && !used.has(i)) { hs = i; break; }
        }
        for (let i = hs - 1; hs >= 0 && i >= Math.max(0, hs - 3); i--) {
          if (/^\s*(?:[A-Za-z]\s+){0,3}name\b/i.test(lines[i]) && !used.has(i)) { hs = i; break; } // "Name" above "Level" (Interface RED 5)
        }
        if (hs < 0) hs = lines.findIndex((l, i) => i >= Math.max(prevEnd, r.start - 16) && i < r.start && (HEADER_START.test(l) || HP_LINE.test(l)));
        if (hs < 0) hs = Math.max(prevEnd, r.start - 6);
        used.add(hs);
      } else hs = r.start;
      prevEnd = r.end + 1;
      read.push({ a, r, hs, regionFrom, twoRows });
    }
    // a sheet with a STAT row but no numbers and no HP is a blank form (Interface RED templates)
    const blank = (x) => x.r.issue?.startsWith('STAT numbers not found') && !HP_LINE.test(lines.slice(x.hs, x.r.start + 1).join('\n'));
    read.filter(blank).forEach(() => excluded.push({ printedPage: printed[p], what: 'blank NPC template sheet' }));
    const sheets = read.filter((x) => !blank(x));
    const starts = sheets.map((x) => x.hs);
    // guard: every header line of the page must belong to a block (or to a blank template sheet)
    const headers = lines.map((_, i) => i).filter((i) => isHeader(lines, i));
    const owned = new Set(read.map((x) => x.hs));
    for (const i of headers) if (!owned.has(i)) warnings.push(`p.${printed[p]} (pdf ${p + 1}): header line "${lines[i].trim().slice(0, 50)}" belongs to no block`);
    sheets.forEach(({ a, r, hs, regionFrom, twoRows }, k) => {
      const end = Math.min(...starts.filter((s) => s > hs), lines.length);
      let h;
      if (twoRows) {
        const block = lines.slice(r.start, r.end + 12).join('\n');
        const pick = (rx) => num(block.match(rx)?.[1]);
        h = {
          hp: pick(/\bHit\s+(?:Points\s+)?(\d+)/i),
          seriouslyWounded: pick(/\bSer\w*\s+(?:Wounded\s+)?(\d+)/i),
          deathSave: pick(/\bDeath\s+(?:Save\s+)?(\d+)/i),
          name: rowName(lines, r.start - 1, r.end + 6) ?? undefined,
        };
        if (h.deathSave === null) h.deathSave = /\bDeath\b/i.test(block) ? undefined : null; // monsters print no Death Save
      } else h = header(lines, hs, r.start);
      const heading = h.name || twoRows ? null : headingAbove(lines, regionFrom, hs, k === 0);
      const issues = [];
      if (r.issue) issues.push(r.issue);
      if (a.kind && STATS.some((s) => !(s in r.stats) && !(s === 'EMP' && twoRows))) issues.push(`STAT row incomplete (missing ${STATS.filter((s) => !(s in r.stats)).join(', ')})`);
      if (h.hp === undefined || h.hp === null) issues.push('HP not read');
      if (h.deathSave === undefined) issues.push('Death Save not read');
      if (!h.name && !heading) issues.push('name not found');
      if (!h.name && heading) issues.push('name taken from the page heading');
      if (h.nameGarbled) issues.push('name taken from a drop cap line: letters may be missing');
      const second = lines.slice(hs + 1, end).findIndex((_, j) => isHeader(lines, hs + 1 + j));
      if (second >= 0) warnings.push(`p.${printed[p]} (pdf ${p + 1}): block "${h.name ?? heading ?? '?'}" contains a second header at line +${second + 1}`);
      blocks.push({
        book,
        printedPage: printed[p],
        pdfPage: p + 1,
        rawName: h.name ?? heading ?? null,
        level: h.level ?? null,
        rep: h.rep ?? null,
        role: h.role ?? null,
        hp: h.hp ?? null,
        seriouslyWounded: h.seriouslyWounded ?? null,
        deathSave: h.deathSave ?? null,
        stats: a.kind && !r.issue ? r.stats : null,
        armoredStats: a.kind && !r.issue ? r.armoredStats : null,
        parsed: !issues.some((s) => !/^name taken/.test(s) && !/^name taken from a drop cap/.test(s)),
        issues,
        preText: k === 0 && hs > 0 ? lines.slice(0, hs).join('\n').trim() : '',
        rawText: lines.slice(hs, end).join('\n').trimEnd(),
      });
    });
  });
  return { book, blocks, excluded, warnings, pages: pages.length };
}

// run

const files = readdirSync(PDFS).filter((f) => /\.pdf$/i.test(f)).sort();
const results = files.map((f) => ({ file: f, ...scan(f) }));
const blocks = results.flatMap((r) => r.blocks);

mkdirSync(OUT, { recursive: true });
writeFileSync(resolve(OUT, 'blocks.json'), `${JSON.stringify(blocks, null, 1)}\n`);

const generic = (b) => GENERIC.test(b.rawName ?? '') || /mook/i.test(b.level ?? '');
const rep = [];
const parsed = blocks.filter((b) => b.parsed).length;
rep.push(`NPC stat blocks: ${blocks.length} found in ${files.length} PDFs, ${parsed} fully read, ${blocks.length - parsed} to check by hand`);
rep.push('');
for (const r of results) {
  rep.push(`${r.book}  (${r.file}, ${r.pages} pages): ${r.blocks.length} blocks`);
}
for (const r of results.filter((x) => x.blocks.length || x.excluded.length)) {
  rep.push('', `=== ${r.book}: ${r.blocks.length} blocks (${r.blocks.filter(generic).length} possible generic) ===`);
  for (const b of r.blocks) {
    const bits = [`p.${b.printedPage}`, b.rawName ?? '(no name)', b.level ? `level ${b.level}` : null, b.rep !== null ? `rep ${b.rep}` : null, b.role ? `role ${b.role}` : null,
      generic(b) ? 'possible generic' : null, b.parsed ? null : `UNPARSED: ${b.issues.join('; ')}`].filter(Boolean);
    rep.push(`  ${bits.join(' | ')}`);
  }
  if (r.excluded.length) {
    const byWhat = new Map();
    for (const e of r.excluded) byWhat.set(e.what, [...(byWhat.get(e.what) ?? []), e.printedPage]);
    rep.push(`  excluded on purpose:`);
    for (const [what, pg] of byWhat) rep.push(`    ${what}: ${pg.length} lines, pages ${[...new Set(pg)].join(', ')}`);
  }
}
const warned = results.filter((r) => r.warnings.length);
rep.push('', `=== Header guard warnings: ${warned.reduce((n, r) => n + r.warnings.length, 0)} ===`);
for (const r of warned) for (const w of r.warnings) rep.push(`  ${r.book} ${w}`);
const bad = blocks.filter((b) => !b.parsed);
rep.push('', `=== Blocks not fully read, to check by hand: ${bad.length} ===`);
for (const b of bad) rep.push(`  ${b.book} p.${b.printedPage} (pdf ${b.pdfPage}) ${b.rawName ?? '(no name)'}: ${b.issues.join('; ')}`);
const fromHeading = blocks.filter((b) => b.parsed && b.issues.length);
rep.push('', `=== Fully read, but the name is a guess (page heading or garbled drop cap): ${fromHeading.length} ===`);
for (const b of fromHeading) rep.push(`  ${b.book} p.${b.printedPage}: ${b.rawName}  (${b.issues.join('; ')})`);
writeFileSync(resolve(OUT, 'report.txt'), `${rep.join('\n')}\n`);
console.log(rep.slice(0, 2 + results.length + 1).join('\n'));
console.log(`\nwritten: ${resolve(OUT, 'blocks.json')} and report.txt`);
