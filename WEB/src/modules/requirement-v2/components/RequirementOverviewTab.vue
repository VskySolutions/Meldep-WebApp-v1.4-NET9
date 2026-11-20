<template>
  <div class="row q-col-gutter-md">
    <div class="col-12 col-md-8">
      <app-section-card title="About" icon="o_assignment" class="q-mb-md">
        <template #actions>
          <q-btn v-if="editable" flat dense no-caps color="primary" icon="o_edit" label="Edit" @click="$emit('edit')" />
        </template>
        <app-detail-grid :rows="facts">
          <template #approval>
            <app-option-badge v-if="approvalBadge" :option="approvalBadge" />
            <span v-else>—</span>
          </template>
          <template #tasks>
            <template v-if="model?.projectTaskRelatedMappings?.length">
              <q-chip
                v-for="task in model.projectTaskRelatedMappings"
                :key="task.id"
                dense
                clickable
                square
                size="sm"
                color="blue-1"
                text-color="primary"
                class="q-ml-none q-mr-xs"
                @click="$emit('open-task', task.id)"
              >
                #{{ task.projectTaskNumber }}
                <q-tooltip v-if="task.status">{{ task.status.dropDownValue }}</q-tooltip>
              </q-chip>
            </template>
            <span v-else class="text-grey-6">Not converted yet</span>
          </template>
        </app-detail-grid>
      </app-section-card>

      <app-section-card title="Initial scope" icon="o_center_focus_weak" info="The specific areas, processes, systems or activities the requirement covers." class="q-mb-md">
        <!-- eslint-disable-next-line vue/no-v-html -->
        <div v-if="hasScope" class="record-value RichTextEditor rot__rich" v-html="model.scope" />
        <div v-else class="rot__empty">
          No initial scope recorded.
          <q-btn v-if="editable" flat dense no-caps color="primary" label="Add scope" @click="$emit('edit')" />
        </div>
      </app-section-card>

      <app-section-card title="Latest Description Log entry" icon="o_history_edu" :count="logCount || null" class="q-mb-md">
        <template #actions>
          <q-btn flat dense no-caps color="primary" icon="o_open_in_new" label="Open Description Log" @click="$emit('go', 'log')" />
        </template>
        <div v-if="latestLog" class="rot__entry">
          <div class="rot__meta">
            <app-avatar :name="latestLog.who" :size="22" show-name />
            <span class="rot__when">{{ latestLog.when }}</span>
            <q-badge v-if="latestLog.original" color="blue-1" text-color="primary">Original description</q-badge>
          </div>
          <!-- eslint-disable-next-line vue/no-v-html -->
          <div class="record-value RichTextEditor rot__rich rot__clamp" v-html="latestLog.html" />
        </div>
        <div v-else class="rot__empty">
          Nothing logged yet.
          <q-btn v-if="editable" flat dense no-caps color="primary" label="Write the first entry" @click="$emit('go', 'log')" />
        </div>
      </app-section-card>

      <app-section-card title="Latest note" icon="o_sticky_note_2" :count="model?.requirementNotesCount || null" class="q-mb-md">
        <template #actions>
          <q-btn flat dense no-caps color="primary" icon="o_open_in_new" label="Open notes" @click="$emit('go', 'notes')" />
        </template>
        <!-- eslint-disable-next-line vue/no-v-html -->
        <div v-if="hasLastNote" class="record-value RichTextEditor rot__rich rot__clamp" v-html="model.lastNote" />
        <div v-else class="rot__empty">
          No notes yet.
          <q-btn v-if="canNotes" flat dense no-caps color="primary" label="Add a note" @click="$emit('go', 'notes')" />
        </div>
      </app-section-card>
    </div>

    <div class="col-12 col-md-4">
      <requirement-people-card :model="model" class="q-mb-md" />
      <requirement-dates-card :model="model" :editable="editable" :busy="busy" class="q-mb-md" @date="$emit('date', $event)" @date-log="$emit('date-log', $event)" />
      <app-record-audit :audit="audit" />
    </div>
  </div>
</template>

<script setup>
// The first tab of a requirement: what it is and where it sits, its scope, the latest thinking on it,
// and down the side who is involved and when it runs. Every field of the requirement is here or in the
// page header (title, number, short description, status, priority); there is no separate "All fields" tab.
import { computed } from "vue";
import AppSectionCard from "components/common/AppSectionCard.vue";
import AppDetailGrid from "components/common/AppDetailGrid.vue";
import AppOptionBadge from "components/common/AppOptionBadge.vue";
import AppRecordAudit from "components/common/AppRecordAudit.vue";
import AppAvatar from "components/common/AppAvatar.vue";
import RequirementPeopleCard from "modules/requirement-v2/components/RequirementPeopleCard.vue";
import RequirementDatesCard from "modules/requirement-v2/components/RequirementDatesCard.vue";
import { dropDownBadge, personName, displayTimestamp } from "modules/requirement-v2/utils/options";
import { hasRichTextContent } from "src/utils/richText";

const props = defineProps({
  model: { type: Object, default: null },
  editable: { type: Boolean, default: false },
  canNotes: { type: Boolean, default: false },
  // The date field being saved ("plannedStartDate", …), to show it busy.
  busy: { type: String, default: "" }
});

defineEmits(["edit", "go", "open-task", "date", "date-log"]);

const approvalBadge = computed(() => dropDownBadge(props.model?.approvalStatusDropDown));
const hasScope = computed(() => hasRichTextContent(props.model?.scope));
const hasLastNote = computed(() => hasRichTextContent(props.model?.lastNote));

const facts = computed(() => {
  const m = props.model || {};
  const identifier = m.userType?.dropDownValue;
  const identified = identifier === "Customer" ? personName(m.customer) : personName(m.employee);
  return [
    { label: "Project", value: m.project?.name },
    { label: "Project module", value: m.projectModule?.name },
    { label: "Requirement group", value: m.requirementGroup?.name, hideWhenEmpty: true },
    { label: "Type", value: m.requirementType?.dropDownValue },
    { label: "Area", value: m.area?.dropDownValue },
    { label: "Workspace", value: m.workspace?.dropDownValue },
    { label: "Approval", slot: "approval" },
    { label: "Identified by", value: identified ? `${identified}${identifier ? ` (${identifier.toLowerCase()})` : ""}` : "" },
    { label: "Identified on", value: m.identifiedDate },
    // The names a Data Integration requirement gives the two sides of its data mapping.
    ...(m.requirementType?.dropDownValue === "Data Integration"
      ? [{ label: "Source label", value: m.sourceLabel }, { label: "Target label", value: m.targetLabel }]
      : []),
    { label: "Tasks", slot: "tasks", wide: true },
    { label: "Notes (internal field)", value: m.notes, hideWhenEmpty: true, wide: true }
  ];
});

// The newest of the original description and the change-log entries.
const logEntries = computed(() => {
  const m = props.model;
  if (!m) return [];
  const out = [];
  if (hasRichTextContent(m.description)) {
    out.push({ html: m.description, who: personName(m.createdBy), when: m.createdOnUtc, original: true, ts: displayTimestamp(m.createdOnUtc) });
  }
  for (const log of m.requirementChangeLog || []) {
    if (hasRichTextContent(log.description)) {
      out.push({ html: log.description, who: personName(log.employee) || personName(log.createdBy), when: log.createdOnUtc, original: false, ts: displayTimestamp(log.createdOnUtc) });
    }
  }
  return out.sort((a, b) => b.ts - a.ts);
});
const latestLog = computed(() => logEntries.value[0] || null);
const logCount = computed(() => logEntries.value.length);

const audit = computed(() => {
  const m = props.model;
  if (!m) return null;
  return {
    createdBy: personName(m.createdBy),
    createdOnUtc: m.createdOnUtc,
    updatedBy: personName(m.updatedBy),
    updatedOnUtc: m.updatedOnUtc
  };
});
</script>

<style scoped>
.rot__rich :deep(p) {
  margin: 0 0 6px;
}
.rot__rich :deep(p:last-child) {
  margin-bottom: 0;
}
.rot__rich :deep(img) {
  max-width: 100%;
  height: auto;
}
.rot__clamp {
  max-height: 180px;
  overflow: hidden;
  position: relative;
}
.rot__clamp::after {
  content: "";
  position: absolute;
  left: 0;
  right: 0;
  bottom: 0;
  height: 36px;
  background: linear-gradient(to bottom, rgba(255, 255, 255, 0), #fff);
  pointer-events: none;
}
.rot__empty {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  color: var(--ink-500, #647a82);
  font-size: 13.5px;
}
.rot__entry {
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.rot__meta {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  font-size: 12px;
  color: var(--ink-500, #647a82);
}
.rot__when {
  font-variant-numeric: tabular-nums;
}
</style>
