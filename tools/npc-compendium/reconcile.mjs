#!/usr/bin/env node
/**
 * Checks that every stat block found in the books (tools/npc-compendium/out/blocks.json) has a destination:
 * an entry of data/source/npc-compendium.json (same book and printed page, and a matching name) or a line of
 * tools/npc-compendium/discarded.txt ("<book> | p.<page> | <name> | <reason>"). Prints the blocks with none
 * and exits 1 if there are any. It also lists the book names used by the entries' sources.
 *
 * Run: node tools/npc-compendium/reconcile.mjs
 */
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const blocks = JSON.parse(readFileSync(resolve(root, 'tools/npc-compendium/out/blocks.json'), 'utf8'));
const npcs = JSON.parse(readFileSync(resolve(root, 'data/source/npc-compendium.json'), 'utf8'));
const discardedLines = readFileSync(resolve(root, 'tools/npc-compendium/discarded.txt'), 'utf8')
  .split('\n').map((l) => l.trim()).filter((l) => l && !l.startsWith('#'));

// The extractor names books by folder, the entries by their printed title.
const BOOK_NAMES = {
  'Interface RED 2': ['Interface RED Volume 2'],
  'Interface RED 3': ['Interface RED Volume 3'],
  'Interface RED 4': ['Interface RED Volume 4'],
  'Interface RED 5': ['Interface RED Volume 5'],
  'Mixing Drinks': ['Mixing Drinks, Changing Lives'],
  'Edgerunners Mission Kit': ['Edgerunners Mission Kit: Rule Book', 'Edgerunners Mission Kit: The Jacket'],
};
const booksOf = (block) => BOOK_NAMES[block] ?? [block];

// Names the extractor reads wrongly (drop caps, page headings): "<book>|<page>|<raw name>" -> the real name.
const NAME_FIXES = {
  'Danger Gal Dossier|46|EDGERUNNERS': '24/7',
  'Tales of the RED|16|TMhe onk': 'The Monk',
  'Tales of the RED|69|C A T W ommander aron yler': 'Commander Aaron Tyler Walsh',
  'Tales of the RED|88|CEon x': 'ConEx',
  'Tales of the RED|148|TTwo hee': 'Two Thee',
  'Interface RED 3|5|3 Hardened Executive Protection rep': 'Hardened Executive Protection',
  'Mixing Drinks|6|VA-11 HALL-A': 'Alma Armas',
  'Mixing Drinks|8|VA-11 HALL-A': 'Dorothy Haze',
};

const norm = (s) => (s ?? '').normalize('NFKD').replace(/[̀-ͯ]/g, '').toLowerCase().replace(/[^a-z0-9]/g, '');

// Drop-cap sheets are garbled by the extractor ("N 6 omad"): compare the letters as a multiset too.
const letters = (s) => norm(s).split('').sort().join('');

const nameMatches = (raw, npc) => {
  const wanted = [npc.name, ...npc.aliases, npc.id.replace(/-/g, ' ')].map(norm).filter(Boolean);
  const r = norm(raw);
  if (!r) return false;
  return wanted.some((w) => w === r || w.includes(r) || r.includes(w));
};

const discarded = discardedLines.map((l) => {
  const [book, page, name] = l.split('|').map((p) => p.trim());
  return { book, page: Number(page.replace(/^p\./, '')), name: norm(name) };
});

const orphans = [];
for (const block of blocks) {
  const books = booksOf(block.book);
  block.rawName = NAME_FIXES[`${block.book}|${block.printedPage}|${block.rawName}`] ?? block.rawName;
  const inEntry = npcs.some((npc) =>
    npc.sources.some((s) => books.includes(s.book) && s.page === block.printedPage) && nameMatches(block.rawName, npc));
  if (inEntry) continue;
  const inDiscarded = discarded.some((d) => books.includes(d.book) && d.page === block.printedPage && (norm(block.rawName) ? d.name === norm(block.rawName) : d.name.startsWith('noname')));
  if (!inDiscarded) orphans.push(block);
}

const sources = new Map();
for (const npc of npcs) for (const s of npc.sources) sources.set(s.book, (sources.get(s.book) ?? 0) + 1);
console.log('Book names used in sources:');
for (const [book, count] of [...sources].sort()) console.log(`  ${book} (${count})`);

if (orphans.length) {
  console.log('\nBlocks with no destination:');
  for (const b of orphans) console.log(`  ${b.book} | p.${b.printedPage} | ${b.rawName} (${letters(b.rawName)})`);
}
console.log(`\n${blocks.length} blocks, ${orphans.length} without a destination.`);
process.exit(orphans.length ? 1 : 0);
