<template>
  <div class="rrt q-pa-md">
    <!-- The figures, and the way in: Summary shows every kind; a tile opens that kind below; the open tile, clicked
         again, returns to the summary of every kind. -->
    <div class="rrt__head">
      <div class="rrt__kpis">
        <app-stat-tile label="Summary" :value="linkedTotal" caption="linked items" icon="o_grid_view" color="#37474f" dense clickable :active="entity === 'all'" :loading="!counts" hint="Every kind of linked work at a glance" @click="pick('all')" />
        <app-stat-tile label="Tasks" :value="counts?.tasks ?? 0" :caption="`${counts?.openTasks ?? 0} open`" icon="o_task_alt" color="#1b75ab" dense clickable :active="entity === 'tasks'" :loading="!counts" :hint="tileHint('tasks', 'tasks')" @click="toggle('tasks')" />
        <app-stat-tile label="Issues" :value="counts?.issues ?? 0" :caption="`${counts?.openIssues ?? 0} open`" icon="o_bug_report" color="#c2185b" dense clickable :active="entity === 'issues'" :loading="!counts" :hint="tileHint('issues', 'issues')" @click="toggle('issues')" />
        <app-stat-tile label="Test cases" :value="counts?.testCases ?? 0" :caption="`${counts?.passedTestCases ?? 0} passed`" icon="o_fact_check" color="#00897b" dense clickable :active="entity === 'testCases'" :loading="!counts" :hint="tileHint('testCases', 'test cases')" @click="toggle('testCases')" />
        <app-stat-tile label="Q&A" :value="counts?.questionsAnswers ?? 0" icon="o_forum" color="#7e57c2" dense clickable :active="entity === 'questions'" :loading="!counts" :hint="tileHint('questions', 'questions and answers')" @click="toggle('questions')" />
        <app-stat-tile label="Action items" :value="counts?.actionItems ?? 0" icon="o_checklist" color="#ef6c00" dense clickable :active="entity === 'actionItems'" :loading="!counts" :hint="tileHint('actionItems', 'action items')" @click="toggle('actionItems')" />
        <app-stat-tile label="Hours logged" :value="hoursLabel(counts?.timesheetHours)" icon="o_schedule" color="#546e7a" dense clickable :active="entity === 'time'" :loading="!counts" :hint="tileHint('time', 'logged time')" @click="toggle('time')" />
      </div>
      <q-btn flat round dense color="grey-8" icon="o_refresh" :loading="anyLoading" aria-label="Refresh" @click="refresh">
        <q-tooltip>Refresh</q-tooltip>
      </q-btn>
    </div>

    <!-- With one kind open: how to read it, and what can be added. -->
    <div v-if="entity !== 'all'" class="rrt__tools">
      <span class="rrt__tools-title"><q-icon :name="current.icon" size="16px" /> {{ current.label }}</span>
      <q-space />
      <app-segmented-control :model-value="view" :options="viewOptions" icon-only aria-label="Reading" @update:model-value="setView" />
      <q-btn v-if="entity === 'tasks' && editable" outline dense no-caps color="primary" icon="o_add" label="Task" @click="addTask" />
      <q-btn v-if="entity === 'questions' && editable" outline dense no-caps color="primary" icon="o_add" label="Question" @click="addQuestion" />
      <q-btn outline dense no-caps color="primary" icon="o_open_in_new" :label="`All ${current.label.toLowerCase()}`" @click="openFullList()">
        <q-tooltip>Open the {{ current.label.toLowerCase() }} list, filtered to this requirement</q-tooltip>
      </q-btn>
    </div>

    <!-- Summary: every kind at a glance, each row opening its record. -->
    <div v-if="entity === 'all'" class="row q-col-gutter-md">
      <div class="col-12 col-lg-6">
        <app-section-card title="Tasks" icon="o_task_alt" :count="cardCount('tasks', tasks)" flush class="rrt__card">
          <template #actions>
            <q-btn flat dense no-caps color="primary" label="Open" @click="pick('tasks')" />
          </template>
          <related-list :loading="loading.tasks" :items="top(tasks)" empty="No tasks yet." empty-icon="o_task_alt">
            <template #item="{ item }">
              <q-item clickable class="rrt__row" @click="onProjectTaskView(item.id)">
                <q-item-section>
                  <q-item-label class="rrt__title"><span class="rrt__num">#{{ item.projectTaskNumber }}</span> {{ item.name }}</q-item-label>
                  <q-item-label caption class="rrt__caption">
                    <app-avatar :name="personName(item.assignedTo)" :size="18" show-name empty="Unassigned" />
                    <span v-if="item.endDate" class="rrt__dot">·</span>
                    <requirement-due-cell v-if="item.endDate" :date="item.endDate" :status-label="item.status?.dropDownValue" />
                  </q-item-label>
                </q-item-section>
                <q-item-section side>
                  <app-option-badge v-if="item.status?.dropDownValue" :option="dropDownBadge(item.status)" />
                </q-item-section>
              </q-item>
            </template>
          </related-list>
          <div v-if="tasks.length > SUMMARY_LIMIT" class="rrt__more">
            <q-btn flat dense no-caps color="primary" icon-right="o_arrow_forward" label="View more" @click="openFullList('tasks')">
              <q-tooltip>Open the {{ ENTITIES.tasks.label.toLowerCase() }} list, filtered to this requirement</q-tooltip>
            </q-btn>
          </div>
        </app-section-card>
      </div>

      <div class="col-12 col-lg-6">
        <app-section-card title="Issues" icon="o_bug_report" :count="cardCount('issues', issues)" flush class="rrt__card">
          <template #actions>
            <q-btn flat dense no-caps color="primary" label="Open" @click="pick('issues')" />
          </template>
          <related-list :loading="loading.issues" :items="top(issues)" empty="No issues raised." empty-icon="o_bug_report">
            <template #item="{ item }">
              <q-item clickable class="rrt__row" @click="onIssueView(item.id)">
                <q-item-section>
                  <q-item-label class="rrt__title"><span class="rrt__num">#{{ item.issueNumber }}</span> {{ item.name }}</q-item-label>
                  <q-item-label caption class="rrt__caption">
                    <app-avatar :name="personName(item.employee)" :size="18" show-name empty="Unassigned" />
                  </q-item-label>
                </q-item-section>
                <q-item-section side>
                  <div class="row no-wrap q-gutter-xs">
                    <app-option-badge v-if="item.priority?.dropDownValue" :option="dropDownBadge(item.priority)" />
                    <app-option-badge v-if="item.status?.dropDownValue" :option="dropDownBadge(item.status)" />
                  </div>
                </q-item-section>
              </q-item>
            </template>
          </related-list>
          <div v-if="issues.length > SUMMARY_LIMIT" class="rrt__more">
            <q-btn flat dense no-caps color="primary" icon-right="o_arrow_forward" label="View more" @click="openFullList('issues')">
              <q-tooltip>Open the {{ ENTITIES.issues.label.toLowerCase() }} list, filtered to this requirement</q-tooltip>
            </q-btn>
          </div>
        </app-section-card>
      </div>

      <div class="col-12 col-lg-6">
        <app-section-card title="Test cases" icon="o_fact_check" :count="cardCount('testCases', testCases)" flush class="rrt__card">
          <template #actions>
            <q-btn flat dense no-caps color="primary" label="Open" @click="pick('testCases')" />
          </template>
          <related-list :loading="loading.testCases" :items="top(testCases)" empty="No test cases yet." empty-icon="o_fact_check">
            <template #item="{ item }">
              <q-item clickable class="rrt__row" @click="onTestCaseView(item.id, item.planId || item.testPlanId)">
                <q-item-section>
                  <q-item-label class="rrt__title"><span class="rrt__num">#{{ item.testCaseNumber }}</span> {{ item.name }}</q-item-label>
                  <q-item-label caption class="rrt__caption">
                    <app-avatar :name="personName(item.testedByEmployee)" :size="18" show-name empty="Not tested yet" />
                    <template v-if="item.testedDate"><span class="rrt__dot">·</span>{{ item.testedDate }}</template>
                  </q-item-label>
                </q-item-section>
                <q-item-section side>
                  <app-option-badge v-if="item.status?.dropDownValue" :option="dropDownBadge(item.status)" />
                </q-item-section>
              </q-item>
            </template>
          </related-list>
          <div v-if="testCases.length > SUMMARY_LIMIT" class="rrt__more">
            <q-btn flat dense no-caps color="primary" icon-right="o_arrow_forward" label="View more" @click="openFullList('testCases')">
              <q-tooltip>Open the {{ ENTITIES.testCases.label.toLowerCase() }} list, filtered to this requirement</q-tooltip>
            </q-btn>
          </div>
        </app-section-card>
      </div>

      <div class="col-12 col-lg-6">
        <app-section-card title="Questions & answers" icon="o_forum" :count="cardCount('questions', questions)" flush class="rrt__card">
          <template #actions>
            <q-btn flat dense no-caps color="primary" label="Open" @click="pick('questions')" />
          </template>
          <related-list :loading="loading.questions" :items="top(questions)" empty="No questions asked." empty-icon="o_forum">
            <template #item="{ item }">
              <q-item clickable class="rrt__row" @click="onQuestionAnswersView(item.id)">
                <!-- The question and its latest answer as two marked lines: Q, then A. -->
                <q-item-section>
                  <div class="rrt__qa">
                    <span class="rrt__qa-mark rrt__qa-mark--q" aria-hidden="true">Q</span>
                    <div class="rrt__qa-text">
                      <span class="rrt__sr">Question: </span>
                      <span class="rrt__title">{{ item.title }}</span>
                    </div>
                  </div>
                  <div v-if="latestAnswer(item)" class="rrt__qa rrt__qa--answer">
                    <span class="rrt__qa-mark rrt__qa-mark--a" aria-hidden="true">A</span>
                    <div class="rrt__qa-text rrt__answer">
                      <span class="rrt__sr">Answer: </span>{{ excerpt(latestAnswer(item), 140) }}
                    </div>
                  </div>
                  <div v-else class="rrt__qa rrt__qa--answer">
                    <span class="rrt__qa-mark rrt__qa-mark--none" aria-hidden="true">A</span>
                    <div class="rrt__qa-text rrt__qa-waiting">Awaiting an answer</div>
                  </div>
                </q-item-section>
                <q-item-section side top class="rrt__qa-side">
                  <span class="rrt__when">{{ item.createdOnUtc }}</span>
                  <span v-if="answerCount(item) > 1" class="rrt__qa-count">{{ answerCount(item) }} answers</span>
                </q-item-section>
              </q-item>
            </template>
          </related-list>
          <div v-if="questions.length > SUMMARY_LIMIT" class="rrt__more">
            <q-btn flat dense no-caps color="primary" icon-right="o_arrow_forward" label="View more" @click="openFullList('questions')">
              <q-tooltip>Open the {{ ENTITIES.questions.label.toLowerCase() }} list, filtered to this requirement</q-tooltip>
            </q-btn>
          </div>
        </app-section-card>
      </div>

      <div class="col-12 col-lg-6">
        <app-section-card title="Action items" icon="o_checklist" :count="cardCount('actionItems', actionItems)" flush class="rrt__card">
          <template #actions>
            <q-btn flat dense no-caps color="primary" label="Open" @click="pick('actionItems')" />
          </template>
          <related-list :loading="loading.actionItems" :items="top(actionItems)" empty="No action items." empty-icon="o_checklist">
            <template #item="{ item }">
              <q-item clickable class="rrt__row" @click="onProjectActionItemsView(item.id)">
                <q-item-section>
                  <q-item-label class="rrt__title">{{ item.title }}</q-item-label>
                  <q-item-label caption class="rrt__caption">
                    <app-avatar :name="personName(item.employee)" :size="18" show-name empty="Unassigned" />
                    <template v-if="item.dueDate"><span class="rrt__dot">·</span><requirement-due-cell :date="item.dueDate" /></template>
                  </q-item-label>
                </q-item-section>
                <q-item-section side>
                  <app-option-badge v-if="item.priority?.dropDownValue" :option="dropDownBadge(item.priority)" />
                </q-item-section>
              </q-item>
            </template>
          </related-list>
          <div v-if="actionItems.length > SUMMARY_LIMIT" class="rrt__more">
            <q-btn flat dense no-caps color="primary" icon-right="o_arrow_forward" label="View more" @click="openFullList('actionItems')">
              <q-tooltip>Open the {{ ENTITIES.actionItems.label.toLowerCase() }} list, filtered to this requirement</q-tooltip>
            </q-btn>
          </div>
        </app-section-card>
      </div>

      <div class="col-12 col-lg-6">
        <app-section-card title="Time logged" icon="o_schedule" flush class="rrt__card">
          <template #actions>
            <span class="rrt__hours q-mr-sm">{{ totalHours }} h</span>
            <q-btn flat dense no-caps color="primary" label="Open" @click="pick('time')" />
          </template>
          <related-list :loading="loading.timesheet" :items="top(hoursByTask)" empty="No time logged on its tasks." empty-icon="o_schedule">
            <template #item="{ item }">
              <q-item class="rrt__row rrt__row--static">
                <q-item-section>
                  <q-item-label class="rrt__title">{{ item.taskName }}</q-item-label>
                  <q-item-label caption>{{ item.people.join(", ") }}</q-item-label>
                </q-item-section>
                <q-item-section side>
                  <span class="rrt__hours-cell">{{ item.hours }} h</span>
                </q-item-section>
              </q-item>
            </template>
          </related-list>
          <div v-if="hoursByTask.length > SUMMARY_LIMIT" class="rrt__more">
            <q-btn flat dense no-caps color="primary" icon-right="o_arrow_forward" label="View more" @click="pick('time')" />
          </div>
        </app-section-card>
      </div>
    </div>

    <!-- One kind, worked on: the list with its filters beside the selected record, or the full table. -->
    <div v-else class="rrt__work rc-legacy">
      <div v-if="view === 'split'" class="row q-col-gutter-md">
        <div class="col-12 col-md-5">
          <component :is="current.list" :key="`${entity}-list`" :requirement-id="id" :project-id="projectId" :active-tab="entity" @select="onSelect" @search-change="timesheetSearchModel = $event" />
        </div>
        <div class="col-12 col-md-7">
          <component :is="current.detail" v-if="selected" :key="`${entity}-${selectedKey}`" v-bind="detailProps" />
          <q-card v-else flat bordered class="rrt__placeholder">
            <app-empty-state :icon="current.icon" :title="`Select a ${current.singular} to see its details`" />
          </q-card>
        </div>
      </div>
      <component :is="current.table" v-else-if="view === 'table'" :key="`${entity}-table`" :requirement-id="id" :project-id="projectId" :active-tab="entity" />
      <component :is="current.summary" v-else :key="`${entity}-summary`" :requirement-id="id" :project-id="projectId" :active-tab="entity" />
    </div>
  </div>
</template>

<script setup>
// Everything that hangs off a requirement — tasks, issues, test cases, Q&A, action items, time — in one
// place: the figures, a summary of every kind, and for any one kind the working views (the list with its
// filters beside the selected record, or the full table). This replaces the old Dashboard, Workbench and
// Workspace tabs, which showed the same work three ways.
import { ref, computed, reactive, watch, h, defineComponent, defineAsyncComponent } from "vue";
import { useRouter } from "vue-router";
import { usePreferences } from "composables/usePreferences";
import AppSectionCard from "components/common/AppSectionCard.vue";
import AppOptionBadge from "components/common/AppOptionBadge.vue";
import AppAvatar from "components/common/AppAvatar.vue";
import AppStatTile from "components/common/AppStatTile.vue";
import AppSegmentedControl from "components/common/AppSegmentedControl.vue";
import AppEmptyState from "components/common/AppEmptyState.vue";
import RequirementDueCell from "modules/requirement-v2/components/RequirementDueCell.vue";
import requirementCenterService from "src/modules/requirement-center/requirementCenter.service";
import { dropDownBadge, personName } from "modules/requirement-v2/utils/options";
import { stripHtml, excerpt } from "src/utils/richText";
import { initProjectTaskDialogs, onProjectTaskView, onProjectTaskAdd } from "src/modules/project-tasks/utils/dialogs.js";
import { initIssueDialogs, onIssueView } from "src/modules/issue/utils/dialogs.js";
import { initTestCaseDialogs, onTestCaseView } from "src/modules/test-case/utils/dialogs.js";
import { initQuestionsAnswersDialogs, onQuestionAnswersView, onQuestionAnswersAdd } from "src/modules/project-questions-answers/utils/dialogs.js";
import { initProjectActionItemsDialogs, onProjectActionItemsView } from "src/modules/project-action-items/utils/dialogs.js";

const props = defineProps({
  id: { type: String, required: true },
  model: { type: Object, default: null },
  counts: { type: Object, default: null },
  editable: { type: Boolean, default: false },
  // Open on one kind ("tasks", "issues", "testCases", "questions", "actionItems", "time").
  initialEntity: { type: String, default: "all" }
});

const emit = defineEmits(["changed"]);

const router = useRouter();

// The working views come from the Requirement Center's components, loaded only when a kind is opened.
const lazy = (loader) => defineAsyncComponent(loader);
const ENTITIES = {
  tasks: {
    label: "Tasks", singular: "task", icon: "o_task_alt", countKey: "tasks",
    list: lazy(() => import("src/modules/requirement-center/components/list/_taskList.vue")),
    detail: lazy(() => import("src/modules/requirement-center/components/details/_taskDetails.vue")),
    table: lazy(() => import("src/modules/requirement-center/components/tabularView/_taskTabularView.vue")),
    path: "/project-tasks"
  },
  issues: {
    label: "Issues", singular: "issue", icon: "o_bug_report", countKey: "issues",
    list: lazy(() => import("src/modules/requirement-center/components/list/_issueList.vue")),
    detail: lazy(() => import("src/modules/requirement-center/components/details/_issueDetails.vue")),
    table: lazy(() => import("src/modules/requirement-center/components/tabularView/_issueTabularView.vue")),
    path: "/issue"
  },
  testCases: {
    label: "Test cases", singular: "test case", icon: "o_fact_check", countKey: "testCases",
    list: lazy(() => import("src/modules/requirement-center/components/list/_testCaseList.vue")),
    detail: lazy(() => import("src/modules/requirement-center/components/details/_testCaseDetails.vue")),
    table: lazy(() => import("src/modules/requirement-center/components/tabularView/_testCaseTabularView.vue")),
    path: "/test-case"
  },
  questions: {
    label: "Q&A", singular: "question", icon: "o_forum", countKey: "questionsAnswers",
    list: lazy(() => import("src/modules/requirement-center/components/list/_projectQAList.vue")),
    detail: lazy(() => import("src/modules/requirement-center/components/details/_projectQ&ADetails.vue")),
    table: lazy(() => import("src/modules/requirement-center/components/tabularView/_projectQ&ATabularView.vue")),
    path: "/project-questions-answers/list"
  },
  actionItems: {
    label: "Action items", singular: "action item", icon: "o_checklist", countKey: "actionItems",
    list: lazy(() => import("src/modules/requirement-center/components/list/_projectActionItemsList.vue")),
    detail: lazy(() => import("src/modules/requirement-center/components/details/_projectActionItemsDetails.vue")),
    table: lazy(() => import("src/modules/requirement-center/components/tabularView/_projectActionItemsTabularView.vue")),
    path: "/project-action-items"
  },
  time: {
    label: "Time", singular: "timesheet group", icon: "o_schedule", countKey: "timesheetHours",
    list: lazy(() => import("src/modules/requirement-center/components/list/_timesheetList.vue")),
    detail: lazy(() => import("src/modules/requirement-center/components/details/_timesheetDetails.vue")),
    table: lazy(() => import("src/modules/requirement-center/components/tabularView/_timesheetTabularView.vue")),
    summary: lazy(() => import("src/modules/requirement-center/components/details/_timesheetOverview.vue")),
    path: "/timesheet"
  }
};

// A list body: spinner, then rows through the item slot, or an empty line.
const RelatedList = defineComponent({
  props: {
    loading: Boolean,
    items: { type: Array, default: () => [] },
    empty: { type: String, default: "Nothing here." },
    emptyIcon: { type: String, default: "o_inbox" }
  },
  setup (p, { slots }) {
    return () => {
      if (p.loading && !p.items.length) return h("div", { class: "rrt__loading" }, [h("span", "Loading…")]);
      if (!p.items.length) return h("div", { class: "rrt__empty" }, p.empty);
      return h("div", { class: "rrt__list" }, p.items.map((item) => slots.item?.({ item })));
    };
  }
});

const activeRowId = ref(null);
initProjectTaskDialogs(activeRowId);
initIssueDialogs(activeRowId);
initTestCaseDialogs(activeRowId);
initQuestionsAnswersDialogs(activeRowId);
initProjectActionItemsDialogs(activeRowId);

const projectId = computed(() => props.model?.project?.id || props.model?.projectId || "");
const projectModuleId = computed(() => props.model?.projectModule?.id || props.model?.projectModuleId || "");

const hoursLabel = (hours) => {
  const n = Number(hours || 0);
  return n % 1 === 0 ? n : Number(n.toFixed(2));
};

// ---- Which kind, which reading ----
const prefs = usePreferences("requirement-related");
const entity = ref(props.initialEntity in ENTITIES ? props.initialEntity : "all");
const view = ref(prefs.get("view", "split"));
const selected = ref(null);
const timesheetSearchModel = ref({});

const current = computed(() => ENTITIES[entity.value] || ENTITIES.tasks);

const viewOptions = computed(() => [
  { value: "split", icon: "o_vertical_split", label: "List and detail" },
  { value: "table", icon: "o_table_rows", label: "Table" },
  ...(entity.value === "time" ? [{ value: "summary", icon: "o_summarize", label: "Summary" }] : [])
]);

const pick = (key) => {
  entity.value = key;
  selected.value = null;
  if (view.value === "summary" && key !== "time") view.value = "split";
};

const setView = (next) => {
  view.value = next;
  if (next !== "summary") prefs.set("view", next);
};

// The Summary tile counts the linked records of every kind (time is hours, not records).
const linkedTotal = computed(() => {
  const c = props.counts || {};
  return (c.tasks || 0) + (c.issues || 0) + (c.testCases || 0) + (c.questionsAnswers || 0) + (c.actionItems || 0);
});

// A tile opens its kind; the open one, clicked again, goes back to the summary.
const toggle = (key) => pick(entity.value === key ? "all" : key);
const tileHint = (key, what) => (entity.value === key ? "Showing " + what + ". Click to go back to the summary." : "Click to open the " + what);

const onSelect = (item) => {
  selected.value = item;
};

// A changing key remounts the detail component for a new selection (the legacy ones load on mount).
const selectedKey = computed(() => selected.value?.id || selected.value?.groupId || JSON.stringify(selected.value || {}));

const detailProps = computed(() => {
  const item = selected.value || {};
  switch (entity.value) {
    case "testCases": return { id: item.id, testPlanId: item.testPlanId || item.planId || "" };
    case "questions": return { id: item.id, requirementId: props.id, projectId: projectId.value };
    case "time": return { requirementId: props.id, searchModel: timesheetSearchModel.value, group: selected.value };
    default: return { id: item.id };
  }
});

// The module's own list, filtered to this requirement (what the old Dashboard cards linked to).
const openFullList = (key = entity.value) => {
  router.push({
    path: ENTITIES[key].path,
    state: { projectId: projectId.value, projectModuleId: projectModuleId.value, requirementId: props.id }
  });
};

// ---- Summary data ----
const tasks = ref([]);
const issues = ref([]);
const testCases = ref([]);
const questions = ref([]);
const actionItems = ref([]);
const timesheet = ref([]);
const loading = reactive({ tasks: false, issues: false, testCases: false, questions: false, actionItems: false, timesheet: false });
const anyLoading = computed(() => Object.values(loading).some(Boolean));

// Each summary card shows the newest few; one row more is read only to know whether "View more" is needed.
const SUMMARY_LIMIT = 10;
const top = (list) => list.slice(0, SUMMARY_LIMIT);
const cardCount = (key, list) => props.counts?.[ENTITIES[key].countKey] ?? Math.min(list.length, SUMMARY_LIMIT);

const page = () => ({ requirementId: props.id, page: 1, pageSize: SUMMARY_LIMIT + 1, sortBy: "createdOnUtc", descending: true });

const fetchInto = async (key, target, call, extract = (r) => r) => {
  loading[key] = true;
  try {
    target.value = extract(await call()) || [];
  } catch (error) {
    console.error(`Error loading related ${key}`, error);
  } finally {
    loading[key] = false;
  }
};

const loadAll = () => Promise.all([
  fetchInto("tasks", tasks, () => requirementCenterService.getTasksByRequirementId(page())),
  fetchInto("issues", issues, () => requirementCenterService.getIssuesByRequirementId(page())),
  fetchInto("testCases", testCases, () => requirementCenterService.getTestCasesByRequirementId(page())),
  fetchInto("questions", questions, () => requirementCenterService.getProjectQAByRequirementId(page()), (r) => r?.projectQuestionsAnswerList),
  fetchInto("actionItems", actionItems, () => requirementCenterService.getProjectActionItemsByRequirementId(page()), (r) => r?.projectActionItemList),
  fetchInto("timesheet", timesheet, () => requirementCenterService.getTimesheetByRequirementId(props.id))
]);

// A question's first answer is typed into its own "Answer" field (saved as its description); the ones
// added later are its response log. The newest of either counts as answered.
const latestAnswer = (item) => (stripHtml(item.lastAnswer) ? item.lastAnswer : stripHtml(item.description) ? item.description : "");
const answerCount = (item) => (stripHtml(item.description) ? 1 : 0) + (item.projectQuestionsAnswersResponseLog?.length || 0);

// A timesheet line carries its hours as "HH:mm" text (the API's HoursConverter); a plain number is read as is.
const lineHours = (value) => {
  if (typeof value === "number") return Number.isFinite(value) ? value : 0;
  const match = /^\s*(\d+):(\d{1,2})/.exec(value || "");
  if (match) return Number(match[1]) + Number(match[2]) / 60;
  const n = Number(value);
  return Number.isFinite(n) ? n : 0;
};

const hoursByTask = computed(() => {
  const groups = new Map();
  for (const line of timesheet.value) {
    const key = line.task?.id || line.task?.name || "—";
    if (!groups.has(key)) groups.set(key, { id: key, taskName: line.task?.name || "Task", hours: 0, peopleSet: new Set() });
    const g = groups.get(key);
    g.hours += lineHours(line.hours);
    const who = personName(line.timesheet?.user) || personName(line.timesheet?.employee);
    if (who) g.peopleSet.add(who);
  }
  return [...groups.values()]
    .map((g) => ({ ...g, hours: Math.round(g.hours * 100) / 100, people: [...g.peopleSet] }))
    .sort((a, b) => b.hours - a.hours);
});

const totalHours = computed(() => Math.round(timesheet.value.reduce((sum, l) => sum + lineHours(l.hours), 0) * 100) / 100);

// A refresh reloads the summary and remounts the open working view.
const workKey = ref(0);
const refresh = () => {
  loadAll();
  workKey.value += 1;
  emit("changed");
};

const addTask = () => onProjectTaskAdd(refresh, null, projectId.value, projectModuleId.value, props.id);
const addQuestion = () => onQuestionAnswersAdd(projectId.value, props.id, refresh);

watch(() => props.id, () => {
  selected.value = null;
  loadAll();
}, { immediate: true });

defineExpose({ reload: loadAll, pick });
</script>

<style scoped>
.rrt__kpis {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: 8px;
  margin-bottom: 12px;
}
.rrt__head {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
}
.rrt__head .rrt__kpis {
  flex: 1 1 auto;
  min-width: 0;
  margin-bottom: 0;
}
.rrt__tools {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 12px;
}
.rrt__tools-title {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  font-size: 13.5px;
  font-weight: 600;
  color: var(--ink-900, #16262c);
}
.rrt__card {
  height: 100%;
}
.rrt__row {
  min-height: 48px;
  border-bottom: 1px solid var(--line, #dde6e8);
}
.rrt__row:last-child {
  border-bottom: none;
}
.rrt__row--static {
  cursor: default;
}
.rrt__title {
  font-size: 13.5px;
  font-weight: 500;
  color: var(--ink-900, #16262c);
}
.rrt__num {
  color: var(--ink-500, #647a82);
  font-weight: 600;
  font-size: 12px;
}
.rrt__caption {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 4px;
}
.rrt__dot {
  color: var(--ink-300, #a6b8bd);
}
.rrt__answer {
  color: var(--ink-700, #3a4d54);
}
.rrt__when {
  font-size: 11.5px;
  color: var(--ink-500, #647a82);
  white-space: nowrap;
}
.rrt__hours {
  font-weight: 700;
  color: var(--q-primary);
  font-variant-numeric: tabular-nums;
}
.rrt__hours-cell {
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}
.rrt__placeholder {
  border-radius: 12px;
  box-shadow: none;
  min-height: 240px;
  display: flex;
  align-items: center;
  justify-content: center;
}

/* The working views are the Requirement Center's own components, which predate the App* layer; these
   rules give them the page's surfaces (white sticky table heads, 12px cards, no brand-blue borders). */
.rrt__work :deep(.dashboard-card),
.rrt__work :deep(.summary-card),
.rrt__work :deep(.card-border) {
  border: 1px solid var(--line, #dde6e8) !important;
  border-radius: 12px !important;
  box-shadow: none !important;
}
.rrt__work :deep(thead tr.bg-primary) {
  background: #f7f9fb !important;
}
.rrt__work :deep(thead tr.bg-primary th) {
  color: var(--ink-700, #3a4d54) !important;
  font-weight: 600;
  border-bottom: 1px solid var(--line, #dde6e8);
}
.rrt__work :deep(.q-tab) {
  margin-right: 6px;
  border: 1px solid var(--line, #dde6e8);
  border-radius: 8px;
  min-height: 36px;
  padding: 0 12px;
}
.rrt__work :deep(.q-tab--active) {
  background: var(--brand-050, #f3f8fb);
  border-color: var(--q-primary);
}
.rrt__work :deep(.q-tab__indicator) {
  display: none;
}
.rrt__work :deep(.bg-blue-1) {
  background: var(--brand-050, #f3f8fb) !important;
}
</style>

<style>
/* Rendered by the inline list component (no scoped attribute); namespaced by the rrt__ prefix. */
.rrt__list {
  max-height: 360px;
  overflow: auto;
}
.rrt__empty,
.rrt__loading {
  padding: 18px 16px;
  color: var(--ink-500, #647a82);
  font-size: 13px;
}
/* Question and answer, each behind its own letter. */
.rrt__qa {
  display: flex;
  align-items: flex-start;
  gap: 8px;
}
.rrt__qa--answer {
  margin-top: 6px;
}
.rrt__qa-mark {
  flex: 0 0 20px;
  width: 20px;
  height: 20px;
  border-radius: 6px;
  font-size: 11px;
  font-weight: 700;
  line-height: 20px;
  text-align: center;
}
.rrt__qa-mark--q {
  background: var(--q-primary);
  color: #fff;
}
.rrt__qa-mark--a {
  background: #e3f4e8;
  color: #1b6b3a;
}
.rrt__qa-mark--none {
  background: #fff3e0;
  color: #b25a00;
}
.rrt__qa-text {
  min-width: 0;
  font-size: 12.5px;
  line-height: 20px;
  word-break: break-word;
}
.rrt__qa-waiting {
  color: #b25a00;
  font-style: italic;
}
.rrt__qa-side {
  align-items: flex-end;
  gap: 4px;
}
.rrt__qa-count {
  padding: 0 8px;
  border-radius: 999px;
  background: #e3f4e8;
  color: #1b6b3a;
  font-size: 11px;
  font-weight: 600;
  white-space: nowrap;
}
.rrt__sr {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0 0 0 0);
  white-space: nowrap;
}
.rrt__more {
  display: flex;
  justify-content: center;
  padding: 4px 8px;
  border-top: 1px solid var(--line, #dde6e8);
}
</style>
