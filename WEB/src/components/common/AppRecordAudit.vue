<template>
  <q-card v-if="rows.length" flat bordered class="app-record-audit q-mb-md">
    <q-card-section class="row items-center q-py-sm">
      <q-icon name="o_history" color="primary" size="18px" class="q-mr-xs" />
      <div class="text-subtitle2 text-primary">{{ title }}</div>
    </q-card-section>
    <q-separator />
    <q-card-section class="app-record-audit__grid">
      <div v-for="row in rows" :key="row.label">
        <div class="record-label">{{ row.label }}</div>
        <div class="record-value">{{ row.value }}</div>
      </div>
    </q-card-section>
  </q-card>
</template>

<script setup>
// The provenance block every record view ends with: who made it and when, who last touched it and when.
// Meldep's API renders timestamps server-side in the site's time zone, so values are shown as sent.
import { computed } from "vue";

const props = defineProps({
  // { createdBy, createdOnUtc, updatedBy, updatedOnUtc } — names and already-formatted dates.
  audit: { type: Object, default: null },
  title: { type: String, default: "Record information" }
});

const who = (name) => name || "—";
const when = (value) => value || "—";

const rows = computed(() => {
  const audit = props.audit;
  if (!audit) return [];
  return [
    { label: "Created By", value: who(audit.createdBy) },
    { label: "Created On", value: when(audit.createdOnUtc) },
    { label: "Updated By", value: who(audit.updatedBy) },
    { label: "Updated On", value: when(audit.updatedOnUtc) }
  ];
});
</script>

<style scoped>
.app-record-audit {
  border-radius: 12px;
  box-shadow: none;
}
.app-record-audit__grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 16px 28px;
}
</style>
