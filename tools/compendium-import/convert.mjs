#!/usr/bin/env node
/**
 * Converts data/source/compendio_completo_cyberpunk_red.md into the catalog import format
 * (data/seed/01-compendium.json). Run: node tools/compendium-import/convert.mjs
 *
 * Every record is tagged dataOrigin = "Compendium": it comes from a user-provided compendium and has
 * NOT been verified against the rulebooks. Derived values (weapon calibers, max range per class,
 * price category from the standard RED price points) follow the core rules and are documented here.
 */
import { readFileSync, readdirSync, writeFileSync, mkdirSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const SOURCE = resolve(root, 'data/source/compendio_completo_cyberpunk_red.md');
const OUT = resolve(root, 'data/seed/01-compendium.json');
const ERRATA_DIR = resolve(root, 'data/source/errata');
const BOOK_TEXT = resolve(root, 'data/source/book-descriptions.json');

const md = readFileSync(SOURCE, 'utf8').replace(/\r\n/g, '\n');

// ------------------------------------------------------------------ markdown helpers

/** Splits the document into sections keyed by their numeric heading ("1.1", "2.5"...). */
function sections() {
  const result = {};
  let current = null;
  for (const line of md.split('\n')) {
    const h = line.match(/^#{2,3} (?:\*\*)?(\d+\.\d+)\b/);
    if (h) { current = h[1]; result[current] = []; continue; }
    if (/^# \d+\./.test(line)) { current = null; continue; }
    if (current) result[current].push(line);
  }
  return result;
}

/** Lines after a sub-heading such as "### **Linha de Cyberfingers...**" until the next heading. */
function subsection(title) {
  const lines = md.split('\n');
  const start = lines.findIndex((l) => l.startsWith('###') && l.includes(title));
  if (start < 0) throw new Error(`Subsection not found: ${title}`);
  const out = [];
  for (const line of lines.slice(start + 1)) {
    if (/^#{1,3} /.test(line) || line.startsWith('---')) break;
    out.push(line);
  }
  return out;
}

const clean = (s) => (s ?? '').replace(/\*\*/g, '').replace(/\*/g, '').replace(/\s+/g, ' ').trim();

function tableRows(lines) {
  const rows = lines.filter((l) => l.trim().startsWith('|'));
  if (rows.length < 2) return [];
  const split = (l) => l.trim().replace(/^\|/, '').replace(/\|$/, '').split('|').map((c) => c.trim());
  const header = split(rows[0]).map(clean);
  return rows.slice(2).map((r) => {
    const cells = split(r);
    return Object.fromEntries(header.map((h, i) => [h, cells[i] ?? '']));
  });
}

/** "* **Name (price / extra)**: text" bullets. */
function bullets(lines) {
  return lines
    .map((l) => l.match(/^\* \*\*(.+?)\*\*:?\s*(.*)$/))
    .filter(Boolean)
    .map(([, head, text]) => {
      const m = head.match(/^(.*?)\s*\(([^)]*)\)\s*:?$/);
      return { name: clean(m ? m[1] : head), meta: m ? m[2] : '', text: clean(text) };
    });
}

// ------------------------------------------------------------------ value helpers

function parsePrice(raw) {
  const text = clean(raw);
  const first = text.match(/(\d[\d.,]*)\s*(k|eb)?/i);
  if (!first) return { price: null, note: text || null };
  let n = first[1];
  // "1.000eb" and "2,100eb" both use a thousands separator in the compendium.
  n = Number(n.replace(/[.,](?=\d{3}\b)/g, '').replace(',', '.'));
  if (first[2]?.toLowerCase() === 'k') n *= 1000;
  const multiple = /[/]|\bou\b/.test(text.replace(/\/\s*\d+\s*un\./, '').replace(/\/\s*\d+\s*HL/i, '').replace(/\/\s*\d+\s*HQ IP/i, ''));
  return { price: Math.round(n), note: multiple ? text : null };
}

const PRICE_CATEGORIES = {
  cheap: 'Cheap', everyday: 'Everyday', costly: 'Costly', premium: 'Premium', expensive: 'Expensive',
  'very expensive': 'VeryExpensive', 'v. exp': 'VeryExpensive', luxury: 'Luxury', 'super luxury': 'SuperLuxury',
};
function parsePriceCategory(raw) {
  const t = clean(raw).toLowerCase().split('/')[0].trim();
  return PRICE_CATEGORIES[t] ?? null;
}
/** RED price categories are defined by these exact price points. */
function priceCategoryFromPrice(price) {
  return { 10: 'Cheap', 20: 'Everyday', 50: 'Costly', 100: 'Premium', 500: 'Expensive', 1000: 'VeryExpensive', 5000: 'Luxury' }[price]
    ?? (price >= 10000 ? 'SuperLuxury' : null);
}

function parseSource(raw) {
  const text = clean(raw);
  if (!text) return { source: null, note: null };
  const parts = text.split('/').map((p) => p.trim()).filter(Boolean);
  return { source: parts[0], note: parts.length > 1 ? `Fontes citadas: ${text}.` : null };
}

const firstDice = (s) => { const m = clean(s).match(/(\d+)d6/); return m ? Number(m[1]) : null; };
const intOrNull = (s) => { const m = clean(s).match(/-?\d+/); return m ? Number(m[0]) : null; };

function parseHumanity(raw) {
  const t = clean(raw);
  const m = t.match(/^(\d+)\s*(?:\(([^)]+)\))?/);
  if (!m) return { humanityLoss: null, humanityLossDice: null };
  const dice = m[2] && m[2] !== 'N/A' ? m[2] : null;
  return { humanityLoss: Number(m[1]), humanityLossDice: dice };
}

const MANUFACTURERS = [
  ['Arasaka', 'Arasaka'], ['Militech', 'Militech'], ['MT Tactical', 'Militech'], ['Budget Arms', 'Budget Arms'], ['BudgetArms', 'Budget Arms'],
  ['Chadran Arms', 'Chadran Arms'], ['Constitutional Arms', 'Constitutional Arms'], ['Con Arms', 'Constitutional Arms'],
  ['Dai Lung', 'Dai Lung'], ['Darra Polytechnic', 'Darra Polytechnic'], ['Eagletech', 'Eagletech'], ['Everest', 'Everest'],
  ['Federated Arms', 'Federated Arms'], ['Fed Arms', 'Federated Arms'], ['GunMart', 'GunMart'], ['Georgia Arms', 'Georgia Arms'],
  ['Hello Cutie', 'Sanroo'], ['Sanroo', 'Sanroo'], ['Micro Cutie', 'Sanroo'], ['IMI', 'IMI'], ['Kang Tao', 'Kang Tao'],
  ['Kendachi', 'Kendachi'], ['KTech', 'KTech'], ['Malorian', 'Malorian Arms'], ['MetaCorp', 'MetaCorp'], ['Midnight Arms', 'Midnight Arms'],
  ['Midnight Assault', 'Midnight Arms'], ['Mustang Arms', 'Mustang Arms'], ['Nova', 'Nova'], ['Westwood (Nova', 'Nova'],
  ['Rhinemetall', 'Rhinemetall'], ['Rostović', 'Rostović'], ['SlamDance', 'SlamDance'], ['Sternmeyer', 'Sternmeyer'],
  ['Stolbovoy', 'Stolbovoy'], ['Superchrome', 'Superchrome'], ['Techtronika', 'Techtronika'], ['Towa', 'Towa'],
  ['Tsunami Arms', 'Tsunami Arms'], ['Zhirafa', 'Zhirafa'], ["Faisal's", "Faisal's"], ['Biotechnica', 'Biotechnica'],
  ['Pursuit Security', 'Pursuit Security'], ['PS E-TACK', 'Pursuit Security'], ['Magnum Opus', 'Magnum Opus'], ['UrbanTech', 'UrbanTech'],
  ['Kiroshi', 'Kiroshi'], ['Zetatech', 'Zetatech'], ['Rocklin', 'Rocklin'], ['Dynalar', 'Dynalar'], ['Sycust', 'Sycust'],
  ['Romanova', 'Romanova'], ['Psiberstuff', 'Psiberstuff'], ['Mediaware', 'Mediaware'], ['Raven MicroCyb', 'Raven MicroCyb'],
  ['EBM', 'EBM'], ['Hammered Ind', 'Hammered Industries'], ['Cybermatrix', 'Cybermatrix'], ['PersonalPak', 'PersonalPak'],
  ['AmeriCar', 'AmeriCar'], ['Harvey 100 (AmeriCar)', 'AmeriCar'], ['Highway Master', 'Highway Master'], ['Quadra', 'Quadra'],
  ['Esporma', 'Esporma'],
];
function manufacturerOf(name) {
  const hit = [...MANUFACTURERS].sort((a, b) => b[0].length - a[0].length).find(([p]) => name.startsWith(p) || name.includes(`(${p}`));
  return hit ? hit[1] : null;
}

const has = (text, re) => re.test(text);

// ------------------------------------------------------------------ weapons

const CLASS_DEFAULTS = {
  MediumPistol: { calibers: ['MediumPistol'], range: 200, skill: 'Handgun', magazine: 12 },
  HeavyPistol: { calibers: ['HeavyPistol'], range: 200, skill: 'Handgun', magazine: 8 },
  VeryHeavyPistol: { calibers: ['VeryHeavyPistol'], range: 200, skill: 'Handgun', magazine: 8 },
  Smg: { calibers: ['MediumPistol'], range: 400, skill: 'Handgun', concealable: true },
  HeavySmg: { calibers: ['HeavyPistol'], range: 400, skill: 'Handgun' },
  Shotgun: { calibers: ['Slug', 'ShotgunShell'], range: 200, skill: 'ShoulderArms' },
  AssaultRifle: { calibers: ['Rifle'], range: 800, skill: 'ShoulderArms' },
  SniperRifle: { calibers: ['Rifle'], range: 800, skill: 'ShoulderArms' },
  MachineGun: { calibers: ['Rifle'], range: 800, skill: 'HeavyWeapons' },
  Bow: { calibers: ['Arrow'], range: 200, skill: 'Archery' },
  Crossbow: { calibers: ['Arrow'], range: 200, skill: 'Archery' },
  GrenadeLauncher: { calibers: ['Grenade'], range: 400, skill: 'HeavyWeapons' },
  RocketLauncher: { calibers: ['Rocket'], range: 800, skill: 'HeavyWeapons' },
  LightMelee: { calibers: [], range: 0, skill: 'MeleeWeapon' },
  MediumMelee: { calibers: [], range: 0, skill: 'MeleeWeapon' },
  HeavyMelee: { calibers: [], range: 0, skill: 'MeleeWeapon' },
  VeryHeavyMelee: { calibers: [], range: 0, skill: 'MeleeWeapon' },
};

/** Ammunition overrides for exotic and combination weapons, based on the compendium notes. */
const CALIBER_OVERRIDES = {
  'Air Pistol': ['Special'], 'Hello Cutie ModBall Gun': ['Special'], 'Hello Cutie Janus Hex': ['Special'],
  'Nomad Air Cannon': ['Special'], 'Nomad Rocker': ['Special'], 'HD Stickybomb Launcher': ['Special'],
  Dartgun: ['Arrow'], 'Stun Gun': ['Battery'], Microwaver: ['Battery'], Shrieker: ['Battery'], Scrambler: ['Battery'],
  'Militech Aegis': ['Battery'], 'Pursuit Security Bouncer': ['Battery'],
  'Militech Crusher': ['Slug', 'ShotgunShell'], 'Militech Crusher (207X)': ['Slug', 'ShotgunShell'],
  Tommyknocker: ['VeryHeavyPistol', 'Slug', 'ShotgunShell'], 'Militech Mastiff': ['Slug', 'ShotgunShell', 'HeavyPistol'],
  'Militech Mastiff SMG': ['HeavyPistol', 'Slug', 'ShotgunShell'], 'KTech TechHammer': ['Slug', 'ShotgunShell', 'Rocket'],
  'BudgetArms Triple Threat': ['Rocket', 'Grenade'], 'Sternmeyer M-04 Variable': ['Rifle', 'Grenade'],
  'Darra Polytechnic Binary': ['Rifle', 'Grenade'], 'Eagletech Survivalist': ['Rifle', 'Arrow'],
  'ModFire 10X': ['HeavyPistol', 'MediumPistol', 'Rifle'], 'Sanroo Hello Cutie Ultra-K8': ['VeryHeavyPistol', 'HeavyPistol'],
  'Flare Gun': ['Grenade', 'Special'], 'Militech Tactical Umbrella': ['HeavyPistol'],
};

/** Alternate listings of an item already present under another name. */
const ALIASES = new Set(['MT Tactical Umbrella', 'ModFire 10X (Rifle Mode)', 'KTech TechHammer (Rocket)']);

function weaponClass(section, type) {
  const t = clean(type);
  if (section === '1.1') return 'MediumPistol';
  if (section === '1.2') return t.startsWith('Heavy Pistol') ? 'HeavyPistol' : 'HeavyPistol';
  if (section === '1.3') return 'VeryHeavyPistol';
  if (section === '1.4') return t.startsWith('Heavy SMG') ? 'HeavySmg' : 'Smg';
  if (section === '1.5') return 'Shotgun';
  if (section === '1.6' || section === '1.7') {
    if (t.startsWith('Assault')) return 'AssaultRifle';
    if (t.startsWith('Sniper')) return 'SniperRifle';
    if (t.startsWith('Machine Gun')) return 'MachineGun';
    if (t.startsWith('Bow')) return 'Bow';
    if (t.startsWith('Crossbow')) return 'Crossbow';
    if (t.startsWith('Shotgun')) return 'Shotgun';
    if (t.startsWith('Grenade')) return 'GrenadeLauncher';
    if (t.startsWith('Rocket')) return 'RocketLauncher';
  }
  if (section === '1.8') {
    if (/^(Leve|Light)/.test(t)) return 'LightMelee';
    if (/^(Muito Pesada|V\. Heavy)/.test(t)) return 'VeryHeavyMelee';
    if (/^(Pesada|Heavy|Thrown)/.test(t)) return 'HeavyMelee';
    if (/^(Média|Medium)/.test(t)) return 'MediumMelee';
  }
  throw new Error(`Unknown weapon class: ${section} / ${t}`);
}

function parseRof(raw) {
  const t = clean(raw);
  const af = t.match(/AFx(\d+)/);
  const single = t.match(/^(\d+)/);
  return { rof: single ? Number(single[1]) : null, autofire: af ? Number(af[1]) : null };
}

function magazineFromNotes(notes) {
  const m = notes.match(/(?:[Pp]entes?|[Bb]ateria)[^.\d]{0,30}?(\d+)/) || notes.match(/\((\d+) tiros\)/);
  return m ? Number(m[1]) : null;
}

function weaponTags(notes, type, data) {
  const tags = new Set();
  if (data.quality === 'Excellent') tags.add('Qualidade Excelente');
  if (data.quality === 'Poor') tags.add('Baixa Qualidade');
  if (/Exótica/.test(type)) tags.add('Exótica');
  if (/Smartgun/i.test(notes)) tags.add('Smartgun');
  if (/Silenciad|Supressor/i.test(notes)) tags.add('Silenciada');
  if (/não-letal|menos-letal|LTL|non-lethal/i.test(notes + type)) tags.add('Não-letal');
  if (data.autofire) tags.add('Autofire');
  if (/descartável/i.test(notes)) tags.add('Descartável');
  if (/Mira|Scope|Infravermelh/i.test(notes)) tags.add('Mira acoplada');
  if (/Pente Estendido|Pente Tambor|Tambor|Pente Caixa/i.test(notes)) tags.add('Pente estendido');
  if (/Wardrobe and Style|\+2 Style/i.test(notes)) tags.add('Estilo');
  if (/sob o cano|Underbarrel|acoplada|Lança-Granadas de cano/i.test(notes)) tags.add('Acessório acoplado');
  if (/incend|chamas|Fogo/i.test(notes + type)) tags.add('Incendiária');
  if (/EMP|Microwaver/.test(notes + type)) tags.add('EMP');
  if (/monofilamento/i.test(notes)) tags.add('Monofilamento');
  if (/rev[oó]lver/i.test(notes)) tags.add('Revólver');
  if (/arremessad|Thrown/i.test(notes + type)) tags.add('Arremessável');
  if (/\//.test(type)) tags.add('Arma combinada');
  return [...tags];
}

function convertWeapons(secs) {
  const items = [];
  for (const sec of ['1.1', '1.2', '1.3', '1.4', '1.5', '1.6', '1.7', '1.8']) {
    for (const row of tableRows(secs[sec])) {
      const name = clean(row['Nome do Item / Arma'] ?? row['Nome do Item / Carga']);
      if (!name || ALIASES.has(name)) continue;
      const type = clean(row['Categoria / Tipo']);
      const notes = clean(row['Notas & Efeitos Especiais']);
      const cls = weaponClass(sec, type);
      const defaults = CLASS_DEFAULTS[cls];
      const { rof, autofire } = parseRof(row['ROF']);
      const quality = /Qualidade Excelente|Excellent/i.test(notes) ? 'Excellent' : /Poor Quality/i.test(notes) ? 'Poor' : 'Standard';
      const concealCell = clean(row['Concebível?']);
      const concealable = concealCell
        ? concealCell === 'Sim'
        : Boolean(defaults.concealable) || /ocult[aá]vel|ocultado/i.test(notes);
      const magazine = cls.endsWith('Melee')
        ? null
        : row['Pente'] !== undefined ? intOrNull(row['Pente']) : magazineFromNotes(notes) ?? defaults.magazine ?? null;
      let skill = defaults.skill;
      if (/Heavy Weapons/.test(notes)) skill = 'HeavyWeapons';
      if (/Pilot Air Vehicle/.test(notes)) skill = 'PilotAirVehicle';
      const body = notes.match(/BODY (\d+)\+/);
      const { price, note: priceNote } = parsePrice(row['Preço (eddies)']);
      const { source, note: sourceNote } = parseSource(row['Fonte / Livro']);
      const isRanged = !cls.endsWith('Melee');
      const attrs = {
        weaponClass: cls,
        skill,
        damage: clean(row['Dano']),
        damageDice: firstDice(row['Dano']),
        rof,
        autofire: isRanged ? autofire : null,
        magazine,
        maxRange: defaults.range,
        hands: /2H/.test(row['Mãos']) && !/1H/.test(row['Mãos']) ? 2 : 1,
        concealable,
        quality,
        exotic: /Exótica/.test(type),
        minBody: body ? Number(body[1]) : null,
        calibers: isRanged ? (CALIBER_OVERRIDES[name] ?? defaults.calibers) : [],
      };
      // A few autofire weapons carry fewer than 10 rounds in the compendium; keep the data, drop the inconsistent flag.
      if (attrs.autofire && attrs.magazine !== null && attrs.magazine < 10) attrs.autofire = null;
      items.push(base(name, 'weapons', {
        description: notes,
        notes: sourceNote,
        price, priceNote,
        priceCategory: parsePriceCategory(row['Categoria de Preço']),
        source,
        tags: weaponTags(notes, type, attrs),
        attributes: attrs,
      }));
    }
  }
  return items;
}

// ------------------------------------------------------------------ ammunition & explosives

function ammoEffect(name, text) {
  const t = `${name} ${text}`;
  if (/Biotoxin|Poison|Veneno/i.test(t)) return 'Toxin';
  if (/\bEMP\b/i.test(t)) return 'Emp';
  if (/Rubber|Não-Letal/i.test(t)) return 'NonLethal';
  if (/Flashbang|Smoke|Fumaça|Teargas|Sleep|Chromatic|Arachnid/i.test(t)) return 'Control';
  if (/Incendi|Molotov|Fogo/i.test(t)) return 'Incendiary';
  if (/Armor-Piercing|Armor Piercing|\bAP\b/i.test(t)) return 'ArmorPiercing';
  if (/Burrowing|Expansive|Serrated|Explosive Ammo/i.test(t)) return 'CriticalInjury';
  if (/Precision|Velocity|Smart|Tracer/i.test(t)) return 'Accuracy';
  if (/CO2|Kill Switch|Detonador|Airburst/i.test(t)) return 'Utility';
  return 'Lethal';
}

function calibersFromCompat(text) {
  const t = clean(text);
  const set = new Set();
  const bullets = ['MediumPistol', 'HeavyPistol', 'VeryHeavyPistol', 'Rifle'];
  if (/capazes de disparar munição básica/i.test(t)) ['MediumPistol', 'HeavyPistol', 'VeryHeavyPistol', 'Rifle', 'Slug', 'Arrow'].forEach((c) => set.add(c));
  if (/Pistolas|SMGs/.test(t)) ['MediumPistol', 'HeavyPistol', 'VeryHeavyPistol'].forEach((c) => set.add(c));
  if (/Balas/.test(t)) bullets.forEach((c) => set.add(c));
  if (/Rifles/.test(t)) set.add('Rifle');
  if (/Slugs/.test(t)) set.add('Slug');
  if (/Shells/.test(t)) set.add('ShotgunShell');
  if (/Flechas/.test(t)) set.add('Arrow');
  if (/Granadas/.test(t)) set.add('Grenade');
  if (/Foguetes/.test(t)) set.add('Rocket');
  return [...set];
}

function convertAmmunition(secs) {
  const items = [];
  for (const row of tableRows(secs['1.9'])) {
    const name = clean(row['Nome do Item / Carga']);
    const cat = clean(row['Categoria']);
    const effectText = clean(row['Dano / Efeito']);
    const notes = clean(row['Notas & Efeitos Especiais']);
    const kind = /^Granada/.test(cat) ? 'Grenade' : /Detonador/.test(cat) ? 'Accessory' : 'Explosive';
    const calibers = /Granada/.test(cat) ? (/Foguete/.test(cat) ? ['Grenade', 'Rocket'] : ['Grenade']) : [];
    const dv = `${effectText} ${notes}`.match(/DV ?(\d+)/);
    const { price, note: priceNote } = parsePrice(row['Preço (eddies)']);
    const { source, note: sourceNote } = parseSource(row['Fonte / Livro']);
    items.push(base(name, 'ammunition', {
      description: notes, notes: sourceNote, price, priceNote,
      priceCategory: parsePriceCategory(row['Categoria de Preço']), source,
      tags: [],
      attributes: {
        kind, effectType: ammoEffect(name, `${effectText} ${notes}`), calibers,
        quantityPerPack: kind === 'Accessory' ? null : 1,
        damage: /\d+d6/.test(effectText) ? effectText : null,
        damageDice: firstDice(effectText),
        area: clean(row['Área de Efeito']) || null,
        resistDv: dv ? Number(dv[1]) : null,
        effect: effectText,
      },
    }));
  }

  for (const row of tableRows(secs['1.10'])) {
    const name = clean(row['Tipo de Munição']);
    const priceCell = clean(row['Preço (eddies)']);
    const { price } = parsePrice(priceCell);
    const qty = priceCell.match(/\/\s*(\d+)\s*un\./);
    const category = priceCell.match(/\(([^)]+)\)/);
    const compat = clean(row['Compatibilidade de Armas']);
    const req = compat.match(/\((Exige[^)]*|Requer[^)]*)\)/);
    const effect = clean(row['Efeito Mecânico e Tático']);
    const dv = effect.match(/DV ?(\d+)/);
    items.push(base(name, 'ammunition', {
      description: effect, price,
      priceNote: qty ? `Pacote com ${qty[1]} unidades` : null,
      priceCategory: category ? parsePriceCategory(category[1]) : null,
      tags: [],
      attributes: {
        kind: 'Round', effectType: ammoEffect(name, effect), calibers: calibersFromCompat(compat),
        quantityPerPack: qty ? Number(qty[1]) : null,
        resistDv: dv ? Number(dv[1]) : null,
        requirements: req ? req[1] : null,
        effect: `Compatível com: ${compat.replace(/\s*\([^)]*\)/, '')}.`,
      },
    }));
  }
  return items;
}

// ------------------------------------------------------------------ cyberware

const FOUNDATION_SLOTS = {
  'Neural Link': 5, Cybereye: 3, 'Sponsored Cybereye': 3, 'Cyberaudio Suite': 3, 'Discount Cyberaudio Suite': 1,
  Cyberarm: 4, Cyberleg: 3, 'Neo-Soviet Cyberarm': 3, 'Modular Finger Cyberhand': 5, 'Modular Finger Enthusiast': 8,
  'Cyclops Int. Bug Eye': 5, 'Kiroshi MonoVision': 3, 'Sensor Array': 5, 'MultiOptic Mount': 5,
  'Chipware Socket': null, 'Budget Chipware Socket': null, 'Artificial Shoulder Mount': null,
};

function installLocation(text) {
  const t = clean(text);
  if (/^Chipware Socket/.test(t)) return 'Socket';
  if (/^Mall/.test(t)) return 'Mall';
  if (/^Clinic/.test(t)) return 'Clinic';
  if (/^Hospital/.test(t)) return 'Hospital';
  return 'Unknown';
}

function cyberwareRow({ name, type, location, slotsCell, priceCell, hlCell, reqCell, effect, requires }) {
  const { price, note: priceNote } = parsePrice(priceCell);
  const hl = parseHumanity(hlCell);
  const req = clean(reqCell);
  const slotsText = clean(slotsCell);
  const effectSlots = effect.match(/\((\d+) slots?\)/i) || effect.match(/,\s*(\d+) slots\)/i);
  const foundational = slotsText === 'Base' || Object.hasOwn(FOUNDATION_SLOTS, name) || ['Cyberlimb', 'Borgware'].includes(type) && /slots? de opções|slots opções/.test(effect);
  let slotsRequired = /^\d/.test(slotsText) ? intOrNull(slotsText) : null;
  if (slotsRequired === null && !foundational && effectSlots && type === 'LimbOption') slotsRequired = Number(effectSlots[1]);
  return base(name, 'cyberware', {
    description: effect, price, priceNote, priceCategory: priceCategoryFromPrice(price),
    tags: [/Requer Par|\(Par\)/.test(`${req} ${slotsText}`) ? 'Exige par' : null, /EMP|Microwaver/.test(effect) ? 'Proteção EMP' : null].filter(Boolean),
    attributes: {
      cyberwareType: type,
      bodyLocation: location,
      installLocation: installLocation(req),
      ...hl,
      foundational,
      slotsProvided: foundational ? FOUNDATION_SLOTS[name] ?? (effectSlots ? Number(effectSlots[1]) : null) : null,
      slotsRequired,
      requiresPair: /Requer Par|\(Par\)/.test(`${req} ${slotsText}`),
      requiresItem: foundational && !/Socket/.test(name) ? null : requires,
      requirements: req || null,
      effect,
    },
  });
}

function convertCyberware(secs) {
  const items = [];
  const seen = new Set();
  const push = (item) => { if (!seen.has(item.name)) { seen.add(item.name); items.push(item); } };

  const simple = [
    ['2.1', 'Neuralware', 'NervousSystem', 'Neural Link'],
    ['2.2', 'Cyberoptics', 'Eyes', 'Cybereye'],
    ['2.3', 'Cyberaudio', 'Ears', 'Cyberaudio Suite'],
  ];
  for (const [sec, type, location, foundation] of simple) {
    for (const row of tableRows(secs[sec])) {
      const name = clean(row['Item de Cromo']);
      const slotsCell = row['Slots'];
      const isChip = clean(slotsCell) === 'Chip';
      const req = clean(row['Requisitos / Instalação']);
      let requires = name === foundation ? null : isChip ? 'Chipware Socket' : foundation;
      if (/Requer Neural/.test(req)) requires = 'Neural Link';
      push(cyberwareRow({
        name, type, location, slotsCell, priceCell: row['Preço (eddies)'], hlCell: row['Perda de Humanidade (HL)'],
        reqCell: req, effect: clean(row['Efeitos Mecânicos e Táticos']), requires,
      }));
    }
  }

  for (const row of tableRows(secs['2.4'])) {
    const name = clean(row['Item de Cromo']);
    const cat = clean(row['Categoria']);
    push(cyberwareRow({
      name, type: cat === 'Internal' ? 'InternalBody' : 'ExternalBody', location: cat === 'Internal' ? 'Internal' : 'Skin',
      slotsCell: '', priceCell: row['Preço (eddies)'], hlCell: row['Perda de Humanidade (HL)'],
      reqCell: row['Requisitos / Instalação'], effect: clean(row['Efeitos Mecânicos e Táticos']), requires: null,
    }));
  }

  for (const row of tableRows(secs['2.5'])) {
    const name = clean(row['Item de Cromo']);
    const cat = clean(row['Categoria']);
    if (cat === 'Neuralware') continue; // duplicate of the Reflex Co-Processor in 2.1
    const map = {
      Cyberlimb: ['Cyberlimb', /leg/i.test(name) ? 'Leg' : 'Arm', null],
      Cyberarm: ['Cyberlimb', 'Arm', null],
      Cyberleg: ['Cyberlimb', 'Leg', null],
      'Arm Option': ['LimbOption', 'Arm', 'Cyberarm'],
      'Leg Option': ['LimbOption', 'Leg', 'Cyberleg'],
      'Limb Option': ['LimbOption', 'FullBody', null],
    }[cat];
    if (!map) throw new Error(`Unknown limb category ${cat}`);
    const req = clean(row['Requisitos / Instalação']);
    push(cyberwareRow({
      name, type: map[0], location: map[1], slotsCell: '', priceCell: row['Preço (eddies)'],
      hlCell: row['Perda de Humanidade (HL)'], reqCell: req, effect: clean(row['Efeitos Mecânicos e Táticos']),
      requires: /Requer Neural/.test(req) ? 'Neural Link' : map[2],
    }));
  }

  for (const b of bullets(subsection('Linha de Cyberfingers'))) {
    const [priceText, hlText] = b.meta.split('/').map((s) => s.trim());
    const { price } = parsePrice(priceText);
    push(base(b.name, 'cyberware', {
      description: b.text, price, priceCategory: priceCategoryFromPrice(price),
      manufacturer: 'Dynalar',
      tags: [],
      attributes: {
        cyberwareType: 'Cyberfinger', bodyLocation: 'Hand', installLocation: 'Unknown',
        humanityLoss: intOrNull(hlText), foundational: false, slotsRequired: 1, requiresPair: false,
        requiresItem: 'Modular Finger Cyberhand', requirements: 'Instalável em Mão Modular', effect: b.text,
      },
    }));
  }

  for (const row of tableRows(secs['2.6'])) {
    const name = clean(row['Item / Pacote']);
    if (/Linear Frame/.test(name)) continue; // modelled as armor (Linear Frame)
    push(cyberwareRow({
      name, type: 'Borgware', location: /Eye|Optic|MonoVision/.test(name) ? 'Eyes' : /Sensor/.test(name) ? 'Head' : 'FullBody',
      slotsCell: '', priceCell: row['Preço (eddies)'], hlCell: row['Perda de Humanidade (HL)'],
      reqCell: row['Requisitos / Instalação'], effect: clean(row['Efeitos Mecânicos e Táticos']), requires: null,
    }));
  }

  for (const b of bullets(subsection('Pacotes Bioexotics'))) {
    const [priceText, hlText] = b.meta.split('/').map((s) => s.trim());
    const { price } = parsePrice(priceText);
    push(base(b.name, 'cyberware', {
      description: b.text, price, manufacturer: 'Biotechnica', priceCategory: priceCategoryFromPrice(price),
      tags: ['Pacote'],
      attributes: {
        cyberwareType: 'Bioexotic', bodyLocation: 'FullBody', installLocation: 'Unknown',
        humanityLoss: intOrNull(hlText), foundational: false, requiresPair: false, effect: b.text,
      },
    }));
  }
  return items;
}

// ------------------------------------------------------------------ armor (incl. Linear Frames and ACPA)

function convertArmor(secs) {
  const items = [];
  for (const row of tableRows(secs['3.1'])) {
    const name = clean(row['Nome da Armadura']);
    const spCell = clean(row['Pontos de Parada (SP)']);
    const penalty = clean(row['Penalidade de Movimento (REF/DEX/MOVE)']);
    const isShield = /Shield/.test(name);
    const p = (stat) => {
      if (/Nenhuma/.test(penalty)) return 0;
      const direct = penalty.match(new RegExp(`-(\\d+) ${stat}`));
      if (direct) return Number(direct[1]);
      const grouped = penalty.match(/-(\d+) (?:REF, )?DEX\/MOVE/);
      if (grouped && stat !== 'REF') return Number(grouped[1]);
      const all = penalty.match(/-(\d+) REF, DEX, MOVE/);
      return all ? Number(all[1]) : null;
    };
    const { price } = parsePrice(row['Preço (eddies)']);
    const locations = spCell.match(/\(([^)]*Corpo[^)]*)\)/);
    items.push(base(name, 'armor', {
      description: clean(row['Descrição & Estilo Visual']), price,
      priceCategory: parsePriceCategory(row['Categoria de Preço']),
      tags: [],
      attributes: isShield
        ? { armorType: 'Shield', shieldHp: intOrNull(spCell), protectedLocations: 'Cobertura móvel (ocupa 1 mão)' }
        : {
          armorType: 'BodyArmor', sp: intOrNull(spCell),
          refPenalty: p('REF'), dexPenalty: p('DEX'), movePenalty: p('MOVE'),
          protectedLocations: locations ? locations[1] : null,
        },
    }));
  }

  for (const row of tableRows(secs['2.6']).filter((r) => /Linear Frame/.test(r['Item / Pacote']))) {
    const name = clean(row['Item / Pacote']);
    const effect = clean(row['Efeitos Mecânicos e Táticos']);
    const { price } = parsePrice(row['Preço (eddies)']);
    const body = effect.match(/BODY base para (\d+)/);
    items.push(base(name, 'armor', {
      description: effect, price, priceCategory: priceCategoryFromPrice(price), tags: ['Borgware'],
      attributes: {
        armorType: 'LinearFrame', bodyValue: body ? Number(body[1]) : null,
        ...parseHumanity(row['Perda de Humanidade (HL)']), installLocation: installLocation(row['Requisitos / Instalação']),
        requirements: clean(row['Requisitos / Instalação']), effect,
      },
    }));
  }

  for (const line of secs['4.3']) {
    const m = line.match(/^\* \*\*(.+?) \(([\d.,]+)eb\)\*\*:\s*(.*)$/);
    if (!m) continue;
    const [, name, priceText, text] = m;
    const description = clean(text);
    const hp = description.match(/(\d+) HP/);
    const sp = description.match(/SP (\d+)/);
    const body = description.match(/BODY (\d+)/);
    const price = parsePrice(`${priceText}eb`).price;
    items.push(base(clean(name), 'armor', {
      description, price, priceCategory: priceCategoryFromPrice(price), tags: ['ACPA'],
      attributes: {
        armorType: 'PoweredArmor', sp: sp ? Number(sp[1]) : null, hitPoints: hp ? Number(hp[1]) : null,
        bodyValue: body ? Number(body[1]) : null, effect: description,
      },
    }));
  }
  return items;
}

// ------------------------------------------------------------------ gear, programs, vehicle upgrades

const GEAR_TYPES = {
  Agent: 'Electronics', Airhypo: 'Medical', 'Anti-Smog Breathing Mask': 'Survival', 'Auto Ear Protectors': 'Survival',
  Binoculars: 'Tool', 'Braindance Viewer': 'Lifestyle', 'Bug Detector': 'Security', Carryall: 'Survival',
  'Chemical Analyzer': 'Tool', ChipVault: 'Security', Computer: 'Electronics', Cryopump: 'Medical', Cryotank: 'Medical',
  'Disposable Cellphone': 'Electronics', 'Drink Master 3000 / 5000': 'Lifestyle', 'Duct Tape': 'Tool',
  'Esporma Environment Suit': 'Survival', Flashlight: 'Tool', 'Forgery Kit': 'Security', 'Grapple Gun': 'Tool',
  Handcuffs: 'Security', 'HI Green Light Go Sniffer': 'Tool', 'Homing Tracer': 'Electronics',
  'Inflatable Bed & Sleep-bag': 'Survival', 'Ion Cuffs': 'Security', 'Lock Picking Set': 'Security',
  'Master Mechanic Tool Kit': 'Tool', Medscanner: 'Medical', 'Medtech Bag': 'Medical', 'Memory Chips': 'Electronics',
  'Personal CarePak': 'Lifestyle', 'Pocket Amplifier': 'Lifestyle', 'RapiDeploy Sheath': 'Security', 'Road Flare': 'Survival',
  'Rope (60m)': 'Survival', 'Scrambler / Descrambler': 'Electronics', 'Security Tech Kit': 'Security',
  'Smart Glasses': 'Electronics', 'Tech Bag': 'Tool', Techscanner: 'Tool', Techtool: 'Tool',
  'Tent & Camping Equipment': 'Survival', 'Video Camera': 'Electronics', 'Virtuality Goggles': 'Electronics',
};

const skillBonus = (text) => { const m = text.match(/\+\d+ (?:em|no|nas|de) [^.()]+/); return m ? m[0].replace(/\*/g, '').trim() : null; };

function convertGear(secs) {
  const items = [];
  for (const row of tableRows(secs['3.2'])) {
    const name = clean(row['Item']);
    const effect = clean(row['Função & Aplicação Tática']);
    const { price, note } = parsePrice(row['Preço (eddies)']);
    const type = GEAR_TYPES[name];
    if (!type) throw new Error(`Gear type missing for ${name}`);
    items.push(base(name, 'gear', {
      description: effect, price, priceNote: note, priceCategory: parsePriceCategory(row['Categoria de Preço']),
      manufacturer: manufacturerOf(name), tags: [],
      attributes: { gearType: type, skillBonus: skillBonus(effect), effect },
    }));
  }

  const programLines = subsection('Programas de Ataque & Defesa');
  for (const b of bullets(programLines)) {
    const cls = b.text.match(/^(Attacker|Defender|Booster)\./);
    items.push(base(b.name, 'gear', {
      description: b.text, ...pricePair(b.meta), tags: ['Netrunning'],
      attributes: { gearType: 'Program', gearClass: cls ? cls[1] : null, requirements: 'Cyberdeck', effect: b.text.replace(/^\w+\.\s*/, '') },
    }));
  }
  for (const b of bullets(subsection('Black ICE Ameaçadores'))) {
    const cls = b.text.match(/^(Anti-Pessoal|Anti-Programa)\./);
    items.push(base(b.name, 'gear', {
      description: b.text, ...pricePair(b.meta), tags: ['Netrunning'],
      attributes: { gearType: 'BlackIce', gearClass: cls ? cls[1] : null, effect: b.text.replace(/^[\w-]+\.\s*/, '') },
    }));
  }
  for (const b of bullets(subsection('Upgrades Veiculares Importantes'))) {
    items.push(base(b.name, 'gear', {
      description: b.text, ...pricePair(b.meta), tags: ['Veículo'],
      attributes: { gearType: 'VehicleUpgrade', requirements: 'Veículo compatível', effect: b.text },
    }));
  }
  return items;
}

function pricePair(meta) {
  const { price, note } = parsePrice(meta);
  return { price, priceNote: note, priceCategory: priceCategoryFromPrice(price) };
}

// ------------------------------------------------------------------ vehicles, drugs, HQ

function convertVehicles(secs) {
  return tableRows(secs['3.3']).map((row) => {
    const name = clean(row['Nome do Veículo / Item']);
    const cat = clean(row['Categoria']);
    const notes = clean(row['Notas & Upgrades Integrados']);
    const speed = clean(row['Velocidade']);
    let type = 'Car';
    if (/Bicycle/.test(name)) type = 'Bicycle';
    else if (/Moto|Superbike/.test(cat) || /Roadbike/.test(name)) type = 'Motorcycle';
    else if (/Aéreo/.test(cat)) type = 'Aerodyne';
    else if (/Combat/.test(name)) type = 'Tactical';
    else if (/Esportivo/.test(cat) || /High Perf/.test(name)) type = 'SportsCar';
    const sp = notes.match(/SP ?(\d+)/);
    const { price } = parsePrice(row['Preço (eddies)']);
    return base(name, 'vehicles', {
      description: notes, price, priceCategory: parsePriceCategory(row['Categoria de Preço']),
      tags: [], attributes: {
        vehicleType: type,
        speedMove: intOrNull(speed),
        topSpeedMph: (speed.match(/\((\d+) MPH\)/) || [])[1] ? Number(speed.match(/\((\d+) MPH\)/)[1]) : null,
        seats: intOrNull(row['Passageiros']),
        sdp: intOrNull(row['SDP (HP)']),
        armorSp: sp ? Number(sp[1]) : null,
        includedUpgrades: notes,
      },
    });
  });
}

const DRUG_CLASSES = {
  Synthcoke: 'Combat', 'Black Lace': 'Combat', Berserker: 'Combat', 'Prime Time': 'Social', Sixgun: 'Netrunning',
  Timewarp: 'Combat', Smash: 'Street', 'Blue Glass': 'Street',
};

function convertDrugs(secs) {
  return tableRows(secs['4.2']).map((row) => {
    const name = clean(row['Droga / Substância']);
    const primary = clean(row['Efeito Primário (Em Combate)']);
    const secondary = clean(row['Efeito Secundário / Vício']);
    const duration = primary.match(/por (\d+) (hora|horas|minuto|minutos)/);
    const hl = secondary.match(/Perda de (\d+) HL/);
    const { price } = parsePrice(row['Preço por Dose']);
    return base(name, 'drugs', {
      description: primary, price, priceNote: 'Por dose', priceCategory: priceCategoryFromPrice(price), tags: [],
      attributes: {
        classification: DRUG_CLASSES[name] ?? 'Street',
        resistDv: intOrNull(row['DV de Resistência']),
        primaryEffect: primary,
        duration: duration ? `${duration[1]} ${duration[2]}` : null,
        durationMinutes: duration ? Number(duration[1]) * (duration[2].startsWith('hora') ? 60 : 1) : null,
        sideEffects: secondary,
        addictionRisk: /vício violento/i.test(secondary) ? 'High' : null,
        humanityLoss: hl ? Number(hl[1]) : null,
      },
    });
  });
}

function convertHq(secs) {
  return bullets(secs['4.4']).map((b) => {
    const [priceText, ipText] = b.meta.split('/').map((s) => s.trim());
    const { price } = parsePrice(priceText);
    const type = /Workstation/.test(b.name) ? 'Workshop' : /Armory/.test(b.name) ? 'Armory' : /Medical/.test(b.name) ? 'Medical'
      : /Garage/.test(b.name) ? 'Garage' : /Lookout/.test(b.name) ? 'Security' : 'Living';
    return base(b.name, 'hq-upgrades', {
      description: b.text, price, priceCategory: priceCategoryFromPrice(price), tags: [],
      attributes: { upgradeType: type, improvementPoints: intOrNull(ipText), benefits: b.text },
    });
  });
}

// ------------------------------------------------------------------ assembly

function base(name, category, fields) {
  const { attributes = {}, manufacturer, ...rest } = fields;
  const cleanAttrs = Object.fromEntries(Object.entries(attributes).filter(([, v]) => v !== null && v !== undefined && v !== ''));
  return {
    name,
    category,
    manufacturer: manufacturer === undefined ? manufacturerOf(name) : manufacturer,
    ruleSystem: 'CyberpunkRed',
    dataOrigin: 'Compendium',
    ...Object.fromEntries(Object.entries(rest).filter(([, v]) => v !== null && v !== undefined && v !== '')),
    attributes: cleanAttrs,
  };
}

const slugify = (s) => s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');

function assignSlugs(items) {
  const categoryBySlug = new Map();
  const out = [];
  for (const item of items) {
    let slug = slugify(item.name);
    // The same item listed twice in one category (e.g. under two weapon tables) is kept once.
    if (categoryBySlug.get(slug) === item.category) continue;
    if (categoryBySlug.has(slug)) slug = `${slug}-${item.category}`;
    categoryBySlug.set(slug, item.category);
    out.push({ slug, ...item });
  }
  return out;
}

const secs = sections();
// Cyberware first so "requiresItem" references resolve in file order; the importer also resolves forward references.
const all = assignSlugs([
  ...convertCyberware(secs),
  ...convertWeapons(secs),
  ...convertAmmunition(secs),
  ...convertGear(secs),
  ...convertArmor(secs),
  ...convertVehicles(secs),
  ...convertDrugs(secs),
  ...convertHq(secs),
]);

// Cyberware references use names in the converter; switch them to slugs.
const slugByName = new Map(all.filter((i) => i.category === 'cyberware').map((i) => [i.name, i.slug]));
for (const item of all) {
  const ref = item.attributes.requiresItem;
  if (ref) item.attributes.requiresItem = slugByName.get(ref) ?? ref;
}

// Corrections checked against the official PDFs, one file per PDF. Slugs stay the same so favorites and
// lists keep pointing at the item. A file-level "source" applies to every entry that does not name its own.
for (const file of readdirSync(ERRATA_DIR).filter((f) => f.endsWith('.json')).sort()) {
  const errata = JSON.parse(readFileSync(resolve(ERRATA_DIR, file), 'utf8'));
  const defaults = errata.source ? { source: errata.source } : {};
  // Compendium entries that duplicate another item under a different name.
  for (const slug of errata.remove ?? []) {
    const index = all.findIndex((i) => i.slug === slug);
    if (index < 0) throw new Error(`Errata ${file}: slug "${slug}" to remove not found.`);
    all.splice(index, 1);
  }
  // Items that belong to another category: the category and its attributes are replaced wholesale,
  // everything else (slug, texts, source) is kept.
  for (const [slug, patch] of Object.entries(errata.move ?? {})) {
    const item = all.find((i) => i.slug === slug);
    if (!item) throw new Error(`Errata ${file}: slug "${slug}" to move not found.`);
    const { category, attributes, ...fields } = patch;
    if (!category || !attributes) throw new Error(`Errata ${file}: moving "${slug}" needs "category" and "attributes".`);
    Object.assign(item, fields, { category, attributes });
  }
  for (const [slug, patch] of Object.entries(errata.updates ?? {})) {
    const item = all.find((i) => i.slug === slug);
    if (!item) throw new Error(`Errata ${file}: slug "${slug}" not found in the compendium output.`);
    const { attributes = {}, ...fields } = patch;
    Object.assign(item, defaults, fields, { dataOrigin: 'Verified' });
    // null in the errata clears the value on reimport, so it is kept in the output.
    Object.assign(item.attributes, attributes);
  }
  for (const addition of errata.additions ?? []) {
    if (all.some((i) => i.slug === addition.slug)) throw new Error(`Errata ${file}: slug "${addition.slug}" already exists.`);
    all.push({ ruleSystem: 'CyberpunkRed', ...defaults, ...addition, dataOrigin: 'Verified' });
  }
}

// Each item's own text from the official books (English) replaces the summary in the description field.
// The Portuguese summaries stay in the errata files.
const bookText = JSON.parse(readFileSync(BOOK_TEXT, 'utf8')).descriptions;
for (const [slug, text] of Object.entries(bookText)) {
  const item = all.find((i) => i.slug === slug);
  if (!item) throw new Error(`book-descriptions.json: slug "${slug}" not found.`);
  item.description = text;
}

mkdirSync(dirname(OUT), { recursive: true });
writeFileSync(OUT, `${JSON.stringify(all, null, 2)}\n`, 'utf8');

const counts = all.reduce((acc, i) => ({ ...acc, [i.category]: (acc[i.category] ?? 0) + 1 }), {});
console.log(`Wrote ${all.length} items to ${OUT}`);
console.table(counts);
