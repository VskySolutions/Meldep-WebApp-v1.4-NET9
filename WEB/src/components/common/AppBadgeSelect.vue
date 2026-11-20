<template>
  <!-- Read-only: just the badge. -->
  <app-option-badge v-if="readonly" :option="current" class="abs__badge abs__badge--static" />

  <!-- Editable: the badge IS the control — the same badge every other status is drawn with, not a button
       dressed up as one. -->
  <q-btn
    v-else
    flat dense no-caps padding="0"
    class="abs"
    :disable="saving"
    :aria-label="`${header}: ${current.label}`"
  >
    <app-option-badge :option="current" class="abs__badge">
      <q-spinner v-if="saving" size="13px" class="q-ml-xs" />
      <q-icon v-else name="o_expand_more" size="15px" class="q-ml-xs abs__chevron" />
    </app-option-badge>
    <q-tooltip v-if="!saving" :delay="400">Click to change</q-tooltip>

    <q-menu anchor="bottom left" self="top left" :offset="[0, 4]">
      <q-list dense class="abs__list">
        <q-item-label header class="abs__header">{{ header }}</q-item-label>
        <q-item
          v-for="choice in options"
          :key="choice.value"
          v-close-popup
          clickable
          :active="choice.value === modelValue"
          active-class="abs__item--active"
          @click="pick(choice.value)"
        >
          <!-- Each choice drawn as the badge it will become, so picking one is picking what the row will
               look like rather than reading a word and hoping. -->
          <q-item-section>
            <app-option-badge :option="choice" class="abs__badge abs__badge--static" />
          </q-item-section>
          <q-item-section side>
            <q-icon v-if="choice.value === modelValue" name="o_check" size="16px" color="primary" />
          </q-item-section>
        </q-item>
        <q-item v-if="!options.length">
          <q-item-section class="text-grey-6">No values configured</q-item-section>
        </q-item>
      </q-list>
    </q-menu>
  </q-btn>
</template>

<script setup>
// A status-like value read and written on the same badge (the "badge-as-dropdown" pattern), for quick
// edits inside a list row.
import { computed } from "vue";
import AppOptionBadge from "components/common/AppOptionBadge.vue";

const props = defineProps({
  modelValue: { type: [String, Number], default: null },
  // The resolved option list: [{ label, value, backgroundColor, textColor }].
  options: { type: Array, default: () => [] },
  // What the badge shows when the value is not in `options` — the record's own value, in its own
  // colours, so a row reads right while the list is still loading or holds a retired value.
  fallback: { type: Object, default: null },
  header: { type: String, default: "Set status" },
  readonly: { type: Boolean, default: false },
  // The row's write is in flight: disables the control rather than swapping the badge for a spinner.
  saving: { type: Boolean, default: false }
});

const emit = defineEmits(["update:modelValue"]);

const current = computed(() =>
  props.options.find((o) => o.value === props.modelValue) || props.fallback || { label: "—" });

// Picking the value it already holds is not a change; letting it through would spend a request.
const pick = (value) => {
  if (value !== props.modelValue) emit("update:modelValue", value);
};
</script>

<style scoped>
/* Nothing of the button may show — what is on screen is the badge inside it. */
.abs {
  min-height: 0;
  border-radius: 5px;
  padding: 0 !important;
}
.abs :deep(.q-btn__content) {
  flex-wrap: nowrap;
}
.abs__badge {
  display: inline-flex;
  align-items: center;
  padding: 3px 8px;
  font-size: 12px;
}
.abs__badge--static {
  cursor: default;
}
.abs__chevron {
  opacity: 0.85;
}
.abs:hover .abs__chevron {
  opacity: 1;
}
.abs__list {
  min-width: 200px;
  padding-bottom: 4px;
}
.abs__header {
  padding: 6px 16px 2px;
  font-size: 11px;
  line-height: 16px;
  color: #7b8794;
  letter-spacing: 0.3px;
  text-transform: uppercase;
}
.abs__item--active {
  background: rgba(27, 117, 171, 0.08);
}
</style>
