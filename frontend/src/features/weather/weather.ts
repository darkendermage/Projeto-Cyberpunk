// Night City Weather (R. Talsorian, 2021): the tables of p.3 and the rules of p.4-5. The GM's roll stays a set of plain
// dice values, so a saved day shows the same text in either language and a picked table row replaces a single die.
import { t } from '../../i18n';
import type { Rng } from '../characters/lifepath';

export type SeasonId = 'winter' | 'spring' | 'summer' | 'autumn';
export type Band = 'cold' | 'cool' | 'warm' | 'hot';
type TableCondition = 'clear' | 'overcast' | 'light-rain' | 'light-rain-sleet' | 'heavy-rain' | 'heavy-rain-sleet';
export type ConditionId = TableCondition | 'radioactive-windstorm' | 'ash-storm' | 'flooding' | 'blood-rain' | 'acid-rain'
  | 'deadly-thunderstorm' | 'inversion-smog' | 'cold-snap' | 'heat-wave' | 'dust-storm' | 'blackout';
export type DurationUnit = 'minutes' | 'hours' | 'days';

export interface TableRow { band: Band; f: number; c: number; condition: TableCondition | 'strange' }
export interface Season { id: SeasonId; label: string; rows: TableRow[] }
export interface StrangeRow { condition: ConditionId | 'cold-snap-or-heat-wave'; unit: DurationUnit }

const row = (band: Band, f: number, c: number, condition: TableRow['condition']): TableRow => ({ band, f, c, condition });

export const SEASONS: Season[] = [
  { id: 'winter', label: t('Dez a Fev', 'Dec to Feb'), rows: [
    row('cold', 35, 2, 'clear'), row('cold', 40, 4, 'light-rain-sleet'), row('cold', 40, 4, 'overcast'),
    row('cool', 50, 10, 'overcast'), row('cool', 50, 10, 'heavy-rain-sleet'), row('cool', 60, 15, 'strange'),
  ] },
  { id: 'spring', label: t('Mar a Mai', 'Mar to May'), rows: [
    row('cold', 40, 4, 'clear'), row('cool', 50, 10, 'light-rain'), row('cool', 50, 10, 'overcast'),
    row('cool', 50, 10, 'light-rain'), row('warm', 60, 15, 'heavy-rain'), row('warm', 70, 21, 'strange'),
  ] },
  { id: 'summer', label: t('Jun a Ago', 'Jun to Aug'), rows: [
    row('warm', 60, 15, 'light-rain'), row('warm', 70, 21, 'clear'), row('warm', 70, 21, 'overcast'),
    row('hot', 80, 27, 'overcast'), row('hot', 80, 27, 'clear'), row('hot', 90, 32, 'strange'),
  ] },
  { id: 'autumn', label: t('Set a Nov', 'Sep to Nov'), rows: [
    row('cool', 40, 4, 'light-rain-sleet'), row('warm', 60, 15, 'clear'), row('warm', 60, 15, 'overcast'),
    row('warm', 60, 15, 'overcast'), row('warm', 70, 21, 'clear'), row('hot', 80, 27, 'strange'),
  ] },
];

export const STRANGE: StrangeRow[] = [
  { condition: 'radioactive-windstorm', unit: 'minutes' },
  { condition: 'ash-storm', unit: 'minutes' },
  { condition: 'flooding', unit: 'days' },
  { condition: 'blood-rain', unit: 'hours' },
  { condition: 'acid-rain', unit: 'hours' },
  { condition: 'deadly-thunderstorm', unit: 'minutes' },
  { condition: 'inversion-smog', unit: 'days' },
  { condition: 'cold-snap-or-heat-wave', unit: 'days' },
  { condition: 'dust-storm', unit: 'minutes' },
  { condition: 'blackout', unit: 'days' },
];

export const BAND_LABEL: Record<Band, string> = {
  cold: t('Frio', 'Cold'), cool: t('Fresco', 'Cool'), warm: t('Ameno', 'Warm'), hot: t('Quente', 'Hot'),
};

export const STRANGE_LABEL = t('Estranho', 'Strange');
export const COLD_SNAP_OR_HEAT_WAVE = t('Cold Snap (Fresco ou Frio) ou Heat Wave (Ameno ou Quente)', 'Cold Snap (Cool or Cold) or Heat Wave (Warm or Hot)');

/** A roll the GM repeats during play: the rule says when, the page shows the outcome. */
export interface GmRoll { label: string; sides: number; outcome: (roll: number) => string }

export interface WeatherRule {
  name: string;
  page: number;
  /** Skill Check penalty the GM may apply, when the rule offers one. */
  penalty?: number;
  text: string;
  protection?: string;
  gmRoll?: GmRoll;
}

const NOSE = 'Nasal Filters, Anti-Smog Breathing Mask';
const VISIBILITY = t('inclusive Perception e ataques à distância', 'including Perception and ranged attacks');

export const RULES: Record<ConditionId, WeatherRule> = {
  clear: { name: t('Céu limpo', 'Clear'), page: 3, text: t('Sem efeito de regra.', 'No rules effect.') },
  overcast: { name: t('Nublado', 'Overcast'), page: 3, text: t('Sem efeito de regra.', 'No rules effect.') },
  'light-rain': {
    name: t('Chuva fraca', 'Light Rain'), page: 5, penalty: -1,
    text: t('Não atrapalha a visão, mas deixa as superfícies escorregadias, como em Athletics para escalar uma cerca molhada ou Drive Land Vehicle no asfalto molhado.',
      'Visibility holds, but surfaces get slick: think Athletics on a wet fence or Drive Land Vehicle on a wet road.'),
  },
  'light-rain-sleet': {
    name: t('Chuva fraca ou gelada', 'Light Rain/Sleet'), page: 5, penalty: -1,
    text: t('Não atrapalha a visão, mas deixa as superfícies escorregadias, como em Athletics para escalar uma cerca molhada ou Drive Land Vehicle no asfalto molhado.',
      'Visibility holds, but surfaces get slick: think Athletics on a wet fence or Drive Land Vehicle on a wet road.'),
  },
  'heavy-rain': {
    name: t('Chuva forte', 'Heavy Rain'), page: 5, penalty: -2,
    text: t(`Superfícies escorregadias e pouca visibilidade, ${VISIBILITY}.`, `Slick surfaces and poor visibility, ${VISIBILITY}.`),
  },
  'heavy-rain-sleet': {
    name: t('Chuva forte ou gelada', 'Heavy Rain/Sleet'), page: 5, penalty: -2,
    text: t(`Superfícies escorregadias e pouca visibilidade, ${VISIBILITY}.`, `Slick surfaces and poor visibility, ${VISIBILITY}.`),
  },
  'radioactive-windstorm': {
    name: 'Radioactive Windstorm', page: 5,
    text: t('Sem proteção, quem pegar a rajada conta como exposto a radiação de alto nível (CP:R p.181). As rajadas começam quando o Mestre decidir.',
      'Anyone unprotected in a burst counts as exposed to high level radiation (CP:R p.181). Bursts begin when the GM says so.'),
    protection: 'Radiation Suit',
    gmRoll: { label: t('Duração da rajada', 'Burst length'), sides: 6, outcome: (r) => t(`Dura ${r} Round(s).`, `Lasts ${r} Round(s).`) },
  },
  'ash-storm': {
    name: 'Ash Storm', page: 4, penalty: -2,
    text: t(`Depois de 1 minuto sem proteção, conta como exposição a um Vial of Poison (CP:R p.355) e causa o Critical Injury Foreign Object, sem o Bonus Damage inicial. Esse ferimento só pode ser tratado fora da tempestade. A penalidade vale para qualquer teste adequado, ${VISIBILITY}.`,
      `After 1 minute unprotected, it counts as exposure to a Vial of Poison (CP:R p.355) and inflicts the Foreign Object Critical Injury, without the initial Bonus Damage. That injury can only be treated outside the storm. The penalty applies to any fitting Check, ${VISIBILITY}.`),
    protection: NOSE,
  },
  flooding: {
    name: 'Flooding', page: 5,
    text: t('Na rua a água raramente passa de alguns centímetros, mas enche porões e túneis. Atravessar áreas muito alagadas segue a regra de "other form of movement" (CP:R p.169).',
      'At street level the water rarely tops a few inches, but it fills basements and tunnels. Wading through deep water uses the "other form of movement" rule (CP:R p.169).'),
  },
  'blood-rain': {
    name: 'Blood Rain', page: 4, penalty: -1,
    text: t(`Chuva gordurosa e rosada, cheia de toxinas. O efeito depende de 1d6, já rolado abaixo. A penalidade vale para qualquer teste adequado, ${VISIBILITY}.`,
      `Greasy pink rain, full of toxins. Its effect depends on 1d6, already rolled below. The penalty applies to any fitting Check, ${VISIBILITY}.`),
    protection: t('Umbrella ou Waterproof Jacket Lining dão +2 em Resist Torture/Drugs', 'Umbrella or Waterproof Jacket Lining give +2 to Resist Torture/Drugs'),
  },
  'acid-rain': {
    name: 'Acid Rain', page: 4,
    text: t('Cada minuto inteiro sob a chuva sem proteção reduz em 1 o SP de toda armadura vestida.', 'Each full minute in the rain unprotected ablates all worn armor by 1 SP.'),
    protection: 'Umbrella, Militech Tactical Umbrella, Waterproof Jacket Lining',
  },
  'deadly-thunderstorm': {
    name: 'Deadly Thunderstorm', page: 5, penalty: -2,
    text: t(`Quem for atingido sofre 6d6 no corpo, e o raio conta como uma flashbang (CP:R p.346) centrada nele. Sem estrutura por perto, o raio cai no Personagem mais alto ou em quem segura uma arma de metal de duas mãos, e também atinge quem estiver tocando esse alvo. A penalidade vale para qualquer teste adequado, ${VISIBILITY}.`,
      `Anyone struck takes 6d6 to the body, and the bolt counts as a flashbang (CP:R p.346) centered on them. With no structure nearby it hits the tallest Character or whoever holds a two-handed metal weapon, and anyone touching that target too. The penalty applies to any fitting Check, ${VISIBILITY}.`),
    gmRoll: {
      label: t('Raio (a cada 10 minutos)', 'Lightning (every 10 minutes)'), sides: 6,
      outcome: (r) => (r <= 3
        ? t('O raio cai longe.', 'The lightning strikes far away.')
        : t('O raio atinge a estrutura ou o elemento natural mais alto por perto.', 'The lightning strikes the tallest nearby structure or natural feature.')),
    },
  },
  'inversion-smog': {
    name: 'Inversion Smog', page: 5, penalty: -4,
    text: t(`Depois de 1 minuto sem proteção, conta como exposição a um Vial of Poison (CP:R p.355). A penalidade vale para qualquer teste adequado, ${VISIBILITY}.`,
      `After 1 minute unprotected, it counts as exposure to a Vial of Poison (CP:R p.355). The penalty applies to any fitting Check, ${VISIBILITY}.`),
    protection: NOSE,
  },
  'cold-snap': {
    name: 'Cold Snap', page: 4, penalty: -2,
    text: t('Abaixo de zero, no lugar da Temperatura rolada. As regras de Exposure (CP:R p.181) valem. O gelo negro dá a penalidade em testes adequados feitos sobre superfícies congeladas.',
      'Below freezing, overriding the rolled Temperature. The Exposure rules (CP:R p.181) apply. Black ice gives the penalty to fitting Checks made on icy surfaces.'),
    protection: 'Cold-Weather Jacket Lining',
  },
  'heat-wave': {
    name: 'Heat Wave', page: 5,
    text: t('Mais de 100°F, no lugar da Temperatura rolada. As regras de Exposure (CP:R p.181) valem. Aumenta em 2 qualquer Armor Penalty de REF, DEX e MOVE: -2 vira -4.',
      'Over 100°F, overriding the rolled Temperature. The Exposure rules (CP:R p.181) apply. Any Armor Penalty to REF, DEX and MOVE grows by 2: -2 becomes -4.'),
    protection: t('Hot-Weather Jacket Lining conta como proteção e reduz em 1 o aumento', 'Hot-Weather Jacket Lining counts as protection and lowers the increase by 1'),
  },
  'dust-storm': {
    name: 'Dust Storm', page: 5, penalty: -2,
    text: t(`Depois de 5 minutos sem proteção, causa o Critical Injury Foreign Object, sem o Bonus Damage inicial. Esse ferimento só pode ser tratado fora da tempestade. A penalidade vale para qualquer teste adequado, ${VISIBILITY}.`,
      `After 5 minutes unprotected, it inflicts the Foreign Object Critical Injury, without the initial Bonus Damage. That injury can only be treated outside the storm. The penalty applies to any fitting Check, ${VISIBILITY}.`),
    protection: NOSE,
  },
  blackout: {
    name: 'Blackout', page: 4,
    text: t('Não é clima, mas costuma acompanhar o clima extremo. Prédios sem gerador ficam sem luz, e Agents não fazem ligações nem acessam o Data Pool. A área afetada é decisão do Mestre, ou o valor sorteado abaixo.',
      'Not weather as such, but it often follows extreme weather. Buildings without a generator lose power, and Agents can neither call nor reach the Data Pool. The GM decides the area, or uses the value rolled below.'),
  },
};

export const TEMPERATURE_RULES: Record<Band, { text: string; protection?: string }> = {
  cold: {
    text: t('Quem passa a maior parte do dia ao ar livre, ou num lugar sem aquecimento, sem proteção adequada sofre dano por Exposure (CP:R p.181).',
      'Anyone spending most of the day outside, or somewhere unheated, without proper gear takes Exposure damage (CP:R p.181).'),
    protection: 'Cold-Weather Jacket Lining',
  },
  cool: { text: t('Confortável, embora possa fazer um pouco de frio.', 'Comfortable, if a touch chilly on some days.') },
  warm: { text: t('Confortável, embora possa ficar um pouco abafado.', 'Comfortable, if a touch muggy on some days.') },
  hot: {
    text: t('Aumenta em 1 qualquer Armor Penalty de REF, DEX e MOVE: -2 vira -3.', 'Any Armor Penalty to REF, DEX and MOVE grows by 1: -2 becomes -3.'),
    protection: t('Hot-Weather Jacket Lining reduz em 1 o aumento', 'Hot-Weather Jacket Lining lowers the increase by 1'),
  },
};

// ------------------------------------------------------------------ rolls

export interface StrangeRoll {
  /** 1d10 on the Strange Weather table, for the condition. */
  condition: number;
  /** A second 1d10 on the same table: the book rolls the duration apart from the condition. */
  durationRow: number;
  /** The 1d6 of the duration on that row. */
  durationDie: number;
  /** 1d6 of Blood Rain and 1d10 of the Blackout area, rolled up front so the result is complete. */
  bloodRain: number;
  blackoutArea: number;
}

export interface WeatherRoll {
  season: SeasonId;
  temperature: number;
  condition: number;
  strange: StrangeRoll | null;
  /** A plain condition sharing the sky with a Strange event (Overcast during Flooding, says the book). */
  extraCondition: number | null;
}

const die = (sides: number, rng: Rng) => Math.floor(rng() * sides) + 1;

export const rollDie = (sides: number, rng: Rng = Math.random) => die(sides, rng);

export function rollStrange(rng: Rng = Math.random): StrangeRoll {
  return { condition: die(10, rng), durationRow: die(10, rng), durationDie: die(6, rng), bloodRain: die(6, rng), blackoutArea: die(10, rng) };
}

export function rollWeather(season: SeasonId, rng: Rng = Math.random): WeatherRoll {
  const temperature = die(6, rng);
  const condition = die(6, rng);
  return { season, temperature, condition, strange: condition === 6 ? rollStrange(rng) : null, extraCondition: null };
}

/** The event ended: a new roll for Current Conditions, with the same Temperature. */
export function rollNewCondition(weather: WeatherRoll, rng: Rng = Math.random): WeatherRoll {
  const condition = die(6, rng);
  return { ...weather, condition, strange: condition === 6 ? rollStrange(rng) : null, extraCondition: null };
}

/** The event goes on under a plain condition. The book leaves a second Strange out of it, so a 6 is rolled again. */
export function rollExtraCondition(weather: WeatherRoll, rng: Rng = Math.random): WeatherRoll {
  let extra = die(6, rng);
  while (extra === 6) extra = die(6, rng);
  return { ...weather, extraCondition: extra };
}

export function pickCondition(weather: WeatherRoll, row: number, rng: Rng = Math.random): WeatherRoll {
  return { ...weather, condition: row, strange: row === 6 ? weather.strange ?? rollStrange(rng) : null, extraCondition: null };
}

/** Choosing an event on the table keeps its own duration row, so the choice reads as the table prints it. */
export function pickStrange(weather: WeatherRoll, row: number, rng: Rng = Math.random): WeatherRoll {
  const strange = weather.strange ?? rollStrange(rng);
  return { ...weather, condition: 6, strange: { ...strange, condition: row, durationRow: row } };
}

// ------------------------------------------------------------------ reading a roll

export const seasonOf = (id: SeasonId) => SEASONS.find((s) => s.id === id) ?? SEASONS[0];

/** The table of a month, counted from 0 as in Date.getMonth(). */
export const seasonOfMonth = (month: number): SeasonId => SEASONS[Math.floor(((month + 1) % 12) / 3)].id;

const isRoll = (n: unknown, sides: number) => Number.isInteger(n) && (n as number) >= 1 && (n as number) <= sides;

/** A saved day can be from an older version or edited by hand: only well-formed rolls are shown. */
export function isWeatherRoll(value: unknown): value is WeatherRoll {
  const w = value as WeatherRoll | null;
  if (!w || !SEASONS.some((s) => s.id === w.season) || !isRoll(w.temperature, 6) || !isRoll(w.condition, 6)) return false;
  if (w.extraCondition !== null && !isRoll(w.extraCondition, 5)) return false;
  if (w.condition !== 6) return w.strange === null;
  const s = w.strange;
  return !!s && isRoll(s.condition, 10) && isRoll(s.durationRow, 10) && isRoll(s.durationDie, 6) && isRoll(s.bloodRain, 6) && isRoll(s.blackoutArea, 10);
}

export const temperatureRow = (w: WeatherRoll) => seasonOf(w.season).rows[w.temperature - 1];

/** Row 8 of Strange Weather depends on the Temperature: Cool or Cold gives a Cold Snap, Warm or Hot a Heat Wave. */
export function strangeCondition(w: WeatherRoll): ConditionId | null {
  if (!w.strange) return null;
  const { condition } = STRANGE[w.strange.condition - 1];
  if (condition !== 'cold-snap-or-heat-wave') return condition;
  const { band } = temperatureRow(w);
  return band === 'cold' || band === 'cool' ? 'cold-snap' : 'heat-wave';
}

/** The conditions in effect, the Strange event first. */
export function activeConditions(w: WeatherRoll): ConditionId[] {
  const rows = seasonOf(w.season).rows;
  const plain = (r: number) => rows[r - 1].condition as TableCondition;
  const event = strangeCondition(w);
  if (!event) return [plain(w.condition)];
  return w.extraCondition ? [event, plain(w.extraCondition)] : [event];
}

export function duration(strange: StrangeRoll): { amount: number; unit: DurationUnit } {
  const { unit } = STRANGE[strange.durationRow - 1];
  return { amount: unit === 'minutes' ? strange.durationDie * 10 : strange.durationDie, unit };
}

export function durationText({ amount, unit }: { amount: number; unit: DurationUnit }) {
  const one = amount === 1;
  if (unit === 'minutes') return t(`${amount} minutos`, `${amount} minutes`);
  if (unit === 'hours') return one ? t('1 hora', '1 hour') : t(`${amount} horas`, `${amount} hours`);
  return one ? t('1 dia', '1 day') : t(`${amount} dias`, `${amount} days`);
}

export const strangeDurationDice = (unit: DurationUnit) => ({
  minutes: t('1d6 x 10 minutos', '1d6 x 10 Minutes'), hours: t('1d6 horas', '1d6 Hours'), days: t('1d6 dias', '1d6 Days'),
}[unit]);

export const bloodRainEffect = (roll: number) => (roll <= 3
  ? t('Age como Acid Rain, mas com um cheiro muito pior.', 'Acts just like Acid Rain, but smells much worse.')
  : t('Uma vez por minuto, quem estiver exposto sem proteção conta como dosado com um Vial of Biotoxin (CP:R p.355).', 'Once per minute, anyone exposed without protection counts as dosed with a Vial of Biotoxin (CP:R p.355).'));

/** One line for the log of past days. */
export function summary(w: WeatherRoll) {
  const event = strangeCondition(w);
  const row = temperatureRow(w);
  const temperature = event === 'cold-snap' || event === 'heat-wave' ? '' : `${BAND_LABEL[row.band]} (${row.f}°F/${row.c}°C), `;
  return `${temperature}${activeConditions(w).map((c) => RULES[c].name).join(' + ')}`;
}
