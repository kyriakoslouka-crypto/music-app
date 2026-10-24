import test from 'node:test';
import assert from 'node:assert/strict';
import { intervals, createExercise, answerLabel } from '../src/intervals.js';
for (const mode of ['ascending', 'descending', 'harmonic']) {
  test(`${mode}: every interval preserves distance and piano range at random boundaries`, () => {
    for (const { semitones, label } of intervals) for (const r of [0, 0.25, 0.999999]) {
      const e = createExercise({ allowed: [semitones], mode, random: () => r });
      assert.equal(Math.abs(e.notes[1] - e.notes[0]), semitones);
      assert.ok(e.notes.every(n => n >= 48 && n <= 84));
      assert.equal(answerLabel(e), label);
      assert.ok(mode === 'descending' ? e.notes[0] >= e.notes[1] : e.notes[0] <= e.notes[1]);
    }
  });
}
test('invalid configurations fail before generation', () => {
  for (const options of [{allowed: []}, {allowed: [13]}, {mode: 'bad'}, {min: 80,max: 81}, {random: () => 1}]) assert.throws(() => createExercise(options));
});
