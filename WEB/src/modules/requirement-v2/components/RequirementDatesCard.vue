<template>
  <app-section-card title="Timeline" icon="o_event">
    <!-- Planned window with where today falls in it. -->
    <div v-if="progress !== null" class="rdt__window">
      <div class="rdt__bar">
        <div :class="['rdt__fill', `rdt__fill--${due.state}`]" :style="{ width: `${Math.round(progress * 100)}%` }" />
      </div>
      <div class="rdt__legend">
        <span>{{ model.plannedStartDate }}</span>
        <span :class="['rdt__due', `rdt__due--${due.state}`]">{{ due.label || "Planned window" }}</span>
        <span>{{ model.plannedEndDate }}</span>
      </div>
    </div>
    <div v-else-if="due.state === 'overdue' || due.state === 'due-soon'" :class="['rdt__flag', `rdt__flag--${due.state}`]">
      <q-icon :name="due.state === 'overdue' ? 'o_error' : 'o_schedule'" size="16px" />
      {{ due.label }}
    </div>

    <div class="rdt__grid">
      <div v-for="item in dates" :key="item.label" :class="['rdt__item', { 'rdt__item--empty': !item.value }]">
        <q-icon :name="item.icon" size="16px" class="rdt__icon" />
        <div class="rdt__text">
          <div class="record-label">{{ item.label }}</div>
          <div class="record-value">{{ item.value || "—" }}</div>
        </div>
        <!-- The four planned / actual dates are changed here, each with a note, and keep a change log. -->
        <div v-if="item.field" class="rdt__tools">
          <q-btn v-if="editable" flat round dense size="sm" color="primary" icon="o_calendar_month" :loading="busy === item.field" :aria-label="`Change ${item.log}`">
            <q-popup-proxy :ref="(el) => setPopup(item.field, el)" transition-show="scale" transition-hide="scale">
              <q-date :model-value="item.value || null" mask="MM/DD/YYYY" @update:model-value="(value) => pick(item, value)" />
            </q-popup-proxy>
            <q-tooltip>Change {{ item.log }}</q-tooltip>
          </q-btn>
          <q-btn v-if="item.value" flat round dense size="sm" color="grey-7" icon="o_history" :aria-label="`${item.log} Change Log`" @click="$emit('date-log', item.log)">
            <q-tooltip>{{ item.log }} Change Log</q-tooltip>
          </q-btn>
        </div>
      </div>
    </div>
  </app-section-card>
</template>

<script setup>
// When the requirement was raised, when it is planned for, when it actually ran, when it closed — with
// the planned window drawn as a bar so an overdue requirement is seen before it is read.
import { computed } from "vue";
import AppSectionCard from "components/common/AppSectionCard.vue";
import { dueInfo, plannedProgress } from "modules/requirement-v2/utils/options";

const props = defineProps({
  model: { type: Object, default: () => ({}) },
  editable: { type: Boolean, default: false },
  // The date field being saved, to show its button busy.
  busy: { type: String, default: "" }
});

// "date": { field, label, value: "MM/DD/YYYY" } once a day is picked (the host asks for the note and saves).
// "date-log": the log's column name ("Planned Start Date", …), to open that date's change log.
const emit = defineEmits(["date", "date-log"]);

const popups = {};
const setPopup = (field, el) => { popups[field] = el; };

const pick = (item, value) => {
  popups[item.field]?.hide?.();
  // q-date sends null when the selected day is clicked again; an unchanged day is not a change.
  if (!value || value === item.value) return;
  emit("date", { field: item.field, label: item.log, value });
};

const due = computed(() => dueInfo(props.model?.plannedEndDate, props.model?.status?.dropDownValue));
const progress = computed(() => plannedProgress(props.model?.plannedStartDate, props.model?.plannedEndDate));

const dates = computed(() => {
  const m = props.model || {};
  const list = [
    { label: "Identified", value: m.identifiedDate, icon: "o_lightbulb" },
    // `log` is the name the date change log is kept under.
    { label: "Planned start", value: m.plannedStartDate, icon: "o_flag", field: "plannedStartDate", log: "Planned Start Date" },
    { label: "Planned end", value: m.plannedEndDate, icon: "o_sports_score", field: "plannedEndDate", log: "Planned End Date" },
    { label: "Actual start", value: m.actualStartDate, icon: "o_play_arrow", field: "actualStartDate", log: "Actual Start Date" },
    { label: "Actual end", value: m.actualEndDate, icon: "o_stop", field: "actualEndDate", log: "Actual End Date" }
  ];
  if (m.closeDate) list.push({ label: "Closed", value: m.closeDate, icon: "o_check_circle" });
  return list;
});
</script>

<style scoped>
.rdt__window {
  margin-bottom: 14px;
}
.rdt__bar {
  height: 8px;
  border-radius: 999px;
  background: #e6edf0;
  overflow: hidden;
}
.rdt__fill {
  height: 100%;
  border-radius: 999px;
  background: var(--q-primary);
  transition: width 0.3s ease;
}
.rdt__fill--overdue {
  background: #c10015;
}
.rdt__fill--due-soon {
  background: #b9862f;
}
.rdt__fill--done {
  background: #21ba45;
}
.rdt__legend {
  display: flex;
  justify-content: space-between;
  gap: 8px;
  margin-top: 4px;
  font-size: 11px;
  color: var(--ink-500, #647a82);
  font-variant-numeric: tabular-nums;
}
.rdt__due {
  font-weight: 600;
}
.rdt__due--overdue {
  color: #c10015;
}
.rdt__due--due-soon {
  color: #8a5300;
}
.rdt__flag {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 12px;
  padding: 4px 10px;
  border-radius: 999px;
  font-size: 12px;
  font-weight: 600;
}
.rdt__flag--overdue {
  background: #fdecea;
  color: #8a1c12;
}
.rdt__flag--due-soon {
  background: #fff4e5;
  color: #8a5300;
}
.rdt__grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: 12px 16px;
}
.rdt__item {
  display: flex;
  align-items: flex-start;
  gap: 8px;
}
.rdt__text {
  flex: 1 1 auto;
  min-width: 0;
}
.rdt__tools {
  display: flex;
  flex: 0 0 auto;
  align-items: center;
}
.rdt__icon {
  margin-top: 2px;
  color: var(--q-primary);
}
.rdt__item--empty .rdt__icon {
  color: var(--ink-300, #a6b8bd);
}
.rdt__item--empty .record-value {
  color: var(--ink-300, #a6b8bd);
}
</style>
