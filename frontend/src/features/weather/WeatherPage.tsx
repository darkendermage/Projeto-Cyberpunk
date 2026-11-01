import { useState } from 'react';
import { CloudRain, Dices, Trash2 } from 'lucide-react';
import clsx from 'clsx';
import { Badge, Button, Collapsible, EmptyState, Panel, Segmented } from '../../components/ui/ui';
import { readStorage, writeStorage } from '../../lib/hooks';
import page from '../../styles/Page.module.css';
import {
  activeConditions, BAND_LABEL, bloodRainEffect, COLD_SNAP_OR_HEAT_WAVE, duration, durationText, isWeatherRoll, pickCondition, pickStrange, rollDie,
  rollExtraCondition, rollNewCondition, rollWeather, RULES, seasonOf, seasonOfMonth, SEASONS, STRANGE, STRANGE_LABEL, strangeCondition,
  strangeDurationDice, summary, TEMPERATURE_RULES, temperatureRow, type ConditionId, type SeasonId, type StrangeRoll, type WeatherRoll,
} from './weather';
import styles from './Weather.module.css';
import { t } from '../../i18n';

const STORAGE_KEY = 'ncdb.weather';
const PAST_DAYS = 7;

interface Saved { season: SeasonId; current: WeatherRoll | null; history: WeatherRoll[] }

function load(): Saved {
  const saved = readStorage<Saved>(STORAGE_KEY, { season: seasonOfMonth(new Date().getMonth()), current: null, history: [] });
  return {
    season: SEASONS.some((s) => s.id === saved.season) ? saved.season : 'winter',
    current: isWeatherRoll(saved.current) ? saved.current : null,
    history: Array.isArray(saved.history) ? saved.history.filter(isWeatherRoll) : [],
  };
}

const Protection = ({ text }: { text?: string }) => (text ? <p className={styles.protection}><span>{t('Proteção', 'Protection')}</span> {text}</p> : null);

function GmRollButton({ condition }: { condition: ConditionId }) {
  const rule = RULES[condition].gmRoll;
  const [roll, setRoll] = useState<number | null>(null);
  if (!rule) return null;
  return (
    <div className={styles.gmRoll}>
      <Button icon={<Dices size={15} />} onClick={() => setRoll(rollDie(rule.sides))}>{rule.label}</Button>
      {roll !== null && <p aria-live="polite"><strong>{roll}</strong> {rule.outcome(roll)}</p>}
    </div>
  );
}

function ConditionCard({ condition, strange }: { condition: ConditionId; strange: StrangeRoll | null }) {
  const rule = RULES[condition];
  return (
    <article className={styles.card}>
      <p className={styles.label}>{strange ? t('Clima Estranho', 'Strange Weather') : t('Condições atuais', 'Current Conditions')}</p>
      <h2 className={styles.value}>{rule.name}</h2>
      <div className={styles.badges}>
        {strange && <Badge tone="amber">{durationText(duration(strange))}</Badge>}
        {rule.penalty && <Badge tone="red" title={t('Penalidade em testes, a critério do Mestre', 'Skill Check penalty, at the GM\'s discretion')}>{rule.penalty} {t('em testes', 'to Checks')}</Badge>}
      </div>
      <p className={styles.text}>{rule.text}</p>
      {strange && condition === 'blood-rain' && <p className={styles.rolled}><strong>{strange.bloodRain}</strong> {bloodRainEffect(strange.bloodRain)}</p>}
      {strange && condition === 'blackout' && (
        <p className={styles.rolled}><strong>{strange.blackoutArea}</strong> {t('quarteirões ou zonas ao redor do local da equipe.', 'blocks or neighborhood zones around the crew.')}</p>
      )}
      <Protection text={rule.protection} />
      <GmRollButton key={condition} condition={condition} />
    </article>
  );
}

function TemperatureCard({ weather }: { weather: WeatherRoll }) {
  const row = temperatureRow(weather);
  const event = strangeCondition(weather);
  const overridden = event === 'cold-snap' || event === 'heat-wave';
  return (
    <article className={styles.card}>
      <p className={styles.label}>{t('Temperatura', 'Temperature')}</p>
      <h2 className={clsx(styles.value, overridden && styles.overridden)}>{BAND_LABEL[row.band]}</h2>
      <p className={styles.degrees}>{row.f}°F / {row.c}°C</p>
      {overridden
        ? <p className={styles.text}>{t(`${RULES[event].name} substitui a Temperatura rolada.`, `${RULES[event].name} overrides the rolled Temperature.`)}</p>
        : <>
          <p className={styles.text}>{TEMPERATURE_RULES[row.band].text}</p>
          <Protection text={TEMPERATURE_RULES[row.band].protection} />
        </>}
    </article>
  );
}

function Result({ weather, onChange }: { weather: WeatherRoll; onChange: (next: WeatherRoll) => void }) {
  const [first, second] = activeConditions(weather);
  return (
    <Panel className={styles.result}>
      <div className={styles.cards}>
        <TemperatureCard weather={weather} />
        <ConditionCard condition={first} strange={weather.strange} />
        {second && <ConditionCard condition={second} strange={null} />}
      </div>
      {weather.strange && (
        <div className={styles.eventActions}>
          <Button onClick={() => onChange(rollNewCondition(weather))}>{t('O evento acabou: nova condição', 'Event over: new condition')}</Button>
          <Button onClick={() => onChange(rollExtraCondition(weather))}>{t('Somar uma condição ao evento', 'Add a condition to the event')}</Button>
        </div>
      )}
    </Panel>
  );
}

function Tables({ season, weather, onChange }: { season: SeasonId; weather: WeatherRoll | null; onChange: (next: WeatherRoll) => void }) {
  const rows = seasonOf(season).rows;
  const shown = weather?.season === season ? weather : null;
  // A choice fills one column: with no day on this table yet, the other column is rolled.
  const day = () => shown ?? rollWeather(season);
  return (
    <div className={styles.tables}>
      <div className={page.tableWrap}>
        <table className={page.table}>
          <thead><tr><th>1d6</th><th>{t('Temperatura', 'Temperature')}</th><th>{t('Condições atuais', 'Current Conditions')}</th></tr></thead>
          <tbody>
            {rows.map((r, i) => (
              <tr key={i}>
                <td>{i + 1}</td>
                <td>
                  <button type="button" className={clsx(styles.pick, shown?.temperature === i + 1 && styles.picked)} aria-pressed={shown?.temperature === i + 1}
                    onClick={() => onChange({ ...day(), temperature: i + 1 })}>
                    {BAND_LABEL[r.band]} ({r.f}°F/{r.c}°C)
                  </button>
                </td>
                <td>
                  <button type="button" className={clsx(styles.pick, shown?.condition === i + 1 && styles.picked)} aria-pressed={shown?.condition === i + 1}
                    onClick={() => onChange(pickCondition(day(), i + 1))}>
                    {r.condition === 'strange' ? STRANGE_LABEL : RULES[r.condition].name}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className={page.tableWrap}>
        <table className={page.table}>
          <thead><tr><th>1d10</th><th>{t('Clima Estranho', 'Strange Weather')}</th><th>{t('Duração', 'How long')}</th></tr></thead>
          <tbody>
            {STRANGE.map((s, i) => (
              <tr key={i}>
                <td>{i + 1}</td>
                <td>
                  <button type="button" className={clsx(styles.pick, shown?.strange?.condition === i + 1 && styles.picked)} aria-pressed={shown?.strange?.condition === i + 1}
                    onClick={() => onChange(pickStrange(day(), i + 1))}>
                    {s.condition === 'cold-snap-or-heat-wave' ? COLD_SNAP_OR_HEAT_WAVE : RULES[s.condition].name}
                  </button>
                </td>
                <td className={clsx(shown?.strange?.durationRow === i + 1 && styles.durationRow)}>{strangeDurationDice(s.unit)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

const ALL_CONDITIONS = Object.keys(RULES) as ConditionId[];

function AllRules() {
  return (
    <div className={styles.rules}>
      {(['cold', 'cool', 'warm', 'hot'] as const).map((band) => (
        <article key={band}>
          <h3>{BAND_LABEL[band]}</h3>
          <p className={styles.text}>{TEMPERATURE_RULES[band].text}</p>
          <Protection text={TEMPERATURE_RULES[band].protection} />
        </article>
      ))}
      {ALL_CONDITIONS.map((id) => (
        <article key={id}>
          <h3>{RULES[id].name} {RULES[id].penalty && <Badge tone="red">{RULES[id].penalty}</Badge>}</h3>
          <p className={styles.text}>{RULES[id].text}</p>
          <Protection text={RULES[id].protection} />
        </article>
      ))}
    </div>
  );
}

export function WeatherPage() {
  const [state, setState] = useState(load);
  const { season, current, history } = state;
  const save = (next: Saved) => { setState(next); writeStorage(STORAGE_KEY, next); };
  const update = (next: WeatherRoll) => save({ ...state, season: next.season, current: next });
  const roll = () => save({
    ...state,
    current: rollWeather(season),
    history: current ? [current, ...history].slice(0, PAST_DAYS) : history,
  });

  return (
    <div className={page.page}>
      <header className={page.header}>
        <h1 className="hud-title" data-glitch={t('Clima de Night City', 'Night City Weather')}>{t('Clima de Night City', 'Night City Weather')}</h1>
        <p className={page.lead}>
          {t('Role a Temperatura e as Condições do mês do jogo, ou escolha nas tabelas. Você decide o quanto usar: as regras são opcionais.',
            'Roll the Temperature and Current Conditions for the in-game month, or choose them on the tables. How much you use is your call: the rules are optional.')}
        </p>
      </header>

      <div className={styles.toolbar}>
        <Segmented ariaLabel={t('Mês do jogo', 'In-game month')} value={season} onChange={(next) => save({ ...state, season: next })}
          options={SEASONS.map((s) => ({ value: s.id, label: s.label }))} />
        <Button variant="primary" icon={<Dices size={16} />} onClick={roll}>{t('Rolar o clima', 'Roll the weather')}</Button>
      </div>

      {current
        ? <Result weather={current} onChange={update} />
        : <EmptyState icon={<CloudRain size={28} />} title={t('Nenhum clima rolado', 'No weather rolled yet')}>
          {t('Escolha o mês do jogo e role, ou clique numa linha das tabelas.', 'Pick the in-game month and roll, or click a row on the tables.')}
        </EmptyState>}

      <Collapsible title={t('Tabelas', 'Tables')} defaultOpen>
        <p className={page.note}>{t('Clique numa célula para escolher em vez de rolar.', 'Click a cell to choose instead of rolling.')}</p>
        <Tables season={season} weather={current} onChange={update} />
      </Collapsible>

      <Collapsible title={t('Todas as regras', 'All rules')}>
        <AllRules />
      </Collapsible>

      {history.length > 0 && (
        <Collapsible title={t('Dias anteriores', 'Past days')} active={history.length}>
          <ol className={styles.history}>{history.map((w, i) => <li key={i}>{summary(w)}</li>)}</ol>
          <Button variant="ghost" icon={<Trash2 size={14} />} onClick={() => save({ ...state, history: [] })}>{t('Limpar dias anteriores', 'Clear past days')}</Button>
        </Collapsible>
      )}

      <p className={page.note}>{t('Night City Weather, p. 3–5. CP:R é o Cyberpunk RED Core Rulebook.', 'Night City Weather, p. 3–5. CP:R is the Cyberpunk RED core rulebook.')}</p>
    </div>
  );
}
