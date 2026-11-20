<template>
  <div class="rdc2">
    <requirement-due-cell v-if="due" :date="date" :status-label="statusLabel" />
    <span v-else :class="['rdc2__value', { 'text-grey-6': !date }]">{{ date || "—" }}</span>

    <span class="rdc2__tools">
      <q-btn
        v-if="editable"
        flat round dense size="xs" color="primary" icon="o_calendar_month"
        class="rdc2__btn"
        :loading="saving"
        :aria-label="`Change ${log}`"
        @click.stop
      >
        <q-popup-proxy ref="popupRef" transition-show="scale" transition-hide="scale">
          <q-date :model-value="date || null" mask="MM/DD/YYYY" :options="options" @update:model-value="pick" />
        </q-popup-proxy>
        <q-tooltip>Change {{ log }}</q-tooltip>
      </q-btn>
      <q-btn
        v-if="date && hasLog"
        flat round dense size="xs" color="grey-6" icon="o_history"
        class="rdc2__btn"
        :aria-label="`${log} Change Log`"
        @click.stop="$emit('log')"
      >
        <q-tooltip>{{ log }} Change Log</q-tooltip>
      </q-btn>
    </span>
  </div>
</template>

<script setup>
// One of the four planned / actual dates of a requirement, in the list: the date (the planned end with
// its overdue / due-soon reading), a picker for those who may change it and the date's change log. The
// page owns the write: it asks for the note and saves.
import { ref, computed } from "vue";
import RequirementDueCell from "modules/requirement-v2/components/RequirementDueCell.vue";
import { notBefore } from "modules/requirement-v2/utils/changeNote";

const props = defineProps({
  // "MM/DD/YYYY", as the API renders calendar dates.
  date: { type: String, default: "" },
  // The name the date's change log is kept under ("Planned Start Date", …).
  log: { type: String, required: true },
  editable: { type: Boolean, default: false },
  // Whether this date has a change log to open; the button is left out when it has none.
  hasLog: { type: Boolean, default: true },
  saving: { type: Boolean, default: false },
  // Show the planned end's overdue / due-soon reading (it needs the status to know what is finished).
  due: { type: Boolean, default: false },
  statusLabel: { type: String, default: "" },
  // An end date may not fall before its start date ("MM/DD/YYYY").
  minDate: { type: String, default: "" }
});

// "change": the day picked ("MM/DD/YYYY"). "log": open the date's change log.
const emit = defineEmits(["change", "log"]);

const popupRef = ref(null);
const options = computed(() => notBefore(props.minDate));

const pick = (value) => {
  popupRef.value?.hide?.();
  // q-date sends null when the selected day is clicked again; an unchanged day is not a change.
  if (!value || value === props.date) return;
  emit("change", value);
};
</script>

<style scoped>
.rdc2 {
  display: flex;
  align-items: center;
  gap: 2px;
  min-width: 0;
}
.rdc2__value {
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}
.rdc2__tools {
  display: inline-flex;
  align-items: center;
  flex: 0 0 auto;
}
/* Dimmed until the row is hovered, like the other in-cell buttons of the list. */
.rdc2__btn {
  opacity: 0.55;
}
tr:hover .rdc2__btn,
.rdc2__btn:focus-visible {
  opacity: 1;
}
</style>
