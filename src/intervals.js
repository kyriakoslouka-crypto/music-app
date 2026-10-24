export const intervals = [
  [0, 'Ταυτοφωνία'], [1, 'Μικρή δεύτερη'], [2, 'Μεγάλη δεύτερη'],
  [3, 'Μικρή τρίτη'], [4, 'Μεγάλη τρίτη'], [5, 'Καθαρή τέταρτη'],
  [6, 'Τρίτονο'], [7, 'Καθαρή πέμπτη'], [8, 'Μικρή έκτη'],
  [9, 'Μεγάλη έκτη'], [10, 'Μικρή έβδομη'], [11, 'Μεγάλη έβδομη'], [12, 'Οκτάβα']
].map(([semitones, label]) => ({ semitones, label }));
export const beginner = [0, 2, 4, 5, 7, 12];
export function createExercise({ allowed = beginner, mode = 'ascending', min = 48, max = 84, random = Math.random } = {}) {
  if (!Array.isArray(allowed) || !allowed.length || allowed.some(n => !Number.isInteger(n) || n < 0 || n > 12)) throw new Error('Invalid interval selection');
  if (!['ascending', 'descending', 'harmonic'].includes(mode)) throw new Error('Invalid mode');
  if (!Number.isInteger(min) || !Number.isInteger(max) || min < 21 || max > 108 || max - min < Math.max(...allowed)) throw new Error('Invalid piano range');
  const pick = size => { const r = random(); if (!(r >= 0 && r < 1)) throw new Error('Invalid random source'); return Math.floor(r * size); };
  const distance = allowed[pick(allowed.length)];
  const low = min + pick(max - min - distance + 1);
  return { semitones: distance, notes: mode === 'descending' ? [low + distance, low] : [low, low + distance], mode };
}
export function answerLabel(exercise) { return intervals.find(i => i.semitones === exercise.semitones).label; }
