<template>
  <q-tr :props="scope || undefined" :class="{ 'rdm-row--new': isNew, 'rdm-row--failed': !!row.error }" @focusout="onFocusOut">
    <q-td v-for="col in cols" :key="col.name" :props="scope || undefined" :class="scope ? undefined : `text-${col.align || 'left'}`">
      <!-- Source and target: text until pointed at or typed in. -->
      <template v-if="col.name === 'source' || col.name === 'target'">
        <input
          v-if="editable"
          :ref="(el) => { if (col.name === 'source') sourceInput = el; }"
          :value="row[col.name]"
          type="text"
          maxlength="500"
          :class="['rdm-row__input', { 'rdm-row__input--missing': row.touched && !row[col.name].trim() }]"
          :placeholder="isNew ? col.label : ''"
          :aria-label="col.label"
          :title="row[col.name]"
          @input="$emit('update', col.name, $event.target.value)"
          @keydown.enter.prevent="$emit('enter')"
          @keydown.esc="$emit('revert', col.name)"
        >
        <span v-else class="rdm-row__text">{{ row[col.name] || "—" }}</span>
      </template>

      <template v-else-if="col.name === 'direction'">
        <app-badge-select
          :model-value="row.directionId"
          :options="directionOptions"
          :fallback="directionFallback"
          :readonly="!editable"
          header="Integration direction"
          :class="{ 'rdm-row__direction--missing': editable && row.touched && !row.directionId }"
          @update:model-value="$emit('direction', $event)"
        />
      </template>

      <!-- Actions: the mapping's notes (with their count), then what the row itself is doing, then delete. -->
      <template v-else-if="col.name === 'actions'">
        <q-btn
          v-if="row.id && (editable || row.notesCount)"
          flat
          round
          dense
          :color="row.notesCount ? 'primary' : 'grey-7'"
          icon="o_sticky_note_2"
          :aria-label="notesLabel"
          class="rdm-row__notes"
          @click="$emit('notes')"
        >
          <q-badge v-if="row.notesCount" floating rounded color="primary" class="rdm-row__note-count">{{ row.notesCount }}</q-badge>
          <q-tooltip>{{ notesLabel }}</q-tooltip>
        </q-btn>
        <template v-if="editable">
          <q-spinner v-if="row.saving" size="16px" color="primary" class="rdm-row__busy" />
          <q-btn v-else-if="row.error" flat round dense color="negative" icon="o_error" aria-label="Not saved. Try again" class="rdm-row__retry" @click="$emit('retry')">
            <q-tooltip max-width="320px">Not saved: {{ row.error }} Click to try again.</q-tooltip>
          </q-btn>
          <q-btn flat round dense color="grey-7" icon="o_delete" :disable="row.saving" :aria-label="isNew ? 'Discard this row' : 'Delete mapping'" class="rdm-row__delete" @click="$emit('remove')">
            <q-tooltip>{{ isNew ? "Discard this row" : "Delete mapping" }}</q-tooltip>
          </q-btn>
        </template>
      </template>

      <template v-else>{{ row[col.name] || "—" }}</template>
    </q-td>
  </q-tr>
</template>

<script setup>
// One line of the data mapping grid: a saved mapping (drawn by the table, with its `scope`) or the line a
// new mapping is typed into (no scope: it sits above the table's own rows). The line only reports what
// the reader does; the grid holds the values and decides what to save.
import { computed } from "vue";
import AppBadgeSelect from "components/common/AppBadgeSelect.vue";

const props = defineProps({
  row: { type: Object, required: true },
  // The columns on show, in the reader's order.
  cols: { type: Array, required: true },
  // The table's own row scope (alignment, widths); absent for the new-mapping line.
  scope: { type: Object, default: null },
  editable: { type: Boolean, default: false },
  // The new-mapping line: placeholders in its cells, "Discard" for its delete.
  isNew: { type: Boolean, default: false },
  directionOptions: { type: Array, default: () => [] }
});

const emit = defineEmits(["update", "enter", "leave", "revert", "direction", "notes", "retry", "remove"]);

const NEUTRAL = { backgroundColor: "#e8eef1", textColor: "#3a4d54" };
// What the badge reads before the list has loaded, or for a row that has no direction yet.
const directionFallback = computed(() => (props.row.directionLabel
  ? { label: props.row.directionLabel, ...NEUTRAL }
  : { label: props.editable ? "Set direction" : "—", backgroundColor: "#ffffff", textColor: "#647a82" }));

const notesLabel = computed(() => (props.row.notesCount ? `Notes (${props.row.notesCount})` : (props.editable ? "Add a note" : "No notes")));

// Focus left the line altogether: not for one of its own cells, nor for the direction menu it opened.
const onFocusOut = (event) => {
  const next = event.relatedTarget;
  if (next && (event.currentTarget.contains(next) || next.closest?.(".q-menu"))) return;
  emit("leave");
};

let sourceInput = null;
defineExpose({ focusSource: () => sourceInput?.focus() });
</script>

<style scoped>
/* A cell that reads as text until it is pointed at or typed in. The box reaches 8px left of the cell's
   content edge so the text inside it lines up with the column heading. */
.rdm-row__input {
  width: calc(100% + 8px);
  margin-left: -8px;
  height: 30px;
  padding: 0 7px;
  border: 1px solid transparent;
  border-radius: 6px;
  background: transparent;
  color: var(--ink-900, #16262c);
  font: inherit;
  text-overflow: ellipsis;
  outline: none;
}
.rdm-row__input:hover {
  border-color: var(--line, #dde6e8);
  background: #fff;
}
.rdm-row__input:focus {
  border-color: var(--q-primary);
  background: #fff;
  box-shadow: 0 0 0 2px rgba(27, 117, 171, 0.12);
}
.rdm-row__input::placeholder {
  color: var(--ink-300, #a6b8bd);
}
.rdm-row__input--missing,
.rdm-row__input--missing:hover {
  border-color: var(--q-negative);
  background: #fff;
}
.rdm-row__direction--missing :deep(.app-option-badge) {
  outline: 1px solid var(--q-negative);
  outline-offset: 1px;
}
.rdm-row__text {
  display: block;
  overflow-wrap: anywhere;
}
.rdm-row--new :deep(td) {
  background: var(--brand-050, #f3f8fb);
}
.rdm-row--failed :deep(td) {
  background: #fff6f5;
}
.rdm-row__note-count {
  font-size: 10px;
  font-weight: 600;
  padding: 2px 5px;
}
.rdm-row__busy {
  margin: 0 8px;
}
.rdm-row__delete:hover {
  color: var(--q-negative) !important;
}
@media (max-width: 599px) {
  .rdm-row__input {
    font-size: 16px;
  }
}
</style>
