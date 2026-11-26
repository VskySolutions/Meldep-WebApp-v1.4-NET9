<template>
  <div :class="['rrec', `rrec--${mode}`]">
    <div v-if="loading && !model" class="row flex-center q-pa-xl">
      <q-spinner color="primary" size="40px" />
    </div>

    <app-empty-state v-else-if="!model" icon="o_search_off" title="Requirement not found" text="It may have been deleted, or you may not have access to its project.">
      <q-btn flat no-caps color="primary" label="Back to requirements" :to="{ name: 'requirement-v2' }" />
    </app-empty-state>

    <template v-else>
      <q-banner v-if="readOnlyReason" dense rounded class="bg-blue-grey-1 text-blue-grey-9 q-mb-md rrec__banner">
        <template #avatar><q-icon name="o_lock" color="blue-grey-9" /></template>
        {{ readOnlyReason }}
      </q-banner>

      <requirement-record-header
        :model="model"
        :editable="editable"
        :can-notes="canNotes"
        :status-options="statusOptions"
        :priority-options="priorityOptions"
        :tags="tags"
        :available-tags="tagCatalogue.list.value"
        :busy="busy"
        :crumbs="crumbs"
        :back-to="backTo"
        :page-note="pageNote"
        class="q-mb-md"
        @status="setStatus"
        @priority="setPriority"
        @pin="togglePin"
        @colour="setColour"
        @tags-before-edit="ensureTagCatalogue"
        @tags-filter="(...args) => tagCatalogue.filter(...args)"
        @tags-save="saveTags"
        @tags-remove="removeTag"
        @edit="openEdit('info')"
        @log="goTo('log')"
        @note="goTo('notes')"
        @convert="convertToTask"
        @data-mapping="editDataMapping"
        @delete="remove"
        @copy-link="copyLink"
        @open-project="onProjectView(model.project?.id)"
        @open-module="onProjectModuleView(model.projectModule?.id)"
      />

      <q-card flat bordered class="rrec__body">
        <!-- One bar: the tabs, and at its end the related-work figures, each a shortcut into the Related tab.
             Log, notes and documents are counted on their own tabs. -->
        <div class="rrec__bar">
          <q-tabs
            v-model="tab"
            dense
            no-caps
            inline-label
            align="left"
            active-color="primary"
            indicator-color="primary"
            class="text-grey-8 rrec__tabs"
            mobile-arrows
            outside-arrows
          >
            <q-tab name="overview" label="Overview" icon="o_dashboard" />
            <q-tab name="log" icon="o_history_edu">
              <span class="q-tab__label rrec__tab-label">Requirement Log<q-badge v-if="logCount" color="grey-3" text-color="grey-8" class="rrec__tab-count">{{ logCount }}</q-badge></span>
              <q-tooltip>Requirement Log: detailed information about the requirement, as a log: the original description, then every entry added since</q-tooltip>
            </q-tab>
            <q-tab name="notes" icon="o_sticky_note_2">
              <span class="q-tab__label rrec__tab-label">Notes<q-badge v-if="counts?.notes" color="grey-3" text-color="grey-8" class="rrec__tab-count">{{ counts.notes }}</q-badge></span>
              <q-tooltip>Client meeting updates, weekly status, next steps, roadblocks — and the work & progress on its tasks</q-tooltip>
            </q-tab>
            <q-tab name="related" icon="o_hub">
              <span class="q-tab__label rrec__tab-label">Related<q-badge v-if="relatedCount" color="grey-3" text-color="grey-8" class="rrec__tab-count">{{ relatedCount }}</q-badge></span>
              <q-tooltip>Tasks, issues, test cases, Q&amp;A, action items and time: a summary of each, and the working views for any one of them</q-tooltip>
            </q-tab>
            <q-tab name="mapping" icon="o_account_tree">
              <span class="q-tab__label rrec__tab-label">Data mapping<q-badge v-if="counts?.dataMappings" color="grey-3" text-color="grey-8" class="rrec__tab-count">{{ counts.dataMappings }}</q-badge></span>
            </q-tab>
            <q-tab name="documents" icon="o_link">
              <span class="q-tab__label rrec__tab-label">Documents<q-badge v-if="documents.length" color="grey-3" text-color="grey-8" class="rrec__tab-count">{{ documents.length }}</q-badge></span>
            </q-tab>
            <q-tab name="activity" label="Activity" icon="o_history" />
          </q-tabs>

          <div class="rrec__counts">
            <button v-for="item in countItems" :key="item.key" type="button" class="rrec__count" :aria-label="item.tip" @click="goTo('related')">
              <q-icon :name="item.icon" size="16px" />
              <span class="rrec__count-value">{{ countsLoading ? "…" : item.value }}</span>
              <span class="rrec__count-label">{{ item.label }}</span>
              <q-tooltip :delay="400">{{ item.tip }}</q-tooltip>
            </button>
          </div>
        </div>
        <q-separator />

        <q-tab-panels v-model="tab" animated keep-alive class="rrec__panels">
          <q-tab-panel name="overview" class="q-pa-md">
            <requirement-overview-tab :model="model" :editable="editable" :can-notes="canNotes" :busy="busy" @edit="openEdit('info')" @go="goTo" @open-task="onProjectTaskView" @date="setDate" @date-log="showDateLog" />
          </q-tab-panel>

          <q-tab-panel name="log" class="q-pa-none rrec__panel--fill">
            <requirement-log-tab :id="model.id" :model="model" :editable="editable" @changed="afterWrite" />
          </q-tab-panel>

          <q-tab-panel name="notes" class="q-pa-none rrec__panel--fill">
            <requirement-notes-tab :id="model.id" :model="model" :can-notes="canNotes" @changed="afterWrite" />
          </q-tab-panel>

          <q-tab-panel name="related" class="q-pa-none">
            <requirement-related-tab :id="model.id" :model="model" :counts="counts" :editable="editable" :initial-entity="initialEntity" @changed="afterWrite" />
          </q-tab-panel>

          <q-tab-panel name="mapping" class="q-pa-md">
            <q-banner v-if="!isDataIntegration" dense rounded class="bg-grey-2 text-grey-8 q-mb-md">
              <template #avatar><q-icon name="o_info" color="grey-8" /></template>
              Data mappings are kept for requirements of type "Data Integration". This one is {{ model.requirementType?.dropDownValue ? `"${model.requirementType.dropDownValue}"` : "untyped" }}.
            </q-banner>
            <!-- Mappings are edited where they stand (each row saves itself); a mapping added or deleted
                 changes the count on the tab's title. -->
            <requirement-data-mapping-tab :key="mappingKey" :id="model.id" :model="model" :editable="editable && isDataIntegration" @changed="loadCounts" />
          </q-tab-panel>

          <q-tab-panel name="documents" class="q-pa-none">
            <requirement-documents-tab :documents="documents" :editable="editable" @manage="openEdit('documents')" />
          </q-tab-panel>

          <q-tab-panel name="activity" class="q-pa-none">
            <requirement-activity-tab ref="activityRef" :id="model.id" :model="model" :status-options="statusOptions" :priority-options="priorityOptions" />
          </q-tab-panel>
        </q-tab-panels>
      </q-card>
    </template>
  </div>
</template>

<script setup>
// A requirement, whole: its header, its tabs and the writes the reader may make on it. Hosted by the
// requirement's page (pages/detail.vue). The Related tab is the Requirement Center: the summary of every
// kind of linked work and the working views for each.
import { ref, computed, watch } from "vue";
import { useQuasar } from "quasar";
import { useRouter } from "vue-router";
import { useAuthStore } from "stores/auth";
import { useNotify } from "composables/useNotify";
import AppEmptyState from "components/common/AppEmptyState.vue";
import RequirementRecordHeader from "modules/requirement-v2/components/RequirementRecordHeader.vue";
import RequirementOverviewTab from "modules/requirement-v2/components/RequirementOverviewTab.vue";
import RequirementLogTab from "modules/requirement-v2/components/RequirementLogTab.vue";
import RequirementNotesTab from "modules/requirement-v2/components/RequirementNotesTab.vue";
import RequirementRelatedTab from "modules/requirement-v2/components/RequirementRelatedTab.vue";
import RequirementDocumentsTab from "modules/requirement-v2/components/RequirementDocumentsTab.vue";
import RequirementActivityTab from "modules/requirement-v2/components/RequirementActivityTab.vue";
import RequirementDataMappingTab from "modules/requirement-v2/components/RequirementDataMappingTab.vue";
import requirementService from "modules/requirement-v2/requirementV2.service";
import requirementModule from "modules/requirement/utils/dropdowns";
import tagModule from "src/modules/tags/utils/dropdowns.js";
import { toBadgeOptions } from "modules/requirement-v2/utils/options";
import { askChangeNote } from "modules/requirement-v2/utils/changeNote";
import { hasRichTextContent } from "src/utils/richText";
import { initProjectDialogs, onProjectView } from "src/modules/project/utils/dialogs.js";
import { initProjectModuleDialogs, onProjectModuleView } from "src/modules/project-modules/utils/dialogs.js";
import { initProjectTaskDialogs, onProjectTaskView } from "src/modules/project-tasks/utils/dialogs.js";
import { initRequirementDataMappingDialogs, onEditRequirementDataMapping } from "src/modules/requirement-data-mapping/utils/dialogs.js";
import { initRequirementDialogs } from "src/modules/requirement/utils/dialogs.js";
import { initSiteDialogs, onSiteModifiedLog } from "src/modules/sites/utils/dialogs.js";
import {
  initRequirementActions,
  onSubmitRequirementStatus,
  onSubmitRequirementPriority,
  onSubmitRequirementPinned,
  onSubmitRequirementColor,
  onSubmitRequirementTags,
  onSubmitRequirementDelete,
  onSubmitRequirementDate,
  copyRequirementLink
} from "src/modules/requirement-v2/utils/actions.js";

const props = defineProps({
  id: { type: String, required: true },
  // Kept for the page's class hook; the record only ever renders on its own page now.
  mode: { type: String, default: "page" },
  initialTab: { type: String, default: "overview" },
  // Which kind of linked work the Related tab opens on ("all", "tasks", "issues", …).
  initialEntity: { type: String, default: "all" },
  // The page hands its crumb trail, Back target and note to the record header (one header, not two).
  crumbs: { type: Array, default: () => [] },
  backTo: { type: [String, Object], default: null },
  pageNote: { type: String, default: "" }
});

const emit = defineEmits(["changed", "deleted", "update:tab"]);

const $q = useQuasar();
const router = useRouter();
const notify = useNotify();
const user = useAuthStore().user;
const isViewer = user?.roles?.some((r) => r?.toLowerCase() === "viewer") ?? false;

const activeRowId = ref(null);
initRequirementDialogs(activeRowId);
initRequirementActions(activeRowId);
initProjectDialogs(activeRowId);
initProjectModuleDialogs(activeRowId);
initProjectTaskDialogs(activeRowId);
initRequirementDataMappingDialogs();
initSiteDialogs(activeRowId);

// ---- State ----
const model = ref(null);
const counts = ref(null);
const loading = ref(false);
const countsLoading = ref(false);
const tab = ref(props.initialTab || "overview");
const busy = ref("");
const activityRef = ref(null);
const mappingKey = ref(0);

watch(tab, (t) => emit("update:tab", t));
watch(() => props.initialTab, (t) => { if (t) tab.value = t; });

const editable = computed(() => !isViewer && model.value?.project?.currentUserManage === true);
const canNotes = computed(() => editable.value || model.value?.project?.currentUserNotes === true);
const readOnlyReason = computed(() => {
  if (!model.value) return "";
  if (isViewer) return "You have a viewer role: this requirement is read-only for you.";
  if (!editable.value) return canNotes.value ? "You can read this requirement and add notes; editing it needs full access on its project." : "This requirement is read-only for you: you do not have full access on its project.";
  return "";
});

const isDataIntegration = computed(() => model.value?.requirementType?.dropDownValue === "Data Integration");
const documents = computed(() => model.value?.filePathDetails || []);

const logCount = computed(() => {
  const m = model.value;
  if (!m) return 0;
  return (m.requirementChangeLog || []).filter((l) => hasRichTextContent(l.description)).length + (hasRichTextContent(m.description) ? 1 : 0);
});
const relatedCount = computed(() => {
  const c = counts.value;
  if (!c) return 0;
  return (c.tasks || 0) + (c.issues || 0) + (c.testCases || 0) + (c.questionsAnswers || 0) + (c.actionItems || 0);
});

// The related-work figures at the end of the tab bar. The name of each is hidden where the bar is too
// narrow for it, so the tooltip (and the accessible name) says the whole thing.
const named = (n, one, many) => (Number(n) === 1 ? one : many);
const countItems = computed(() => {
  const c = counts.value || {};
  const tasks = c.tasks ?? 0;
  const issues = c.issues ?? 0;
  const tests = c.testCases ?? 0;
  const questions = c.questionsAnswers ?? 0;
  const actions = c.actionItems ?? 0;
  const hours = Number(Number(c.timesheetHours || 0).toFixed(2));
  return [
    { key: "tasks", icon: "o_task_alt", value: tasks, label: named(tasks, "task", "tasks"), tip: `${tasks} ${named(tasks, "task", "tasks")}, ${c.openTasks ?? 0} open` },
    { key: "issues", icon: "o_bug_report", value: issues, label: named(issues, "issue", "issues"), tip: `${issues} ${named(issues, "issue", "issues")}, ${c.openIssues ?? 0} open` },
    { key: "tests", icon: "o_fact_check", value: tests, label: named(tests, "test case", "test cases"), tip: `${tests} ${named(tests, "test case", "test cases")}, ${c.passedTestCases ?? 0} passed` },
    { key: "qa", icon: "o_forum", value: questions, label: "Q&A", tip: `${questions} ${named(questions, "question", "questions")} raised on this requirement` },
    { key: "actions", icon: "o_checklist", value: actions, label: named(actions, "action item", "action items"), tip: `${actions} ${named(actions, "action item", "action items")} linked to this requirement` },
    { key: "hours", icon: "o_schedule", value: `${hours} h`, label: "logged", tip: `${hours} timesheet ${named(hours, "hour", "hours")} on its tasks` }
  ];
});

// ---- Lookups ----
const { requirementStatusDropdownSingleSelect: statuses, requirementPriorityDropdownSingleSelect: priorities } = requirementModule();
const statusOptions = computed(() => toBadgeOptions(statuses.list.value));
const priorityOptions = computed(() => toBadgeOptions(priorities.list.value));
Promise.allSettled([statuses.load("Requirement Status"), priorities.load("Requirement Priority")])
  .then((results) => results.forEach((r) => { if (r.status === "rejected") console.error(r.reason); }));

const { tagsDropdown: tagCatalogue } = tagModule();
let catalogueLoaded = false;
const ensureTagCatalogue = () => {
  if (catalogueLoaded) return;
  catalogueLoaded = true;
  tagCatalogue.load().catch((error) => { catalogueLoaded = false; console.error(error); });
};

const tags = computed(() => (model.value?.requirementTags || []).map((tag) => ({
  text: tag.tags?.name,
  value: tag.tags?.id,
  color: tag.tags?.color,
  bgColor: tag.tags?.bgColor
})));

// ---- Load ----
const loadCounts = async () => {
  countsLoading.value = true;
  try {
    counts.value = await requirementService.getRequirementRelatedCounts(props.id);
  } catch (error) {
    console.error("Error loading related counts", error);
  } finally {
    countsLoading.value = false;
  }
};

const load = async () => {
  loading.value = !model.value;
  try {
    model.value = await requirementService.getRequirementDetails(props.id);
    activeRowId.value = props.id;
  } catch (error) {
    console.error("Error loading requirement", error);
    model.value = null;
  } finally {
    loading.value = false;
  }
  loadCounts();
};

// Anything written through a tab refreshes the record, the counts and the activity trail, and tells
// the host so a list behind the panel can reload too.
const afterWrite = () => {
  load();
  activityRef.value?.reload?.();
  emit("changed");
};

watch(() => props.id, () => {
  model.value = null;
  counts.value = null;
  tab.value = props.initialTab || "overview";
  load();
}, { immediate: true });

// ---- Navigation between tabs ----
const goTo = (name) => {
  tab.value = name;
};

// ---- Writes ----
// A status change may carry a note (optional), as on the requirement list; it is kept in the status
// change log. Cancelling the prompt leaves the status as it was.
const setStatus = async (statusId) => {
  const { ok, note } = await askChangeNote("Requirement Status Note", "Add a note for this status change (optional).");
  if (!ok) return;
  busy.value = "status";
  try {
    await onSubmitRequirementStatus(props.id, statusId, afterWrite, note);
  } finally {
    busy.value = "";
  }
};

// A date change is saved with its note and written to the date change log.
const setDate = async ({ field, label, value }) => {
  const { ok, note } = await askChangeNote(`${label} Note`, `Add a note for this ${label.toLowerCase()} change (optional).`);
  if (!ok) return;
  busy.value = field;
  try {
    await onSubmitRequirementDate(props.id, value, field, note, afterWrite);
  } finally {
    busy.value = "";
  }
};

const showDateLog = (label) => onSiteModifiedLog(props.id, model.value?.title, label);

const setPriority = async (priorityId) => {
  busy.value = "priority";
  try {
    await onSubmitRequirementPriority(props.id, priorityId, afterWrite);
  } finally {
    busy.value = "";
  }
};

const togglePin = () => onSubmitRequirementPinned(props.id, !model.value?.isPinned, afterWrite);
const setColour = (colour) => onSubmitRequirementColor(props.id, colour, true, model.value?.requirementColor, afterWrite);
const saveTags = (value) => onSubmitRequirementTags(props.id, value, afterWrite, null);
const removeTag = (tag) => saveTags(tags.value.filter((t) => t.value !== tag.value));

// Editing is a page of its own (v2); the record is read afresh when the reader comes back to it.
// The "documents" section lands on the document references.
const openEdit = (section) => {
  activeRowId.value = props.id;
  router.push({ name: "requirement-v2-edit", params: { id: props.id }, query: section === "documents" ? { section: "documents" } : {} });
};

// The data-mapping editor is the current one; it takes the requirement's project and module.
const editDataMapping = () => onEditRequirementDataMapping(props.id, model.value?.projectId, model.value?.projectModuleId, true, () => {
  mappingKey.value += 1;
  afterWrite();
});

const convertToTask = () => {
  const m = model.value;
  const taskNumbers = (m.projectTaskRelatedMappings || []).map((t) => t.projectTaskNumber).filter(Boolean);
  import("modules/project-tasks/components/addEdit.vue").then((mod) => {
    $q.dialog({
      component: mod.default,
      componentProps: {
        requirementId: m.id,
        projectIdAttr: m.project?.id,
        moduleIdAttr: m.projectModule?.id,
        name: m.title,
        isRequirementConverted: true,
        taskNumbers
      }
    }).onOk(() => afterWrite());
  });
};

const remove = async () => {
  const done = await onSubmitRequirementDelete(props.id, model.value?.title, null);
  if (done) emit("deleted", props.id);
};

const copyLink = () => {
  const href = router.resolve({ name: "requirement-v2-detail", params: { id: props.id } }).href;
  copyRequirementLink(`${window.location.origin}${href}`);
};

defineExpose({ reload: load, goTo, model, counts });
</script>

<style scoped>
.rrec {
  min-height: 0;
}
.rrec__body {
  border-radius: 12px;
  box-shadow: none;
}
/* The log and notes tabs keep their composer at the foot of a fixed-height thread. */
.rrec--page .rrec__panels :deep(.q-tab-panel.rrec__panel--fill) {
  height: 70vh;
  min-height: 480px;
}
.rrec__panels {
  background: transparent;
}
/* The tab bar: the tabs keep their own width and the related-work figures take what is left of the
   line. Where the two do not fit side by side the figures drop to a line of their own under the tabs. */
.rrec__bar {
  display: flex;
  flex-wrap: wrap;
  align-items: stretch;
  background: #fff;
  border-radius: 12px 12px 0 0;
  overflow: hidden;
  container-type: inline-size;
}
.rrec__tabs {
  flex: 0 1 auto;
  min-width: 0;
  padding: 0 8px;
}
/* Icon beside the label and tighter tabs, so the bar is one line tall and leaves room for the figures. */
.rrec__tabs :deep(.q-tab) {
  padding: 0 12px;
}
.rrec__tabs :deep(.q-tab__icon) {
  width: 20px;
  height: 20px;
  font-size: 20px;
}
.rrec__tabs :deep(.q-tab__content--inline .q-tab__icon + .q-tab__label) {
  padding-left: 6px;
}
.rrec__tab-label {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
.rrec__tab-count {
  font-size: 10.5px;
  font-weight: 600;
  padding: 1px 6px;
}
.rrec__counts {
  flex: 1 1 auto;
  min-width: 0;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: flex-end;
  padding: 0 8px;
  /* A rule over the figures that shows only when they sit under the tabs: on the tabs' own line it
     falls above the bar's top edge and is clipped. */
  margin-top: -1px;
  border-top: 1px solid var(--line, #dde6e8);
}
.rrec__count {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  height: 30px;
  padding: 0 8px;
  border: none;
  border-radius: 6px;
  background: transparent;
  color: var(--ink-700, #3a4d54);
  font: inherit;
  font-size: 12px;
  white-space: nowrap;
  cursor: pointer;
}
.rrec__count:hover {
  background: var(--brand-050, #f3f8fb);
  color: var(--q-primary);
}
.rrec__count:focus-visible {
  outline: 2px solid var(--q-primary);
  outline-offset: -2px;
}
.rrec__count-value {
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}
/* Each figure is named only where the bar is wide enough for the names; its tooltip always says it.
   The widths below are the bar's own (measured with three-digit counts on every tab that has one): tabs
   and named figures need about 1770px side by side, tabs and bare figures about 1435px, and the
   tightened ones about 1295px. */
.rrec__count-label {
  display: none;
}
@container (min-width: 1780px) {
  .rrec__count {
    gap: 5px;
    padding: 0 10px;
  }
  .rrec__count-label {
    display: inline;
  }
}
@container (max-width: 1439px) {
  .rrec__tabs :deep(.q-tab) {
    padding: 0 8px;
  }
  .rrec__tabs :deep(.q-tab__icon) {
    width: 18px;
    height: 18px;
    font-size: 18px;
  }
  .rrec__tabs :deep(.q-tab__content--inline .q-tab__icon + .q-tab__label) {
    padding-left: 4px;
  }
  .rrec__tab-label {
    gap: 4px;
  }
  .rrec__count {
    padding: 0 5px;
  }
}
.rrec__banner {
  border-radius: 10px;
}
</style>
