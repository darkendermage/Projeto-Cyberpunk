import { describe, expect, it } from 'vitest';
import {
  activeConditions, bloodRainEffect, duration, isWeatherRoll, pickCondition, pickStrange, rollExtraCondition, rollNewCondition, rollWeather,
  RULES, SEASONS, seasonOfMonth, STRANGE, strangeCondition, summary, temperatureRow, type WeatherRoll,
} from './weather';

/** An Rng that lands on these die faces, given the sides of each die in the order they are rolled. */
const dice = (...faces: [number, number][]) => {
  let i = 0;
  return () => {
    const [face, sides] = faces[i++];
    return (face - 0.5) / sides;
  };
};

const day = (over: Partial<WeatherRoll> = {}): WeatherRoll => ({ season: 'summer', temperature: 1, condition: 1, strange: null, extraCondition: null, ...over });
const strange = (condition: number, durationRow = condition) => ({ condition, durationRow, durationDie: 3, bloodRain: 2, blackoutArea: 7 });

describe('tables', () => {
  it('prints the rows of Night City Weather p.3', () => {
    const line = (id: string) => SEASONS.find((s) => s.id === id)!.rows.map((r) => `${r.band} ${r.f}/${r.c} ${r.condition}`);
    expect(line('winter')).toEqual(['cold 35/2 clear', 'cold 40/4 light-rain-sleet', 'cold 40/4 overcast', 'cool 50/10 overcast', 'cool 50/10 heavy-rain-sleet', 'cool 60/15 strange']);
    expect(line('spring')).toEqual(['cold 40/4 clear', 'cool 50/10 light-rain', 'cool 50/10 overcast', 'cool 50/10 light-rain', 'warm 60/15 heavy-rain', 'warm 70/21 strange']);
    expect(line('summer')).toEqual(['warm 60/15 light-rain', 'warm 70/21 clear', 'warm 70/21 overcast', 'hot 80/27 overcast', 'hot 80/27 clear', 'hot 90/32 strange']);
    expect(line('autumn')).toEqual(['cool 40/4 light-rain-sleet', 'warm 60/15 clear', 'warm 60/15 overcast', 'warm 60/15 overcast', 'warm 70/21 clear', 'hot 80/27 strange']);
    expect(STRANGE.map((s) => s.unit)).toEqual(['minutes', 'minutes', 'days', 'hours', 'hours', 'minutes', 'days', 'days', 'minutes', 'days']);
  });

  it('has a rule for every condition with the penalty of the book', () => {
    const penalties = Object.fromEntries(Object.entries(RULES).filter(([, r]) => r.penalty).map(([id, r]) => [id, r.penalty]));
    expect(penalties).toEqual({
      'light-rain': -1, 'light-rain-sleet': -1, 'heavy-rain': -2, 'heavy-rain-sleet': -2, 'ash-storm': -2, 'blood-rain': -1,
      'deadly-thunderstorm': -2, 'inversion-smog': -4, 'cold-snap': -2, 'dust-storm': -2,
    });
  });
});

describe('seasons', () => {
  it('maps each month to its table', () => {
    const months = Array.from({ length: 12 }, (_, m) => seasonOfMonth(m));
    expect(months).toEqual(['winter', 'winter', 'spring', 'spring', 'spring', 'summer', 'summer', 'summer', 'autumn', 'autumn', 'autumn', 'winter']);
  });
});

describe('rolling', () => {
  it('rolls Temperature and Current Conditions apart', () => {
    const w = rollWeather('winter', dice([1, 6], [5, 6]));
    expect(temperatureRow(w)).toMatchObject({ band: 'cold', f: 35 });
    expect(activeConditions(w)).toEqual(['heavy-rain-sleet']);
    expect(w.strange).toBeNull();
  });

  it('a 6 goes to Strange Weather, with the duration from its own roll on the table', () => {
    const w = rollWeather('spring', dice([2, 6], [6, 6], [5, 10], [3, 10], [4, 6], [2, 6], [9, 10]));
    expect(activeConditions(w)).toEqual(['acid-rain']);
    expect(duration(w.strange!)).toEqual({ amount: 4, unit: 'days' });
    expect(w.strange).toMatchObject({ bloodRain: 2, blackoutArea: 9 });
  });

  it('counts the x 10 Minutes rows', () => {
    expect(duration({ ...strange(1), durationDie: 4 })).toEqual({ amount: 40, unit: 'minutes' });
    expect(duration({ ...strange(4), durationDie: 4 })).toEqual({ amount: 4, unit: 'hours' });
  });

  it('row 8 follows the Temperature', () => {
    expect(strangeCondition(day({ season: 'winter', temperature: 1, condition: 6, strange: strange(8) }))).toBe('cold-snap');
    expect(strangeCondition(day({ season: 'winter', temperature: 6, condition: 6, strange: strange(8) }))).toBe('cold-snap');
    expect(strangeCondition(day({ season: 'summer', temperature: 1, condition: 6, strange: strange(8) }))).toBe('heat-wave');
    expect(strangeCondition(day({ season: 'summer', temperature: 6, condition: 6, strange: strange(8) }))).toBe('heat-wave');
  });

  it('Blood Rain splits at 3', () => {
    expect(bloodRainEffect(3)).toContain('Acid Rain');
    expect(bloodRainEffect(4)).toContain('Biotoxin');
  });

  it('a new condition keeps the Temperature and ends the event', () => {
    const w = rollNewCondition(day({ temperature: 4, condition: 6, strange: strange(3) }), dice([2, 6]));
    expect(w).toMatchObject({ temperature: 4, condition: 2, strange: null });
  });

  it('a condition can share the sky with the event, and is never a second Strange', () => {
    const w = rollExtraCondition(day({ condition: 6, strange: strange(3) }), dice([6, 6], [6, 6], [3, 6]));
    expect(activeConditions(w)).toEqual(['flooding', 'overcast']);
  });
});

describe('choosing on the table', () => {
  it('picks a plain condition', () => {
    expect(pickCondition(day({ condition: 6, strange: strange(3) }), 2)).toMatchObject({ condition: 2, strange: null });
  });

  it('a chosen event reads as its own row', () => {
    const w = pickStrange(day(), 6, dice([1, 10], [1, 10], [2, 6], [1, 6], [1, 10]));
    expect(activeConditions(w)).toEqual(['deadly-thunderstorm']);
    expect(duration(w.strange!)).toEqual({ amount: 20, unit: 'minutes' });
  });
});

describe('saved days', () => {
  it('accepts a roll and refuses anything else', () => {
    expect(isWeatherRoll(day())).toBe(true);
    expect(isWeatherRoll(day({ condition: 6, strange: strange(2) }))).toBe(true);
    expect(isWeatherRoll(day({ condition: 6 }))).toBe(false);
    expect(isWeatherRoll(day({ season: 'monsoon' as WeatherRoll['season'] }))).toBe(false);
    expect(isWeatherRoll(day({ temperature: 7 }))).toBe(false);
    expect(isWeatherRoll(null)).toBe(false);
  });

  it('sums a day in one line, leaving out a Temperature the event overrides', () => {
    expect(summary(day({ temperature: 2, condition: 2 }))).toBe('Warm (70°F/21°C), Clear');
    expect(summary(day({ condition: 6, strange: strange(8) }))).toBe('Heat Wave');
  });
});
