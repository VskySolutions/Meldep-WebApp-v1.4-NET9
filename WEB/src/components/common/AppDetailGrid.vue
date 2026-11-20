<template>
  <div class="dg">
    <div v-for="row in visibleRows" :key="row.label" class="dg__item" :class="{ 'dg__item--wide': row.wide }">
      <div class="record-label">
        {{ row.label }}
        <app-info-tip v-if="row.info" :text="row.info" size="14px" />
      </div>
      <!-- Rich text arrives as markup and has to render as such. -->
      <!-- eslint-disable-next-line vue/no-v-html -->
      <div v-if="row.html" class="record-value dg__value--rich RichTextEditor" v-html="row.value || '—'" />
      <div v-else class="record-value">
        <slot :name="row.slot" :row="row">{{ display(row.value) }}</slot>
      </div>
    </div>
  </div>
</template>

<script setup>
// Read-only presentation of a record: label above value, never a disabled input.
import { computed } from "vue";
import AppInfoTip from "components/common/AppInfoTip.vue";

const props = defineProps({
  // [{ label, value, wide?, html?, hideWhenEmpty?, info?, slot? }] — `slot` names a scoped slot that
  // draws the value (a link, a badge) instead of plain text.
  rows: { type: Array, default: () => [] }
});

// A field nobody filled in still matters on a record — "no customer owner" is information — so an empty
// value shows as a dash rather than vanishing, unless the row asks to hide.
const hasValue = (v) => !(v === null || v === undefined || v === "" || (Array.isArray(v) && !v.length));

const visibleRows = computed(() =>
  props.rows.filter((r) => !(r.hideWhenEmpty && !hasValue(r.value))));

const display = (v) => {
  if (!hasValue(v)) return "—";
  if (Array.isArray(v)) return v.join(", ");
  if (typeof v === "boolean") return v ? "Yes" : "No";
  return v;
};
</script>

<style scoped>
/* auto-fit rather than a fixed column count: four across on a desktop, folding to two and one as the
   pane narrows, with no breakpoint to keep in step per page. */
.dg {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 16px 28px;
}
.dg__item--wide {
  grid-column: 1 / -1;
}
.dg__value--rich :deep(p) {
  margin: 0 0 6px;
}
.dg__value--rich :deep(p:last-child) {
  margin-bottom: 0;
}
.dg__value--rich :deep(img) {
  max-width: 100%;
  height: auto;
}
</style>
