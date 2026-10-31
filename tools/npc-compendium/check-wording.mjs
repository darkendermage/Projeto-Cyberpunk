#!/usr/bin/env node
/**
 * Lists the English bios and special rules of data/source/npc-compendium.json that repeat a run of 8 or more
 * consecutive words from any of the books: those texts must be our own words. Exits 1 when it finds any.
 *
 * Run: node tools/npc-compendium/check-wording.mjs ["<folder with the PDFs>"]
 * Needs pdftotext on PATH. Default folder: %USERPROFILE%/Downloads/Cyberpunk PDFs.
 */
import { execFileSync } from 'node:child_process';
import { readdirSync, readFileSync } from 'node:fs';
import { homedir } from 'node:os';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const RUN = 8;
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const PDFS = resolve(process.argv[2] ?? resolve(homedir(), 'Downloads', 'Cyberpunk PDFs'));
const npcs = JSON.parse(readFileSync(resolve(root, 'data/source/npc-compendium.json'), 'utf8'));

const words = (text) => text.toLowerCase().replace(/[’']/g, '').match(/[\p{L}\p{N}]+/gu) ?? [];

// Plain mode reads each column on its own, so sentences aren't interleaved as they are with -layout.
const runs = new Set();
for (const file of readdirSync(PDFS).filter((f) => f.toLowerCase().endsWith('.pdf'))) {
  const text = execFileSync('pdftotext', ['-enc', 'UTF-8', resolve(PDFS, file), '-'], { maxBuffer: 256 * 1024 * 1024 }).toString('utf8');
  const all = words(text);
  for (let i = 0; i + RUN <= all.length; i++) runs.add(all.slice(i, i + RUN).join(' '));
}

const hits = [];
const check = (id, field, text) => {
  const all = words(text ?? '');
  let i = 0;
  while (i + RUN <= all.length) {
    if (!runs.has(all.slice(i, i + RUN).join(' '))) { i++; continue; }
    let end = i + RUN;
    while (end < all.length && runs.has(all.slice(end - RUN + 1, end + 1).join(' '))) end++;
    hits.push(`${id} | ${field} | ${end - i} words | ${all.slice(i, end).join(' ')}`);
    i = end;
  }
};

for (const npc of npcs) {
  check(npc.id, 'summary', npc.summary?.en);
  check(npc.id, 'sheetNote', npc.sheetNote?.en);
  for (const special of npc.sheet?.specials ?? []) check(npc.id, `special ${special.name}`, special.text?.en);
}

console.log(hits.length ? hits.join('\n') : 'No runs found.');
console.log(`${npcs.length} entries checked, ${hits.length} runs of ${RUN}+ words.`);
process.exit(hits.length ? 1 : 0);
