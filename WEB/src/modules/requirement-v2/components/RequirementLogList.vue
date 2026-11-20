<template>
  <div :class="['rlog', { 'rlog--boxed': boxed }]">
    <app-empty-state v-if="!entries.length" :icon="emptyIcon" :title="emptyTitle" :text="emptyText" class="rlog__empty" />

    <div v-for="entry in entries" :key="entry.id" class="rlog__entry">
      <div class="rlog__rail">
        <span class="rlog__dot" />
      </div>
      <div class="rlog__card">
        <div v-if="showMeta && (entry.who || entry.when || entry.badge)" class="rlog__meta">
          <span v-if="entry.who" class="rlog__who">{{ entry.who }}</span>
          <span v-if="entry.when" class="rlog__when">{{ entry.when }}</span>
          <q-badge v-if="entry.badge" color="blue-1" text-color="primary" class="rlog__badge">{{ entry.badge }}</q-badge>
        </div>
        <!-- Log entries are stored as the editor's own markup. -->
        <!-- eslint-disable-next-line vue/no-v-html -->
        <div class="rlog__body RichTextEditor" v-html="entry.html || '—'" />
      </div>
    </div>
  </div>
</template>

<script setup>
// A read-only log: one card per entry on a thin rail, newest or oldest first as the caller orders them.
import AppEmptyState from "components/common/AppEmptyState.vue";

defineProps({
  // [{ id, html, who?, when?, badge? }]
  entries: { type: Array, default: () => [] },
  // Hide the author / date line (the "overview" reading of the same log).
  showMeta: { type: Boolean, default: true },
  // Draw the list inside a bordered box (embedded panels, e.g. the Requirement Center).
  boxed: { type: Boolean, default: false },
  emptyIcon: { type: String, default: "o_history_edu" },
  emptyTitle: { type: String, default: "Nothing logged yet" },
  emptyText: { type: String, default: "" }
});
</script>

<style scoped>
.rlog {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.rlog--boxed {
  border: 1px solid var(--line, #dde6e8);
  border-radius: 10px;
  padding: 12px;
  min-height: 120px;
  max-height: 100%;
  overflow: auto;
}
.rlog__empty {
  padding: 24px 12px;
}
.rlog__entry {
  display: flex;
  align-items: stretch;
  gap: 10px;
}
.rlog__rail {
  position: relative;
  width: 14px;
  flex: 0 0 14px;
}
.rlog__rail::before {
  content: "";
  position: absolute;
  top: 14px;
  bottom: -14px;
  left: 6px;
  width: 2px;
  background: var(--line, #dde6e8);
}
.rlog__entry:last-child .rlog__rail::before {
  display: none;
}
.rlog__dot {
  position: absolute;
  top: 10px;
  left: 2px;
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: #fff;
  border: 2px solid var(--q-primary);
}
.rlog__card {
  flex: 1 1 auto;
  min-width: 0;
  border: 1px solid var(--line, #dde6e8);
  border-radius: 10px;
  background: #fff;
  padding: 8px 12px;
}
.rlog__meta {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 4px 10px;
  font-size: 11.5px;
  color: var(--ink-500, #647a82);
  margin-bottom: 4px;
}
.rlog__who {
  font-weight: 600;
  color: var(--ink-700, #3a4d54);
}
.rlog__badge {
  font-size: 10.5px;
}
.rlog__body {
  font-size: 13.5px;
  color: var(--ink-900, #16262c);
  word-break: break-word;
}
.rlog__body :deep(p) {
  margin: 0 0 6px;
}
.rlog__body :deep(p:last-child) {
  margin-bottom: 0;
}
.rlog__body :deep(img) {
  max-width: 100%;
  height: auto;
}
</style>
