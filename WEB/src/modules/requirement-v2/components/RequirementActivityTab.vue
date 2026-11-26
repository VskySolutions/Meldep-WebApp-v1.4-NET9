<template>
  <div class="ract q-pa-md">
    <div class="ract__bar">
      <div class="text-caption text-grey-7">{{ feed.length }} event{{ feed.length === 1 ? "" : "s" }}</div>
      <q-space />
      <q-btn-toggle
        v-model="kind"
        no-caps
        unelevated
        dense
        toggle-color="primary"
        color="white"
        text-color="grey-8"
        class="ract__kinds"
        :options="[
          { label: 'Everything', value: 'all' },
          { label: 'Status & priority', value: 'changes' },
          { label: 'Dates', value: 'dates' },
          { label: 'Requirement Log', value: 'log' },
          { label: 'Notes', value: 'note' }
        ]"
      />
      <q-btn flat round dense color="grey-8" icon="o_refresh" :loading="loading" aria-label="Refresh" @click="load">
        <q-tooltip>Refresh</q-tooltip>
      </q-btn>
    </div>

    <div v-if="loading && !feed.length" class="row flex-center q-pa-xl">
      <q-spinner color="primary" size="32px" />
    </div>

    <app-empty-state v-else-if="!feed.length" icon="o_history" title="No activity yet" text="Status, priority and date changes, Requirement Log entries and notes will show here as they happen." />

    <div v-else class="ract__days">
      <section v-for="day in days" :key="day.key" class="ract__day">
        <div class="ract__day-head"><span>{{ day.label }}</span></div>
        <div v-for="event in day.events" :key="event.key" :class="['ract__event', `ract__event--${event.kind}`]">
          <div class="ract__dot"><q-icon :name="event.icon" size="15px" /></div>
          <div class="ract__body">
            <div class="ract__line">
              <span class="ract__what">{{ event.what }}</span>
              <app-option-badge v-if="event.badge" :option="event.badge" class="ract__badge" />
              <span v-if="event.who" class="ract__who">by {{ event.who }}</span>
              <span class="ract__when">{{ event.when }}</span>
            </div>
            <!-- eslint-disable-next-line vue/no-v-html -->
            <div v-if="event.html" class="ract__excerpt RichTextEditor" v-html="event.html" />
          </div>
        </div>
      </section>
    </div>
  </div>
</template>

<script setup>
// One trail of everything that happened to the requirement: created, each status and priority change,
// each log entry, each note — newest first, grouped by day.
import { ref, computed, watch } from "vue";
import AppEmptyState from "components/common/AppEmptyState.vue";
import AppOptionBadge from "components/common/AppOptionBadge.vue";
import commonService from "services/common.service";
import requirementService from "modules/requirement-v2/requirementV2.service";
import { personName, displayTimestamp, displayDay } from "modules/requirement-v2/utils/options";
import { hasRichTextContent, excerpt } from "src/utils/richText";

const props = defineProps({
  id: { type: String, required: true },
  model: { type: Object, default: null },
  // Status / priority option lists, to colour the change badges.
  statusOptions: { type: Array, default: () => [] },
  priorityOptions: { type: Array, default: () => [] }
});

const loading = ref(false);
const changes = ref([]);
const notes = ref([]);
const logs = ref([]);
const kind = ref("all");

const badgeFor = (list, label) => {
  const hit = list.find((o) => o.label === label);
  return hit || { label };
};

const load = async () => {
  if (!props.id) return;
  loading.value = true;
  const [history, noteList, logList] = await Promise.allSettled([
    requirementService.getRequirementChangeHistory(props.id),
    commonService.getAllNoteByTypeAndRecord(props.id, "Requirement", true),
    requirementService.getAllRequirementDescriptionsById(props.id, true)
  ]);
  changes.value = history.status === "fulfilled" ? (history.value || []) : [];
  notes.value = noteList.status === "fulfilled" ? (noteList.value || []) : [];
  logs.value = logList.status === "fulfilled" ? (logList.value?.requirementList || []) : [];
  for (const r of [history, noteList, logList]) if (r.status === "rejected") console.error(r.reason);
  loading.value = false;
};

const feed = computed(() => {
  const out = [];
  const m = props.model;

  if (m?.createdOnUtc) {
    out.push({ key: "created", kind: "created", icon: "o_add_circle", what: "Requirement created", who: personName(m.createdBy), when: m.createdOnUtc, ts: displayTimestamp(m.createdOnUtc) });
  }

  for (const change of changes.value) {
    const isStatus = change.columnName === "Requirement Status";
    const isPriority = change.columnName === "Requirement Priority";
    const common = {
      key: `change-${change.id}`,
      who: personName(change.user) || change.lastModifiedBy,
      when: change.lastModifiedOnUtc,
      ts: displayTimestamp(change.lastModifiedOnUtc),
      // The note given for the change, when one was.
      html: hasRichTextContent(change.note) ? excerpt(change.note, 240) : ""
    };
    if (isStatus || isPriority) {
      out.push({
        ...common,
        kind: "changes",
        icon: isStatus ? "o_flag" : "o_priority_high",
        what: isStatus ? "Status set to" : "Priority set to",
        badge: badgeFor(isStatus ? props.statusOptions : props.priorityOptions, change.columnValue)
      });
    } else {
      // The date change log: planned / actual start and end.
      out.push({
        ...common,
        kind: "dates",
        icon: "o_event",
        what: `${change.columnName} set to ${change.columnValue || "—"}`
      });
    }
  }

  for (const requirement of logs.value) {
    for (const log of requirement.requirementChangeLog || []) {
      if (!hasRichTextContent(log.description)) continue;
      out.push({ key: `log-${log.id}`, kind: "log", icon: "o_history_edu", what: "Requirement Log entry added", who: personName(log.createdBy), when: log.createdOnUtc, ts: displayTimestamp(log.createdOnUtc), html: excerpt(log.description, 240) });
    }
  }

  for (const note of notes.value) {
    out.push({
      key: `note-${note.id}`,
      kind: "note",
      icon: "o_sticky_note_2",
      what: note.noteType?.dropDownValue ? `${note.noteType.dropDownValue} note added` : "Note added",
      who: personName(note.user),
      when: note.createdOnUtc,
      ts: displayTimestamp(note.createdOnUtc),
      html: excerpt(note.note, 240)
    });
  }

  const filtered = kind.value === "all" ? out : out.filter((e) => e.kind === kind.value || (kind.value === "changes" && e.kind === "created"));
  return filtered.sort((a, b) => b.ts - a.ts);
});

const days = computed(() => {
  const groups = new Map();
  for (const event of feed.value) {
    const key = displayDay(event.when) || "Undated";
    if (!groups.has(key)) groups.set(key, { key, label: key, events: [] });
    groups.get(key).events.push(event);
  }
  return [...groups.values()];
});

watch(() => props.id, () => load(), { immediate: true });

defineExpose({ reload: load });
</script>

<style scoped>
.ract__bar {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
}
.ract__kinds {
  border: 1px solid var(--line, #dde6e8);
  border-radius: 8px;
}
.ract__days {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.ract__day-head {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 8px;
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: var(--ink-500, #647a82);
}
.ract__day-head::after {
  content: "";
  flex: 1 1 auto;
  height: 1px;
  background: var(--line, #dde6e8);
}
.ract__event {
  display: flex;
  gap: 10px;
  padding: 6px 0 6px 2px;
  position: relative;
}
.ract__event::before {
  content: "";
  position: absolute;
  left: 13px;
  top: 30px;
  bottom: -6px;
  width: 2px;
  background: var(--line, #dde6e8);
}
.ract__event:last-child::before {
  display: none;
}
.ract__dot {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  flex: 0 0 24px;
  border-radius: 50%;
  background: #fff;
  border: 2px solid var(--q-primary);
  color: var(--q-primary);
  z-index: 1;
}
.ract__event--note .ract__dot {
  border-color: #b9862f;
  color: #b9862f;
}
.ract__event--log .ract__dot {
  border-color: #26a69a;
  color: #26a69a;
}
.ract__event--created .ract__dot {
  border-color: #21ba45;
  color: #21ba45;
}
.ract__body {
  min-width: 0;
  flex: 1 1 auto;
}
.ract__line {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 4px 8px;
  font-size: 13px;
}
.ract__what {
  font-weight: 500;
  color: var(--ink-900, #16262c);
}
.ract__badge {
  font-size: 11px;
}
.ract__who {
  color: var(--ink-500, #647a82);
}
.ract__when {
  margin-left: auto;
  font-size: 11.5px;
  color: var(--ink-500, #647a82);
  font-variant-numeric: tabular-nums;
}
.ract__excerpt {
  margin-top: 4px;
  padding: 6px 10px;
  border-left: 3px solid var(--line, #dde6e8);
  font-size: 12.5px;
  color: var(--ink-700, #3a4d54);
}
</style>
