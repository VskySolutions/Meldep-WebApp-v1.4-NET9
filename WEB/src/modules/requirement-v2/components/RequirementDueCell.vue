<template>
  <span v-if="!date" class="text-grey-6">—</span>
  <span v-else :class="['rdc', `rdc--${info.state}`]">
    <q-icon v-if="info.state === 'overdue'" name="o_error" size="15px" />
    <q-icon v-else-if="info.state === 'due-soon'" name="o_schedule" size="15px" />
    <span>{{ date }}</span>
    <q-tooltip v-if="info.label" :delay="300">{{ info.label }}</q-tooltip>
  </span>
</template>

<script setup>
// A planned end date with its standing: red when overdue, amber when due within the week, quiet
// otherwise. The date is shown as the API renders it.
import { computed } from "vue";
import { dueInfo } from "modules/requirement-v2/utils/options";

const props = defineProps({
  date: { type: String, default: "" },
  statusLabel: { type: String, default: "" }
});

const info = computed(() => dueInfo(props.date, props.statusLabel));
</script>

<style scoped>
.rdc {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  white-space: nowrap;
  font-variant-numeric: tabular-nums;
}
.rdc--overdue {
  color: #c10015;
  font-weight: 600;
}
.rdc--due-soon {
  color: #8a5300;
  font-weight: 500;
}
.rdc--done {
  color: var(--ink-500, #647a82);
}
</style>
