import { describe, expect, it } from 'vitest';
import type { CharacterRules, CharacterSheet, LifepathQuestion } from '../../api/characterTypes';
import { rollIndex, rollMissing, rollOption, rollRelationshipCount, visibleQuestions } from './lifepath';

const q = (id: string, showIfQuestion: string | null = null, showIfValue: string | null = null): LifepathQuestion =>
  ({ id, label: id, labelPt: id, dice: '1d6', options: ['a', 'b'], showIfQuestion, showIfValue });

describe('lifepath helpers', () => {
  it('rolls every row of a table with equal reach', () => {
    const options = ['1', '2', '3', '4', '5', '6'];
    expect(rollIndex(options, () => 0)).toBe(0);
    expect(rollIndex(options, () => 0.999)).toBe(5);
    expect(rollOption(options, () => 0.5)).toBe('4');
  });

  it('counts friends and enemies as 1d10 - 7, never below zero', () => {
    expect(rollRelationshipCount(() => 0)).toBe(0);       // d10 = 1
    expect(rollRelationshipCount(() => 0.65)).toBe(0);    // d10 = 7
    expect(rollRelationshipCount(() => 0.75)).toBe(1);    // d10 = 8
    expect(rollRelationshipCount(() => 0.999)).toBe(3);   // d10 = 10
  });

  it('follows the branches of the Role flowchart', () => {
    const questions = [q('act'), q('onceInGroup', 'act', 'Solo'), q('whyLeft', 'onceInGroup', 'Yes'), q('venue')];
    expect(visibleQuestions(questions, {}).map((x) => x.id)).toEqual(['act', 'venue']);
    expect(visibleQuestions(questions, { act: 'Solo' }).map((x) => x.id)).toEqual(['act', 'onceInGroup', 'venue']);
    expect(visibleQuestions(questions, { act: 'Solo', onceInGroup: 'Yes' }).map((x) => x.id)).toEqual(['act', 'onceInGroup', 'whyLeft', 'venue']);
    expect(visibleQuestions(questions, { act: 'Group', onceInGroup: 'Yes' }).map((x) => x.id)).toEqual(['act', 'venue']);
  });

  describe('rollMissing', () => {
    const table = (id: string, dice = '1d10') => ({ id, label: id, labelPt: id, dice, page: 45, options: [`${id}-a`, `${id}-b`] });
    const rules = {
      culturalOrigins: [{ region: 'East Asian', languages: ['Japanese'] }],
      lifepathTables: [table('personality'), table('valueMost'), table('lifeGoal', 'choose'), table('clothingStyle'), table('hairstyle'), table('affectation')],
      relations: { friendRelationship: ['f'], enemyWho: ['w'], enemyCause: ['c'], enemyResources: ['r'], enemyRevenge: ['v'], loveOutcome: ['l'], source: '' },
      roles: [{
        id: 'Rockerboy',
        lifepath: [
          { ...q('act'), options: ['Solo', 'Group'] },
          { ...q('whyLeft', 'act', 'Group'), options: ['Fight'] },
          { ...q('venue'), dice: 'choose', options: ['Club'] },
          { ...q('leftBecause', 'venue', 'Club'), options: ['Money'] },
        ],
      }],
    } as unknown as CharacterRules;
    const blank = {
      lifepath: { culturalRegion: null, answers: { personality: 'escolhida à mão' }, relationships: [] },
      style: { outfitItems: [] },
      roleLifepath: {},
    } as unknown as CharacterSheet;
    const high = () => 0.99; // last option of every table, d10 = 10 (three of each relationship)

    it('fills only what is empty and leaves "choose" tables to the player', () => {
      const { sheet, filled } = rollMissing(blank, rules, 'Rockerboy', high, () => 'id');
      expect(sheet.lifepath.answers.personality).toBe('escolhida à mão');
      expect(sheet.lifepath.answers.valueMost).toBe('valueMost-b');
      expect(sheet.lifepath.answers.lifeGoal).toBeUndefined();
      expect(sheet.lifepath.culturalRegion).toBe('East Asian');
      expect(sheet.style.clothingStyle).toBe('clothingStyle-b');
      // 1 region + 1 table + 3 style + 9 relationships + 2 Role questions
      expect(filled).toBe(16);
    });

    it('follows the Role branches it opens and stops at the ones that need a choice', () => {
      const answers = rollMissing(blank, rules, 'Rockerboy', high, () => 'id').sheet.roleLifepath.Rockerboy;
      expect(answers).toEqual({ act: 'Group', whyLeft: 'Fight' });
    });

    it('rolls friends, lovers and enemies once, even when the roll was zero', () => {
      const low = () => 0; // d10 = 1: nobody
      const first = rollMissing(blank, rules, null, low, () => 'id');
      expect(first.sheet.lifepath.relationships).toHaveLength(0);
      const again = rollMissing(first.sheet, rules, null, high, () => 'id');
      expect(again.sheet.lifepath.relationships).toHaveLength(0);
      expect(again.filled).toBe(0);
    });
  });
});
