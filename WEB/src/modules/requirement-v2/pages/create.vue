<template>
  <q-page padding class="rfp">
    <app-detail-header :items="crumbs" :back-to="{ name: 'requirement-v2' }">
      <template #note>
        A requirement belongs to a project module. Its first log entry goes in the Requirement Log; later
        entries are added from the requirement's Log tab. "Save & add another" keeps the project and
        module for the next one.
      </template>
      <template #actions>
        <q-btn flat no-caps color="grey-8" label="Cancel" @click="cancel" />
        <q-btn outline no-caps color="primary" label="Save & add another" :loading="processing && another" :disable="processing" @click="onSubmit(true)" />
        <q-btn unelevated no-caps color="primary" icon="o_save" label="Save" :loading="processing && !another" :disable="processing" @click="onSubmit()" />
      </template>
    </app-detail-header>

    <q-banner v-if="savedCount" dense rounded class="bg-green-1 text-green-9 q-mb-md rfp__banner">
      <template #avatar><q-icon name="o_check_circle" color="green-9" /></template>
      {{ savedCount }} requirement{{ savedCount === 1 ? "" : "s" }} created so far. The project and module are kept for the next one.
      <template #action>
        <q-btn flat no-caps color="green-9" label="Open the last one" @click="openLast" />
      </template>
    </q-banner>

    <q-form greedy @submit.prevent.stop="onSubmit()">
      <requirement-form-fields ref="fieldsRef" v-model="model" is-new show-log>
        <template #aside>
          <app-section-card title="Document references" icon="o_link" info="Links to the documents that describe the requirement — a specification, a mock-up, a signed form — each with a name and a note." dense class="q-mb-md">
            <requirement-document-refs ref="docsRef" v-model="documentRefs" hide-title />
          </app-section-card>
        </template>
      </requirement-form-fields>
    </q-form>

    <!-- The same actions again at the foot of a long form, always in view. -->
    <div class="rfp__bar">
      <q-btn flat no-caps color="grey-8" label="Cancel" @click="cancel" />
      <div class="rfp__bar-note text-caption text-grey-7">Required fields are marked with a star.</div>
      <q-btn outline no-caps color="primary" label="Save & add another" :loading="processing && another" :disable="processing" @click="onSubmit(true)" />
      <q-btn unelevated no-caps color="primary" icon="o_save" label="Save" :loading="processing && !another" :disable="processing" @click="onSubmit()" />
    </div>
  </q-page>
</template>

<script setup>
// Create Requirement, as a page of its own. The list's project / module filters arrive as query
// parameters so a new requirement lands where the reader was looking.
import { ref, computed } from "vue";
import { useRoute, useRouter, onBeforeRouteLeave } from "vue-router";
import { format } from "date-fns";
import { useAuthStore } from "stores/auth";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import AppDetailHeader from "components/common/AppDetailHeader.vue";
import AppSectionCard from "components/common/AppSectionCard.vue";
import RequirementFormFields from "modules/requirement-v2/components/RequirementFormFields.vue";
import RequirementDocumentRefs from "modules/requirement-v2/components/RequirementDocumentRefs.vue";
import requirementService from "modules/requirement-v2/requirementV2.service";

const route = useRoute();
const router = useRouter();
const notify = useNotify();
const { confirm } = useConfirm();
const user = useAuthStore().user;

const query = (key) => (Array.isArray(route.query[key]) ? route.query[key][0] : route.query[key]) || null;

const crumbs = [
  { label: "Home", icon: "o_home", to: user?.siteLandingPageLink || "/" },
  { label: "SDLC" },
  { label: "Requirements", to: { name: "requirement-v2" } },
  { label: "New requirement" }
];

const today = format(new Date(), "MM/dd/yyyy");

const blankModel = (keep = {}) => ({
  projectId: keep.projectId ?? query("projectId"),
  projectModuleId: keep.projectModuleId ?? query("moduleId"),
  requirementGroupId: keep.requirementGroupId ?? null,
  title: "",
  shortDescription: "",
  scope: "",
  description: "",
  requirementTypeId: null,
  // Asked for (and required) when the type is Data Integration.
  sourceLabel: null,
  targetLabel: null,
  areaId: null,
  workspaceId: null,
  priorityId: null,
  notes: "",
  identifiedUserType: "",
  employeeId: "",
  identifiedEmployeeId: user?.employeeId || "",
  identifiedCustomerId: null,
  requirementOwnerId: user?.employeeId || "",
  customerOwnerId: keep.customerOwnerId ?? null,
  requirementEnteredBy: user?.employeeId || "",
  confirmedById: null,
  approvedById: null,
  statusId: "",
  approvalStatus: "",
  identifiedDateStr: today,
  closeDateStr: today,
  plannedStartDateStr: "",
  plannedEndDateStr: "",
  actualStartDateStr: "",
  actualEndDateStr: "",
  editingStatus: 0
});

const model = ref(blankModel());
const documentRefs = ref([]);
const fieldsRef = ref(null);
const docsRef = ref(null);
const processing = ref(false);
const another = ref(false);
const savedCount = ref(0);
const lastId = ref("");
let leaving = false;

// A viewer does not create requirements (the list keeps the button from them); an address typed by hand
// gets the same answer.
const isViewer = user?.roles?.some((r) => r?.toLowerCase() === "viewer") ?? false;
if (isViewer) {
  leaving = true;
  notify.warning("Your viewer role cannot create requirements.");
  router.replace({ name: "requirement-v2" });
}

// Anything typed counts as a draft worth a question before it is thrown away.
const dirty = computed(() => {
  const m = model.value;
  return !!(m.title || m.shortDescription || m.scope || m.description || documentRefs.value.length);
});

onBeforeRouteLeave(async () => {
  if (leaving || !dirty.value) return true;
  return confirm({ title: "Discard this requirement?", message: "What you typed will be lost.", confirmLabel: "Discard", cancelLabel: "Keep editing", type: "danger" });
});

// Back to where the reader came from (the list, usually); the list itself when the page was opened cold.
const cancel = () => {
  if (window.history.state?.back) router.back();
  else router.push({ name: "requirement-v2" });
};

const openLast = () => {
  if (lastId.value) router.push({ name: "requirement-v2-detail", params: { id: lastId.value } });
};

const onSubmit = async (addAnother = false) => {
  if (processing.value) return;

  const fieldsOk = await fieldsRef.value?.validate();
  const docsOk = docsRef.value?.validate() ?? true;
  if (!fieldsOk || !docsOk) {
    notify.error("Please fix the highlighted fields.");
    return;
  }

  processing.value = true;
  another.value = addAnother;
  try {
    const id = await requirementService.saveRequirement("", {
      ...model.value,
      filePathDetailsModel: documentRefs.value,
      requirementChangeLogModel: []
    });
    savedCount.value += 1;
    lastId.value = typeof id === "string" ? id : "";
    if (addAnother) {
      notify.success("Requirement created. Ready for the next one.");
      const keep = { projectId: model.value.projectId, projectModuleId: model.value.projectModuleId, requirementGroupId: model.value.requirementGroupId, customerOwnerId: model.value.customerOwnerId };
      model.value = blankModel(keep);
      documentRefs.value = [];
      window.scrollTo({ top: 0, behavior: "smooth" });
    } else {
      notify.success("Requirement created.");
      leaving = true;
      if (lastId.value) router.replace({ name: "requirement-v2-detail", params: { id: lastId.value } });
      else router.replace({ name: "requirement-v2" });
    }
  } catch (error) {
    // The interceptor has already shown the server's message; the page keeps the typed data.
    console.error("Error creating requirement", error);
  } finally {
    processing.value = false;
    another.value = false;
  }
};
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
