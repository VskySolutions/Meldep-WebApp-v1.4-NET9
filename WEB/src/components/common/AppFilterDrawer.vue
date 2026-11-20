<template>
  <div>
    <!-- Active filter chips above the table; the Filters trigger lives in AppListHeader. -->
    <div v-if="showChips && chips.length" class="app-filter-chips q-mb-sm">
      <q-chip
        v-for="chip in chips"
        :key="chip.key"
        removable
        dense
        color="blue-1"
        text-color="primary"
        class="app-filter-chips__chip"
        @remove="$emit('remove', chip.key)"
      >
        <span class="ellipsis">{{ chip.label }}</span>
        <q-tooltip v-if="chip.label.length > 40" :delay="300">{{ chip.label }}</q-tooltip>
      </q-chip>
      <q-btn flat dense no-caps color="grey-7" label="Clear all" @click="$emit('clear')" />
    </div>

    <q-drawer v-model="open" side="right" overlay bordered :width="width" class="app-filter-drawer column no-wrap">
      <!-- Drag handle: resize the filter drawer (double-click resets); width persists in LocalStorage. -->
      <div class="app-filter-drawer__resizer" @mousedown="startResize" @dblclick="resetWidth" />

      <div class="row items-center q-pa-md bg-primary text-white">
        <div class="text-h6 text-white">{{ title }}</div>
        <q-space />
        <q-btn flat round dense color="white" icon="o_close" aria-label="Close" @click="open = false" />
      </div>
      <q-separator />
      <!-- A plain scrolling div rather than q-scroll-area: app.scss forces every q-scrollarea to
           calc(100% - 80px), which cuts the drawer's footer off. -->
      <div class="app-filter-drawer__body col scroll">
        <div class="q-pa-md app-filter-drawer__fields">
          <slot />
        </div>
      </div>
      <q-separator />
      <div class="row justify-end q-gutter-sm q-pa-md bg-grey-1">
        <q-btn flat no-caps color="grey-8" label="Clear all" @click="$emit('clear')" />
        <q-btn unelevated no-caps color="primary" label="Done" @click="open = false" />
      </div>
    </q-drawer>
  </div>
</template>

<script setup>
// The right-side filter drawer every list uses, plus the active-filter chip row it draws above the table.
import { computed } from "vue";
import { useDrawerResize, viewportWidth } from "composables/useDrawerResize";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  title: { type: String, default: "Filters" },
  // [{ key, label }] for each active filter; drives the chips.
  chips: { type: Array, default: () => [] },
  showChips: { type: Boolean, default: true }
});

const emit = defineEmits(["update:modelValue", "remove", "clear"]);

const open = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});

// Compact by default; drag the left edge to widen. A phone gets almost the whole screen.
const { width, startResize, resetWidth } = useDrawerResize({
  storageKey: "appFilterDrawerWidth",
  getDefault: () => 380,
  getMin: () => 300,
  getMax: () => Math.round(viewportWidth() * (viewportWidth() < 600 ? 0.92 : 0.6))
});
</script>

<style scoped>
.app-filter-chips {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;
}
.app-filter-chips__chip {
  max-width: 320px;
}
.app-filter-drawer__resizer {
  position: absolute;
  top: 0;
  left: 0;
  width: 6px;
  height: 100%;
  cursor: ew-resize;
  z-index: 10;
  background: transparent;
  transition: background 0.15s ease;
}
.app-filter-drawer__resizer:hover {
  background: var(--q-primary);
  opacity: 0.4;
}
/* QDrawer puts the slot inside its own scrolling content div; made a flex column so the header and the
   footer stay put and only the fields scroll. */
.app-filter-drawer :deep(.q-drawer__content) {
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.app-filter-drawer__body {
  flex: 1 1 auto;
  min-height: 0;
  overflow: auto;
}
/* Spaced with flex gap, not q-gutter: a nested gutter's negative margin never cancels. */
.app-filter-drawer__fields {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
</style>
