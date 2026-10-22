#!/usr/bin/env node
/**
 * Extracts the Night Market data that the catalog does not carry, straight from the official PDFs:
 *
 * - data/source/night-market-index.json: every row of the Night Market Index (v1.1), with its section, group,
 *   cost, price category and sources, plus the catalog slug when the item is catalogued;
 * - data/source/black-chrome-booths.json: the "Pick-up or Browse at Booth @#" codes of each Black Chrome
 *   listing (BC p.7: the letter is one of the six Night Markets of p.131, the number a spot on its map).
 *
 * Run: node tools/night-market/extract.mjs ["<folder with the PDFs>"]
 * Needs pdftotext (xpdf or poppler) on PATH. Default folder: %USERPROFILE%/Downloads/Cyberpunk PDFs.
 * Rows the PDF layout breaks are fixed by hand in FIXES below; every fix was checked against the PDF.
 */
import { execFileSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import { homedir } from 'node:os';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const PDFS = resolve(process.argv[2] ?? resolve(homedir(), 'Downloads', 'Cyberpunk PDFs'));
const CATALOG = JSON.parse(readFileSync(resolve(root, 'data/seed/01-compendium.json'), 'utf8'));
const OUT_INDEX = resolve(root, 'data/source/night-market-index.json');
const OUT_BOOTHS = resolve(root, 'data/source/black-chrome-booths.json');

function pdftotext(file, mode) {
  const out = execFileSync('pdftotext', [mode, '-enc', 'UTF-8', resolve(PDFS, file), '-'], { maxBuffer: 64 * 1024 * 1024 });
  return out.toString('utf8').replace(/\r/g, '');
}

const STOP = new Set(['the', 'a', 'of', 'and', 'by', 'with', 'tm']);

function norm(s) {
  return s
    .replace(/\u2122|\ufffd/g, '')
    .normalize('NFKD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .replace(/\(padrão\)|\(padrao\)|\bpadrao\b|\(.*?\)/g, ' ')
    .replace(/[^a-z0-9]+/g, ' ')
    .split(' ')
    .filter((t) => t && !STOP.has(t))
    .join(' ');
}

const tokens = (s) => norm(s).split(' ').filter(Boolean);

// ------------------------------------------------------------------ Black Chrome booths

const BOOTH = /Booth\s+([A-F]\d(?:\s*\/\s*[A-F]\d)*)/;
const BC_PAGE_OFFSET = 1; // PDF page = printed page + 1
const BC_STREAM_PAGES = [16, 23]; // two-column cyberware pages; the rest use the catalog sidebar layout
/** Listings the layout heuristics miss (BC printed page in the comment). */
const BOOTH_FIXES = {
  'int-cyberware-hardened-shield': ['F1'], // p.20
  'racer-bracer': ['B6'], // p.22
  'rostovic-kleaver': ['C4'], // p.94
  'the-harveymerc': ['B1', 'F4'], // p.69, shares the Harvey 100 listing
  'fma-kotar-linear-frame': ['D2'], // p.52, "Fūma Kotarō" in the book
};

/** Splits a -layout page into two text columns, segment by segment. */
function columns(lines, split) {
  const cols = [[], []];
  for (const line of lines) {
    const segs = [...line.matchAll(/\S(?:.*?\S)?(?=\s{3,}|$)/g)].map((m) => [m.index, m[0].trim()]);
    cols[0].push(segs.filter(([c]) => c < split).map(([, t]) => t).join(' '));
    cols[1].push(segs.filter(([c]) => c >= split).map(([, t]) => t).join(' '));
  }
  return cols;
}

/** First row where most of the item's name words appear (names may wrap onto a second line). */
function nameRow(col, item) {
  const target = tokens(item.name);
  const need = Math.min(target.length, Math.max(2, target.length - 1));
  for (let i = 0; i < col.length; i++) {
    if (!col[i].trim()) continue;
    const here = new Set(tokens(col[i].replace(BOOTH, '')));
    const joined = new Set([...here, ...tokens(col[i + 1] ?? '')]);
    if (target.some((t) => here.has(t)) && target.filter((t) => joined.has(t)).length >= need) return i;
  }
  return null;
}

function extractBooths() {
  const text = pdftotext('Black Chrome [v1.1].pdf', '-layout');
  const items = CATALOG.filter((i) => i.source === 'Black Chrome' && i.attributes?.gearClass !== 'App'); // Apps have no booth (BC p.7)
  const booths = new Map();
  const add = (slug, codes) => booths.set(slug, new Set([...(booths.get(slug) ?? []), ...codes]));
  let seen = [];
  text.split('\f').forEach((page, index) => {
    const pdfPage = index + 1;
    const onPage = items.filter((i) => i.page === pdfPage - BC_PAGE_OFFSET);
    const lines = page.split('\n');
    const width = Math.max(0, ...lines.map((l) => l.length));
    const cols = columns(lines, Math.max(40, Math.floor(width / 2) - 10));
    if (pdfPage >= BC_STREAM_PAGES[0] && pdfPage <= BC_STREAM_PAGES[1]) {
      // left column, then right, page after page: a booth line closes the listings seen since the previous one
      for (const col of cols) {
        const rows = new Map();
        for (const it of onPage) {
          const r = nameRow(col, it);
          if (r !== null) rows.set(r, [...(rows.get(r) ?? []), it]);
        }
        col.forEach((line, i) => {
          seen.push(...(rows.get(i) ?? []));
          const m = line.match(BOOTH);
          if (!m) return;
          const codes = m[1].split('/').map((c) => c.trim());
          for (const it of seen.length ? seen : onPage.length === 1 ? onPage : []) add(it.slug, codes);
          seen = [];
        });
      }
      return;
    }
    seen = [];
    // sidebar layout: the booth sits next to the listing name
    const boothRows = lines.flatMap((l, i) => (l.match(BOOTH) ? [[i, l.match(BOOTH)[1].split('/').map((c) => c.trim())]] : []));
    if (!boothRows.length) return;
    for (const it of onPage) {
      const rows = cols.map((c) => nameRow(c, it)).filter((r) => r !== null);
      if (!rows.length) continue;
      const r = Math.min(...rows);
      const [, codes] = boothRows.reduce((best, b) => (Math.abs(b[0] - r) < Math.abs(best[0] - r) ? b : best));
      add(it.slug, codes);
    }
  });
  for (const [slug, codes] of Object.entries(BOOTH_FIXES)) booths.set(slug, new Set(codes));
  const result = Object.fromEntries([...booths].sort(([a], [b]) => a.localeCompare(b)).map(([slug, codes]) => [slug, [...codes].sort()]));
  const missing = items.filter((i) => !result[i.slug]).map((i) => i.slug);
  console.log(`booths: ${Object.keys(result).length} of ${items.length} Black Chrome listings${missing.length ? `; without booth: ${missing.join(', ')}` : ''}`);
  return result;
}

// ------------------------------------------------------------------ Night Market Index

const TABS = new Set(['Weapons,', 'Ammo &', 'Attachments', 'Armor', 'Gear', 'Cyberware', 'Fashion', 'Vehicles', 'NET Stuff',
  'Lifestyle &', 'Housing', 'Entertainment &', 'Services', 'Elflines', 'Online']);
// The section titles come out garbled (letters interleaved with the title repeated): match their first letters.
const SECTIONS = [
  ['weapons', /Wew ae poa np so/], ['armor', /Ara mr orm/], ['gear', /Geg ar\s+e\s+a\s+r/], ['cyberware', /Cyc bey rwb/],
  ['fashion', /Faf sha iosn/], ['vehicles', /Vev hice lesh/], ['net', /NEn T Se tuft/], ['lifestyle', /Lilfesi tyf/],
  ['entertainment', /Ene tnerttaeinrmet/], ['elflines', /Ele flinl es/],
];
const CATS = 'Free|Dirt Cheap|Evereyday|Cheap|Everyday|Costly|Premium|Expensive|V Expensive|Very Expensive|Luxury|S Luxury|Super Luxury';
const CATEGORY = {
  cheap: 'Cheap', everyday: 'Everyday', evereyday: 'Everyday', costly: 'Costly', premium: 'Premium', expensive: 'Expensive',
  'v expensive': 'VeryExpensive', 'very expensive': 'VeryExpensive', luxury: 'Luxury', 's luxury': 'SuperLuxury', 'super luxury': 'SuperLuxury',
};
const COST_START = new RegExp(`(?:[\\d,]+(?:eb)?\\s*\\((?:${CATS})|[\\d,]+eb\\b|(?<=\\s)(?:--|\u2014)(?=\\s|$)|(?<=\\s)DV Dependent|# of Floors Dependent|Varies)`);
const SOURCE_START = /(?:^|(?<=[\s,)]))(?:CP:R|CP:DP|BC|CEMK|CPCW|DGD|DLC?:\s?[\w+]+|IR\d|MC|T:\w+)\s*\d/g;
const CAT = new RegExp(`\\((${CATS})\\)`);
/** Rows printed broken in the index itself, as [broken text, text read off the PDF]. */
const LINE_FIXES = [
  ['100eb (DL:12Gear 4', '100eb (Premium)  DL:12Gear 4'], // SkidRow PackShield, Armor
];

function segments(line) {
  return line.trim().split(/\s{2,}/).map((s) => s.trim()).filter((s) => s && !TABS.has(s))
    .map((s) => s.replace(/^(?:Weapons,|Ammo &|Attachments|NET Stuff|Lifestyle &|Housing|Entertainment &|Services|Elflines|Online) /, ''));
}

function parseSources(text) {
  return text.split(/,\s*(?=[A-Z])/).map((part) => part.trim()).filter(Boolean).map((part) => {
    const m = part.match(/^(.*?)\s*(\d+)(?:-\d+)?\s*\*?$/);
    return m ? { book: m[1].replace(/\s/g, ''), page: Number(m[2]) } : { book: part, page: null };
  });
}

function parseRow(text) {
  const cost = text.match(COST_START);
  if (!cost) return null;
  const source = [...text.matchAll(SOURCE_START)].find((m) => m.index > cost.index);
  if (!source) return null;
  const costText = text.slice(cost.index, source.index).trim();
  const cat = costText.match(CAT);
  const price = costText.match(/^[\d,]+/);
  return {
    name: text.slice(0, cost.index).trim(),
    cost: costText,
    priceEb: price ? Number(price[0].replace(/,/g, '')) : null,
    priceCategory: cat ? CATEGORY[cat[1].toLowerCase()] ?? null : null,
    sources: parseSources(text.slice(source.index)),
  };
}

function extractIndex() {
  const lines = pdftotext('RTG-CPR-DLC-NightMarketIndexv1.1.pdf', '-table').replace(/\f/g, '\n').split('\n');
  let section = null;
  let group = null;
  let prev = null;
  const entries = [];
  for (let raw of lines) {
    for (const [broken, fixed] of LINE_FIXES) raw = raw.replace(broken, fixed);
    const hit = SECTIONS.find(([, rx]) => rx.test(raw));
    if (hit) [section, group] = [hit[0], null];
    if (!section) continue;
    const segs = segments(raw);
    if (!segs.length) continue;
    if (segs[0] === 'Name' && segs.some((s) => s.startsWith('Cost'))) { group = prev; continue; }
    const row = group ? parseRow(segs.join(' ')) : null;
    if (row && !row.name && prev && prev !== group) row.name = prev; // name wrapped onto the line above
    // Elflines Online in-game items are priced in gold pieces, not eb: they never reach a Night Market
    if (row?.name && !/\dgp\b/.test(row.cost)) { entries.push({ section, group, ...row }); continue; }
    const joined = segs.join('  ');
    if (segs.length === 1 && !COST_START.test(joined) && joined.length < 60) prev = joined;
  }
  return entries;
}

// ------------------------------------------------------------------ index -> catalog

const BOOK = {
  'CP:R': 'Core Book', BC: 'Black Chrome', 'DL:BC+': 'Black Chrome+', CEMK: 'Edgerunners Mission Kit', DGD: 'Danger Gal Dossier',
  IR2: 'Interface 2', IR3: 'Interface 3', IR4: 'Interface 4', IR5: 'Interface 5', 'DL:TT': "Toggle's Temple",
  'DL:WG': "Woodchipper's Garage", 'DL:HP': "Hornet's Pharmacy", 'DL:AAA': 'All About Agents', 'DL:12Gun': '12 Days of Gunmas',
  'DL:12Cy': '12 Days of Cybermas', 'DL:12Gear': '12 Days of Gearmas', 'DL:12RED': '12 Days of REDmas', 'DL:Home': 'No Place Like Home',
  'DL:Mix': 'Mixing Drinks\u2013Changing Lives', 'T:Hope': 'Tales of the RED', 'T:Street': 'Tales of the RED',
};

function matchCatalog(entries) {
  const byName = new Map();
  for (const it of CATALOG) byName.set(norm(it.name), [...(byName.get(norm(it.name)) ?? []), it]);
  for (const e of entries) {
    const n = norm(e.name);
    const tok = new Set(n.split(' '));
    const pages = new Set(e.sources.map((s) => `${BOOK[s.book]}|${s.page}`));
    const books = new Set(e.sources.map((s) => BOOK[s.book]));
    let cands = byName.get(n) ?? [];
    if (!cands.length) {
      // the catalog name may add or drop the maker ("Kendachi Mono-Katana" vs "Mono-Katana")
      cands = [...byName].flatMap(([k, its]) => {
        const kt = new Set(k.split(' '));
        const subset = [...kt].every((t) => tok.has(t)) || [...tok].every((t) => kt.has(t));
        return k && subset && Math.min(kt.size, tok.size) >= 2 ? its : [];
      });
    }
    if (!cands.length) {
      // same page and price, sharing a name word
      cands = CATALOG.filter((it) => pages.has(`${it.source}|${it.page}`) && it.price === e.priceEb && tokens(it.name).some((t) => tok.has(t)));
    }
    let best = cands.filter((c) => pages.has(`${c.source}|${c.page}`));
    if (!best.length) best = cands.filter((c) => books.has(c.source));
    if (!best.length) best = cands;
    if (best.length > 1) {
      // variants: prefer the one whose name adds the fewest words to the index name
      const extra = (c) => tokens(c.name).filter((t) => !tok.has(t)).length + [...tok].filter((t) => !tokens(c.name).includes(t)).length;
      const min = Math.min(...best.map(extra));
      best = best.filter((c) => extra(c) === min);
    }
    e.slug = best.length === 1 || (best.length > 1 && new Set(best.map((c) => c.price)).size === 1) ? best[0].slug : null;
  }
  return entries;
}

// ------------------------------------------------------------------ run

const booths = extractBooths();
writeFileSync(OUT_BOOTHS, `${JSON.stringify(booths, null, 1)}\n`);

const index = matchCatalog(extractIndex()).map(({ section, group, name, cost, priceEb, priceCategory, sources, slug }) =>
  ({ section, group, name, cost, priceEb, priceCategory, sources, slug }));
writeFileSync(OUT_INDEX, `${JSON.stringify(index, null, 1)}\n`);
const matched = index.filter((e) => e.slug).length;
console.log(`index: ${index.length} rows, ${matched} linked to the catalog (${new Set(index.map((e) => e.slug).filter(Boolean)).size} items)`);
