import './style.css';
import { intervals, beginner, createExercise, answerLabel } from './intervals.js';
import { Piano } from './audio.js';
const $ = id => document.getElementById(id);
const piano = new Piano();
let exercise = null, answered = false, correct = 0, total = 0, busy = false;
const selected = () => $('level').value === 'all' ? intervals.map(i => i.semitones) : beginner;
function renderChoices() {
  $('choices').replaceChildren(...intervals.filter(i => selected().includes(i.semitones)).map(i => {
    const label = document.createElement('label');
    const input = document.createElement('input');
    input.type = 'radio'; input.name = 'interval'; input.value = i.semitones; input.required = true;
    label.append(input, document.createTextNode(i.label)); return label;
  }));
}
async function play() {
  try { await piano.play(exercise, Number($('volume').value) / 100); return true; }
  catch { $('feedback').textContent = 'Ο ήχος δεν είναι διαθέσιμος. Έλεγξε τη συσκευή ήχου και πάτησε Ακρόαση ξανά.'; return false; }
}
$('next').onclick = async () => {
  if (busy) return;
  busy = true;
  try {
    exercise = createExercise({ allowed: selected(), mode: $('mode').value });
    answered = false; renderChoices(); $('answers').disabled = false; $('check').disabled = false; $('replay').disabled = false;
    $('feedback').textContent = 'Νέα άσκηση. Άκουσε και επίλεξε διάστημα.';
    if (await play()) $('choices').querySelector('input').focus();
  } finally { busy = false; }
};
$('replay').onclick = () => exercise && play();
$('stop').onclick = () => piano.stop();
$('answer-form').onsubmit = event => {
  event.preventDefault();
  if (!exercise || answered) return;
  const value = new FormData(event.currentTarget).get('interval');
  if (value === null) return;
  answered = true; total++;
  const success = Number(value) === exercise.semitones;
  if (success) correct++;
  $('feedback').textContent = `${success ? 'Σωστά!' : 'Λάθος.'} Το διάστημα είναι: ${answerLabel(exercise)}. Πάτησε Νέα άσκηση για συνέχεια.`;
  $('score').textContent = `Σωστά: ${correct} από ${total}`;
  $('check').disabled = true;
  $('next').focus();
};
for (const id of ['level', 'mode']) $(id).onchange = () => {
  piano.stop(); exercise = null; renderChoices(); $('answers').disabled = true; $('check').disabled = true; $('replay').disabled = true;
  $('feedback').textContent = 'Οι ρυθμίσεις άλλαξαν. Πάτησε Νέα άσκηση.';
};
$('samples').onclick = async () => {
  $('samples').disabled = true; $('instrument').textContent = 'Φόρτωση δειγμάτων πιάνου…';
  try { await piano.loadSamples(); $('instrument').textContent = 'Ήχος: δειγματοληπτημένο πιάνο Salamander.'; }
  catch { $('instrument').textContent = 'Η φόρτωση απέτυχε. Ο προηγούμενος ήχος παραμένει διαθέσιμος. Μπορείς να δοκιμάσεις ξανά.'; }
  finally { $('samples').disabled = false; }
};
renderChoices();
