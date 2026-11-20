<template>
  <q-page padding class="rfp">
    <app-detail-header :items="crumbs" :back-to="{ name: 'requirement-v2-detail', params: { id } }">
      <template #note>
        Changes to an existing requirement save themselves about a second after you stop editing; the
        indicator says where things stand. Document references save the same way once a row has a path
        and a name. The status, the priority and the planned / actual dates save the moment you pick
        them and keep a change log (the status and the dates with an optional note). The requirement log
        and notes are written on the requirement's own page.
      </template>
      <template #actions>
        <app-auto-save-state :state="autosave.state.value" :message="autosave.message.value" :saved-at="autosave.savedAt.value" @save-now="autosave.run()" />
        <q-btn unelevated no-caps color="primary" icon="o_check" label="Done" :loading="finishing" @click="done" />
      </template>
    </app-detail-header>

    <div v-if="loading" class="row flex-center q-pa-xl">
      <q-spinner color="primary" size="40px" />
    </div>

    <template v-else>
      <q-banner v-if="autosave.state.value === 'error'" dense rounded class="bg-red-1 text-negative q-mb-md rfp__banner">
        <template #avatar><q-icon name="o_cloud_off" color="negative" /></template>
        The last change could not be saved{{ autosave.message.value ? ` — ${autosave.message.value}` : "." }} Your edits are still here; saving is retried as you continue.
        <template #action>
          <q-btn flat no-caps color="negative" label="Save now" @click="autosave.run()" />
        </template>
      </q-banner>

      <q-form greedy @submit.prevent.stop="autosave.run()">
        <requirement-form-fields
          ref="fieldsRef"
          v-model="model"
          :logged-busy="loggedBusy"
          @status-change="setStatus"
          @priority-change="setPriority"
          @date-change="setDate"
          @date-log="showDateLog"
        >
          <template #aside>
            <app-section-card ref="docsCardRef" title="Document references" icon="o_link" :count="liveDocs || null" info="Links to the documents that describe the requirement — a specification, a mock-up, a signed form — each with a name and a note. A new row saves once it has a path and a name." dense class="q-mb-md">
              <requirement-document-refs ref="docsRef" v-model="documentRefs" hide-title />
            </app-section-card>
          </template>
        </requirement-form-fields>
      </q-form>

      <div class="rfp__bar">
        <app-auto-save-state :state="autosave.state.value" :message="autosave.message.value" :saved-at="autosave.savedAt.value" @save-now="autosave.run()" />
        <div class="rfp__bar-note text-caption text-grey-7">Required fields are marked with a star.</div>
        <q-btn unelevated no-caps color="primary" icon="o_check" label="Done" :loading="finishing" @click="done" />
      </div>
    </template>
  </q-page>
</template>

<script setup>
// Edit Requirement, as a page of its own, saving itself as the reader edits: the details and the document
// references go to the server about a second after the last change, and leaving flushes what is due.
// The status, the priority and the four planned / actual dates are saved on their own, the moment they
// are picked, through the logged actions the list uses.
import { ref, computed, nextTick, watch, onMounted } from "vue";
import { useRoute, useRouter, onBeforeRouteLeave } from "vue-router";
import { format } from "date-fns";
import { useAuthStore } from "stores/auth";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useAutoSave } from "composables/useAutoSave";
import AppDetailHeader from "components/common/AppDetailHeader.vue";
import AppSectionCard from "components/common/AppSectionCard.vue";
import AppAutoSaveState from "components/common/AppAutoSaveState.vue";
import RequirementFormFields from "modules/requirement-v2/components/RequirementFormFields.vue";
import RequirementDocumentRefs from "modules/requirement-v2/components/RequirementDocumentRefs.vue";
import requirementService from "modules/requirement-v2/requirementV2.service";
import { askChangeNote } from "modules/requirement-v2/utils/changeNote";
import { onSubmitRequirementStatus, onSubmitRequirementPriority, onSubmitRequirementDate } from "src/modules/requirement-v2/utils/actions.js";
import { initSiteDialogs, onSiteModifiedLog } from "src/modules/sites/utils/dialogs.js";

const route = useRoute();
const router = useRouter();
const notify = useNotify();
const { confirm } = useConfirm();
const user = useAuthStore().user;

const id = computed(() => String(route.params.id || ""));
const isViewer = user?.roles?.some((r) => r?.toLowerCase() === "viewer") ?? false;

// The change-log dialog marks the record it was opened for.
const activeRowId = ref(null);
initSiteDialogs(activeRowId);

const loading = ref(true);
const finishing = ref(false);
const model = ref({});
const documentRefs = ref([]);
const fieldsRef = ref(null);
const docsRef = ref(null);
const docsCardRef = ref(null);
let snapshot = "";
let leaving = false;

const crumbs = computed(() => [
  { label: "Home", icon: "o_home", to: user?.siteLandingPageLink || "/" },
  { label: "SDLC" },
  { label: "Requirements", to: { name: "requirement-v2" } },
  { label: model.value.requirementNumber ? `#${model.value.requirementNumber}` : "Requirement", to: { name: "requirement-v2-detail", params: { id: id.value } } },
  { label: "Edit" }
]);

const liveDocs = computed(() => documentRefs.value.filter((d) => !d.deleted).length);

// What counts as a change: the form and the document rows against what was last saved. `projectStatus`
// is derived by the field set from the project list after load, so it is left out.
const serialise = () => {
  const { projectStatus, ...fields } = model.value;
  return JSON.stringify({ m: fields, d: documentRefs.value.map((d) => ({ ...d })) });
};

// The API returns calendar dates as ISO instants; the form (and the API on the way back) use the
// MM/DD/YYYY *Str fields. Formatted in the browser's zone, as the previous form did.
const toDateStr = (value) => (value ? format(value, "MM/dd/yyyy") : "");

const load = async () => {
  loading.value = true;
  let turnedAway = false;
  try {
    const resp = await requirementService.getRequirementDetails(id.value);
    // The list and the record only offer Edit to those who may; an address typed by hand gets the same
    // answer, and the form is never shown (the page stays on its spinner until the record's page opens).
    if (isViewer || resp?.project?.currentUserManage !== true) {
      turnedAway = true;
      leaving = true;
      notify.warning("You can read this requirement, but editing it needs full access on its project.");
      router.replace({ name: "requirement-v2-detail", params: { id: id.value } });
      return;
    }
    model.value = {
      ...resp,
      identifiedDateStr: toDateStr(resp.identifiedDate),
      plannedStartDateStr: toDateStr(resp.plannedStartDate),
      plannedEndDateStr: toDateStr(resp.plannedEndDate),
      actualStartDateStr: toDateStr(resp.actualStartDate),
      actualEndDateStr: toDateStr(resp.actualEndDate),
      closeDateStr: toDateStr(resp.closeDate),
      description: resp.description || "",
      scope: resp.scope || "",
      shortDescription: resp.shortDescription || ""
    };
    documentRefs.value = (resp.filePathDetails || []).map((item) => ({ ...item, flag: "Edit" }));
    snapshot = serialise();
  } catch (error) {
    // The interceptor has said why; a requirement that cannot be read cannot be edited either.
    console.error("Error loading requirement", error);
    turnedAway = true;
    leaving = true;
    router.replace({ name: "requirement-v2" });
  } finally {
    if (!turnedAway) loading.value = false;
  }
};

// ---- Auto-save ----
// Only what the reader changed is validated: a record that has always lacked, say, a customer owner
// still saves its other edits, and no untouched field turns red.
const changedKeys = () => {
  const loaded = JSON.parse(snapshot).m;
  const { projectStatus, ...fields } = model.value;
  return Object.keys(fields).filter((key) => JSON.stringify(fields[key] ?? null) !== JSON.stringify(loaded[key] ?? null));
};

// A logged save in flight (status, priority or a date), if any. The form's own save waits for it, so the
// two never cross and the form never sends the value the logged save is replacing.
let loggedWrite = null;

const autosave = useAutoSave({
  debounceMs: 1200,
  save: async () => {
    if (loggedWrite) await loggedWrite;
    if (serialise() === snapshot) return undefined;

    const fieldsOk = fieldsRef.value?.validateKeys?.(changedKeys()) ?? true;
    const docsOk = docsRef.value?.validate({ ignoreBlankNew: true }) ?? true;
    if (!fieldsOk || !docsOk) return "Fix the highlighted fields to save";

    // A row just added and still empty is not sent; the rest go with their flags. A failure is reported
    // by the indicator, not by the global toast.
    const docs = documentRefs.value.filter((d) => !docsRef.value?.isBlankNew?.(d));
    await requirementService.saveRequirement(id.value, { ...model.value, filePathDetailsModel: docs }, { silent: true });

    // What was sent is now what is saved: removed rows go, new rows become stored ones.
    documentRefs.value = documentRefs.value
      .filter((d) => !d.deleted)
      .map((d) => (d.flag === "New" && !docsRef.value?.isBlankNew?.(d) ? { ...d, flag: "Edit" } : d));
    snapshot = serialise();
    return undefined;
  }
});

// Every change arms the save; a change that brings the form back to what is saved has nothing to save,
// and clears a stale failure with it.
watch([model, documentRefs], () => {
  if (loading.value) return;
  if (serialise() !== snapshot) autosave.mark();
  else if (autosave.unsaved.value) autosave.reset();
}, { deep: true });

// ---- Status, priority and the planned / actual dates: saved on their own, through their logged actions ----
const loggedBusy = ref("");

// What a logged action saved is already on the server: it goes into the form and into the last-saved
// snapshot together, so it neither triggers the form's save nor rides along with it as a "change".
const applySaved = (patch) => {
  const nothingElsePending = serialise() === snapshot;
  Object.assign(model.value, patch);
  if (nothingElsePending) {
    snapshot = serialise();
  } else {
    const saved = JSON.parse(snapshot);
    Object.assign(saved.m, patch);
    snapshot = JSON.stringify(saved);
  }
};

// `write(done)` runs one of the module's logged actions; they report success by calling `done` and
// say what went wrong themselves. Whatever the form still owes is saved first.
const saveLogged = async (key, patch, write) => {
  if (loggedWrite) await loggedWrite;
  await autosave.flush();
  loggedBusy.value = key;
  loggedWrite = (async () => {
    let saved = false;
    await write(() => { saved = true; });
    if (saved) applySaved(patch);
    return saved;
  })();
  try {
    return await loggedWrite;
  } finally {
    loggedWrite = null;
    loggedBusy.value = "";
  }
};

// A status change may carry a note (optional), kept in the status change log. Cancelling the prompt
// leaves the status as it was.
const setStatus = async (statusId) => {
  if (!statusId || statusId === model.value.statusId) return;
  const { ok, note } = await askChangeNote("Requirement Status Note", "Add a note for this status change (optional).");
  if (!ok) return;
  await saveLogged("status", { statusId }, (done) => onSubmitRequirementStatus(id.value, statusId, done, note));
};

const setPriority = async (priorityId) => {
  if (!priorityId || priorityId === model.value.priorityId) return;
  await saveLogged("priority", { priorityId }, (done) => onSubmitRequirementPriority(id.value, priorityId, done));
};

// A date change is saved with its note and written to that date's change log.
const setDate = async ({ field, log, value }) => {
  const { ok, note } = await askChangeNote(`${log} Note`, `Add a note for this ${log.toLowerCase()} change (optional).`);
  if (!ok) return;
  await saveLogged(field, { [field]: value, [`${field}Str`]: value }, (done) => onSubmitRequirementDate(id.value, value, field, note, done));
};

const showDateLog = (log) => onSiteModifiedLog(id.value, model.value.title, log);

// Leaving flushes what is due; only a failed or blocked save asks the question.
const settleBeforeLeaving = async () => {
  const state = await autosave.flush();
  if (state === "error" || state === "blocked") {
    return confirm({
      title: "Leave without saving?",
      message: state === "blocked"
        ? "Some fields are invalid, so the latest changes could not be saved. Leave anyway and lose them?"
        : "The latest changes could not be saved. Leave anyway and lose them?",
      confirmLabel: "Leave",
      cancelLabel: "Keep editing",
      type: "danger"
    });
  }
  return true;
};

onBeforeRouteLeave(async () => {
  if (leaving) return true;
  return settleBeforeLeaving();
});

const done = async () => {
  finishing.value = true;
  try {
    const ok = await settleBeforeLeaving();
    if (!ok) return;
    leaving = true;
    if (autosave.state.value === "saved") notify.success("Requirement updated.");
    // Back to the requirement's page: the one the reader came from when there is one (so its own Back
    // still leads to the list), a fresh one in this page's place otherwise.
    const record = router.resolve({ name: "requirement-v2-detail", params: { id: id.value } });
    const cameFrom = String(window.history.state?.back || "").split("?")[0];
    if (cameFrom === record.path) router.back();
    else router.replace({ name: "requirement-v2-detail", params: { id: id.value } });
  } finally {
    finishing.value = false;
  }
};

onMounted(async () => {
  await load();
  // ?section=documents (from the record's Documents tab) lands on the references.
  if (route.query.section === "documents") {
    await nextTick();
    docsCardRef.value?.$el?.scrollIntoView?.({ behavior: "smooth", block: "start" });
  }
});
</script>

<style scoped>
.rfp__banner {
  border-radius: 10px;
}
.rfp__bar {
  position: sticky;
  bottom: 0;
  z-index: 2;
  display: flex;
  align-items: center;
  gap: 8px;
  margin: 0 -16px -16px;
  padding: 10px 16px;
  background: #fff;
  border-top: 1px solid var(--line, #dde6e8);
}
.rfp__bar-note {
  flex: 1 1 auto;
  text-align: center;
}
@media (max-width: 599px) {
  .rfp__bar {
    flex-wrap: wrap;
  }
  .rfp__bar-note {
    order: -1;
    width: 100%;
  }
}
</style>
