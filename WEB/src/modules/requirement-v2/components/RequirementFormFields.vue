<template>
  <div class="row q-col-gutter-md rff">
    <!-- Status is set by default on create and never typed; a missing default is the only way it fails. -->
    <div v-if="v$.statusId.$error" class="col-12">
      <q-banner dense rounded class="bg-red-1 text-negative">
        <template #avatar><q-icon name="o_error" color="negative" /></template>
        No default requirement status could be set. Add a "New" value under Manage Dropdowns → Requirement Status.
      </q-banner>
    </div>

    <!-- Main column: what the requirement is. -->
    <div class="col-12 col-lg-8">
      <app-section-card ref="aboutCard" title="About" icon="o_assignment" collapsible dense class="q-mb-md">
        <div class="row q-col-gutter-sm">
          <app-text-field
            v-model="model.title"
            class="col-12"
            label="Requirement"
            required
            type="textarea"
            autogrow
            maxlength="200"
            placeholder="e.g. Customers can export their invoices as PDF"
            :error="v$.title.$error"
            :error-message="err(v$.title)"
            @blur="v$.title.$touch"
          />
          <app-text-field
            v-model="model.shortDescription"
            class="col-12"
            label="Short Description"
            type="textarea"
            autogrow
            maxlength="200"
            counter
            info="Shown with the requirement in the requirement list and in the weekly and monthly planners."
          />
          <app-select
            v-model="model.projectId"
            class="col-12 col-sm-6"
            label="Project"
            required
            option-label="text"
            :options="projects.list.value"
            :loading="loading.projects"
            :error="v$.projectId.$error"
            :error-message="err(v$.projectId)"
            @blur="v$.projectId.$touch"
          />
          <app-select
            v-model="model.projectModuleId"
            class="col-12 col-sm-6"
            label="Project Module"
            required
            option-label="text"
            info="Modules of the selected project."
            :options="modules.list.value"
            :loading="loading.modules"
            :disable="!model.projectId"
            :error="v$.projectModuleId.$error"
            :error-message="err(v$.projectModuleId)"
            @blur="v$.projectModuleId.$touch"
          />
          <app-select v-model="model.requirementTypeId" class="col-12 col-sm-6 col-md-4" label="Type" option-label="text" :options="types.list.value" />
          <app-select v-model="model.areaId" class="col-12 col-sm-6 col-md-4" label="Area" option-label="text" :options="areas.list.value" />
          <app-select v-model="model.workspaceId" class="col-12 col-sm-6 col-md-4" label="Workspace" option-label="text" :options="workspaces.list.value" />
          <!-- A Data Integration requirement names the two sides of its data mapping. -->
          <template v-if="isDataIntegration">
            <app-text-field
              v-model="model.sourceLabel"
              class="col-12 col-sm-6"
              label="Source Label"
              required
              info="The label that identifies the source data column in the Requirement Data Mapping section."
              :error="v$.sourceLabel.$error"
              :error-message="err(v$.sourceLabel)"
              @blur="v$.sourceLabel.$touch"
            />
            <app-text-field
              v-model="model.targetLabel"
              class="col-12 col-sm-6"
              label="Target Label"
              required
              info="The label that identifies the target data column in the Requirement Data Mapping section."
              :error="v$.targetLabel.$error"
              :error-message="err(v$.targetLabel)"
              @blur="v$.targetLabel.$touch"
            />
          </template>
        </div>
      </app-section-card>

      <app-section-card title="Initial scope" icon="o_center_focus_weak" collapsible info="The specific areas, processes, systems or activities the requirement covers." dense class="q-mb-md">
        <app-rich-text-field
          v-model="model.scope"
          placeholder="Describe what is in scope…"
          min-height="5rem"
        />
      </app-section-card>

      <app-section-card v-if="showLog" ref="logCard" title="Requirement Log" icon="o_history_edu" collapsible info="Detailed information about the requirement. This first entry becomes the requirement's description; later entries are added from the requirement's Requirement Log tab." dense class="q-mb-md">
        <app-rich-text-field
          v-model="model.description"
          label="First entry"
          required
          placeholder="Describe the requirement in detail…"
          min-height="6rem"
          :error="v$.description?.$error"
          :error-message="err(v$.description)"
          @blur="v$.description?.$touch()"
        />
      </app-section-card>

      <slot name="main" />
    </div>

    <!-- Side column: where it stands, who is on it, when it runs. -->
    <div class="col-12 col-lg-4">
      <app-section-card ref="workflowCard" title="Workflow" icon="o_flag" collapsible dense class="q-mb-md">
        <div class="row q-col-gutter-sm">
          <!-- On an existing requirement the status and the priority are saved the moment they are picked,
               through the same logged actions the list uses (the status with its optional note), so their
               change logs stay complete. The host does the saving. -->
          <app-select
            v-if="!isNew"
            :model-value="model.statusId"
            class="col-12 col-sm-6 col-lg-12"
            label="Status"
            required
            option-label="text"
            info="Saved as soon as you pick it, with an optional note, and kept in the status change log."
            :options="statuses.list.value"
            :clearable="false"
            :loading="loggedBusy === 'status'"
            :error="v$.statusId.$error"
            :error-message="err(v$.statusId)"
            @update:model-value="(value) => $emit('status-change', value)"
          >
            <template #option="scope">
              <q-item v-bind="scope.itemProps">
                <q-item-section><app-option-badge :option="toBadgeOption(scope.opt)" class="rff__badge" /></q-item-section>
              </q-item>
            </template>
          </app-select>
          <app-select
            :model-value="model.priorityId"
            class="col-12 col-sm-6 col-lg-12"
            label="Priority"
            option-label="text"
            :info="isNew ? '' : 'Saved as soon as you pick it, and kept in the priority change log.'"
            :options="priorities.list.value"
            :clearable="isNew"
            :loading="loggedBusy === 'priority'"
            @update:model-value="onPriority"
          >
            <template #option="scope">
              <q-item v-bind="scope.itemProps">
                <q-item-section><app-option-badge :option="toBadgeOption(scope.opt)" class="rff__badge" /></q-item-section>
              </q-item>
            </template>
          </app-select>
          <app-select v-model="model.approvalStatus" class="col-12 col-sm-6 col-lg-12" label="Approval Status" option-label="text" :options="approvalStatuses.list.value" />
          <app-select
            v-if="!isNew"
            v-model="model.approvedById"
            class="col-12 col-sm-6 col-lg-12"
            label="Approved By"
            option-label="text"
            :options="employees.list.value"
            :loading="loading.employees"
          />
        </div>
      </app-section-card>

      <app-section-card ref="peopleCard" title="People" icon="o_groups" collapsible default-collapsed dense class="q-mb-md">
        <div class="row q-col-gutter-sm">
          <app-select
            v-model="model.requirementOwnerId"
            class="col-12 col-sm-6 col-lg-12"
            label="Requirement Owner"
            required
            option-label="text"
            :options="employees.list.value"
            :loading="loading.employees"
            :error="v$.requirementOwnerId.$error"
            :error-message="err(v$.requirementOwnerId)"
            @blur="v$.requirementOwnerId.$touch"
          />
          <app-select
            v-model="model.customerOwnerId"
            class="col-12 col-sm-6 col-lg-12"
            label="Customer Owner"
            required
            option-label="text"
            info="Contacts of the selected project's customer."
            :options="customers.list.value"
            :loading="loading.customers"
            :disable="!model.projectId"
            :error="v$.customerOwnerId.$error"
            :error-message="err(v$.customerOwnerId)"
            @blur="v$.customerOwnerId.$touch"
          />
          <app-select
            v-model="model.identifiedUserType"
            class="col-12 col-sm-6 col-lg-12"
            label="Identified By"
            option-label="text"
            info="Whether the requirement was raised by one of your employees or by the customer."
            :options="identifiers.list.value"
          />
          <app-select
            v-if="identifierText === 'Employee'"
            v-model="model.identifiedEmployeeId"
            class="col-12 col-sm-6 col-lg-12"
            label="Employee Name"
            option-label="text"
            :options="employees.list.value"
            :loading="loading.employees"
          />
          <app-select
            v-else-if="identifierText === 'Customer'"
            v-model="model.identifiedCustomerId"
            class="col-12 col-sm-6 col-lg-12"
            label="Customer Contact Name"
            option-label="text"
            info="Contacts of the selected project's customer."
            :options="customers.list.value"
            :loading="loading.customers"
            :disable="!model.projectId"
          />
          <app-select
            v-model="model.requirementEnteredBy"
            class="col-12 col-sm-6 col-lg-12"
            label="Entered By"
            option-label="text"
            :options="employees.list.value"
            :loading="loading.employees"
          />
        </div>
      </app-section-card>

      <app-section-card ref="timelineCard" title="Timeline" icon="o_event" collapsible default-collapsed dense class="q-mb-md">
        <div class="row q-col-gutter-sm">
          <app-date-field
            v-model="model.identifiedDateStr"
            class="col-12 col-sm-4 col-lg-12"
            label="Identified"
            required
            model-format="MM/DD/YYYY"
            :error="v$.identifiedDateStr.$error"
            :error-message="err(v$.identifiedDateStr)"
            @blur="v$.identifiedDateStr.$touch"
          />
          <!-- A new requirement is simply given its dates. -->
          <template v-if="isNew">
            <app-date-field v-model="model.plannedStartDateStr" class="col-6 col-sm-4 col-lg-6" label="Planned Start" model-format="MM/DD/YYYY" :max-date="model.plannedEndDateStr" />
            <app-date-field v-model="model.plannedEndDateStr" class="col-6 col-sm-4 col-lg-6" label="Planned End" model-format="MM/DD/YYYY" :min-date="model.plannedStartDateStr" />
            <app-date-field v-model="model.actualStartDateStr" class="col-6 col-sm-4 col-lg-6" label="Actual Start" model-format="MM/DD/YYYY" :max-date="model.actualEndDateStr" />
            <app-date-field v-model="model.actualEndDateStr" class="col-6 col-sm-4 col-lg-6" label="Actual End" model-format="MM/DD/YYYY" :min-date="model.actualStartDateStr" />
          </template>
          <!-- On an existing one each planned / actual date is changed on its own, with an optional note,
               and keeps a change log (as on the list); the host asks for the note and saves. -->
          <template v-else>
            <div v-for="def in LOGGED_DATES" :key="def.field" class="col-6 col-sm-4 col-lg-6">
              <app-field-label :label="def.label" />
              <div class="rff__logged">
                <span :class="['rff__logged-value', { 'text-grey-6': !model[`${def.field}Str`] }]">{{ model[`${def.field}Str`] || "Not set" }}</span>
                <q-btn flat round dense size="sm" color="primary" icon="o_calendar_month" :loading="loggedBusy === def.field" :aria-label="`Change ${def.log}`">
                  <q-popup-proxy :ref="(el) => setDatePopup(def.field, el)" transition-show="scale" transition-hide="scale">
                    <q-date
                      :model-value="model[`${def.field}Str`] || null"
                      mask="MM/DD/YYYY"
                      :options="def.after ? notBefore(model[`${def.after}Str`]) : undefined"
                      @update:model-value="(value) => pickDate(def, value)"
                    />
                  </q-popup-proxy>
                  <q-tooltip>Change {{ def.log }}</q-tooltip>
                </q-btn>
                <q-btn v-if="model[`${def.field}Str`]" flat round dense size="sm" color="grey-7" icon="o_history" :aria-label="`${def.log} Change Log`" @click="$emit('date-log', def.log)">
                  <q-tooltip>{{ def.log }} Change Log</q-tooltip>
                </q-btn>
              </div>
            </div>
            <div class="col-12 text-caption text-grey-7 rff__logged-note">
              Planned and actual dates save as soon as you pick them, each with an optional note, and keep a change log.
            </div>
            <app-date-field v-model="model.closeDateStr" class="col-12 col-sm-4 col-lg-12" label="Closed" model-format="MM/DD/YYYY" :error="v$.closeDateStr.$error" :error-message="err(v$.closeDateStr)" />
          </template>
        </div>
      </app-section-card>

      <slot name="aside" />
    </div>
  </div>
</template>

<script setup>
// The requirement field set, defined once and shared by the create and edit pages, laid out in the same
// sections the record's Overview uses: About and Initial scope in the main column; Workflow, People and
// Timeline down the side. Each section is an accordion; People and Timeline start closed. It owns its lookups, the project → module / customer cascade, the
// create-time defaults and the validation rules; the host calls validate() before saving.
//
// On an existing requirement the status, the priority and the four planned / actual dates are not part
// of the form's save: each is changed through its own logged action (the host listens for
// status-change / priority-change / date-change and saves), exactly as the current list does.
import { computed, reactive, ref, watch, onMounted } from "vue";
import useVuelidate from "@vuelidate/core";
import { required, requiredIf, helpers, maxLength } from "@vuelidate/validators";
import { isDate } from "validators/zw_validators.js";
import AppSectionCard from "components/common/AppSectionCard.vue";
import AppSelect from "components/common/AppSelect.vue";
import AppTextField from "components/common/AppTextField.vue";
import AppDateField from "components/common/AppDateField.vue";
import AppFieldLabel from "components/common/AppFieldLabel.vue";
import AppRichTextField from "components/common/AppRichTextField.vue";
import AppOptionBadge from "components/common/AppOptionBadge.vue";
import { hasRichTextContent } from "src/utils/richText";
import { labelFor, toBadgeOption } from "modules/requirement-v2/utils/options";
import { LOGGED_DATES, notBefore } from "modules/requirement-v2/utils/changeNote";

import projectModule from "src/modules/project/utils/dropdowns.js";
import projectModuleOfProjectModule from "src/modules/project-modules/utils/dropdowns.js";
import projectTaskModule from "src/modules/project-tasks/utils/dropdowns.js";
import employeeModule from "src/modules/employee/utils/dropdowns.js";
import requirementModule from "src/modules/requirement-v2/utils/dropdowns.js";

const model = defineModel({ type: Object, required: true });

const props = defineProps({
  // A new requirement gets the "New" status, the "Unapproved" approval state and "Employee" identifier.
  isNew: { type: Boolean, default: false },
  // The create page captures the first log entry here; later entries live on the record's Log tab.
  showLog: { type: Boolean, default: false },
  // Which logged field the host is saving right now ("status", "priority", "plannedStartDate", …).
  loggedBusy: { type: String, default: "" }
});

// Editing only. "status-change" / "priority-change": the id picked. "date-change": { field, log, value }
// with the day as "MM/DD/YYYY". "date-log": the name of the date whose change log is asked for.
const emit = defineEmits(["status-change", "priority-change", "date-change", "date-log"]);

const onPriority = (value) => {
  if (props.isNew) model.value.priorityId = value;
  else emit("priority-change", value);
};

const datePopups = {};
const setDatePopup = (field, el) => { datePopups[field] = el; };

const pickDate = (def, value) => {
  datePopups[def.field]?.hide?.();
  // q-date sends null when the selected day is clicked again; an unchanged day is not a change.
  if (!value || value === model.value[`${def.field}Str`]) return;
  emit("date-change", { field: def.field, log: def.log, value });
};

// ---- Lookups ----
const { projectNameDropdownSingleSelect: projects, customerContactByProjectIdDropdownSingleSelect: customers } = projectModule();
const { projectModulesByProjectIdForDropdownSingleSelect: modules } = projectModuleOfProjectModule();
const { activeEmployeesDropdownSingleSelect: employees } = employeeModule();
const { areaForDropdownSingleSelect: areas, workspaceForDropdownSingleSelect: workspaces } = projectTaskModule();
const {
  requirementIdentifiedUserTypeDropdownSingleSelect: identifiers,
  requirementStatusDropdownSingleSelect: statuses,
  requirementPriorityDropdownSingleSelect: priorities,
  requirementApprovalStatusDropdownSingleSelect: approvalStatuses,
  requirementTypeDropdownSingleSelect: types
} = requirementModule();

const loading = reactive({ projects: false, modules: false, customers: false, employees: false });

// Silent on purpose: a list that cannot load leaves an empty picker, and the interceptor has already
// reported the failure.
const quietly = (promise) => promise.catch((error) => console.error(error));

const identifierText = computed(() => labelFor(identifiers.list.value, model.value.identifiedUserType));

// ---- Data Integration: the source and target labels of the requirement's data mapping ----
const typeText = computed(() => labelFor(types.list.value, model.value.requirementTypeId));
const isDataIntegration = computed(() => typeText.value === "Data Integration");

// The labels belong to the Data Integration type: moving to another type drops them.
watch(typeText, (next, previous) => {
  if (previous === "Data Integration" && next !== "Data Integration") {
    model.value.sourceLabel = null;
    model.value.targetLabel = null;
  }
});

// ---- Project cascade: the module list and the customer contacts belong to the chosen project ----
// The Requirement Group field is not in use and is not shown; a group saved on an older requirement is
// kept until its project changes.
// `fillCustomerOwner`: the project was just picked in the form, so an empty Customer Owner takes the
// project's own contact (or the customer's only contact). Seeding a saved record never fills it.
const syncProject = async (projectId, { fillCustomerOwner = false } = {}) => {
  const id = Array.isArray(projectId) ? projectId[0] : projectId;
  if (!id) {
    modules.list.value = [];
    customers.list.value = [];
    return;
  }
  const selected = projects.list.value.find((p) => p.value === id);
  if (selected) model.value.projectStatus = selected.data;

  loading.modules = true;
  loading.customers = true;
  const [, contacts] = await Promise.allSettled([
    quietly(modules.load(false, false, id)).finally(() => { loading.modules = false; }),
    quietly(customers.load(id)).finally(() => { loading.customers = false; })
  ]);

  // The answer may arrive after the project was changed again; only the current project's contact counts.
  if (!fillCustomerOwner || model.value.projectId !== projectId || model.value.customerOwnerId) return;
  const list = Array.isArray(contacts.value) ? contacts.value : [];
  const owner = list.find((c) => c.data === true) || (list.length === 1 ? list[0] : null);
  if (owner?.value) model.value.customerOwnerId = owner.value;
};

watch(() => model.value.projectId, (next, previous) => {
  // Only a change made in the form clears the dependents; seeding the form with a saved record must not.
  if (previous && next !== previous) {
    model.value.projectModuleId = null;
    model.value.requirementGroupId = null;
    model.value.identifiedCustomerId = null;
    model.value.customerOwnerId = null;
  }
  syncProject(next, { fillCustomerOwner: props.isNew || Boolean(previous) });
});

// ---- Validation ----
const dateRule = helpers.withMessage("Enter a valid date (MM/DD/YYYY)", isDate);

const rules = computed(() => ({
  projectId: { required: helpers.withMessage("Project is required", required) },
  projectModuleId: { required: helpers.withMessage("Project module is required", required) },
  statusId: { required: helpers.withMessage("Requirement status is required", required) },
  title: {
    required: helpers.withMessage("Requirement is required", required),
    maxLength: helpers.withMessage("Keep the requirement under 200 characters", maxLength(200))
  },
  identifiedDateStr: {
    required: helpers.withMessage("Identified date is required", required),
    isDate: dateRule
  },
  requirementOwnerId: { required: helpers.withMessage("Requirement owner is required", required) },
  customerOwnerId: { required: helpers.withMessage("Customer owner is required", required) },
  sourceLabel: { required: helpers.withMessage("Source label is required", requiredIf(() => isDataIntegration.value)) },
  targetLabel: { required: helpers.withMessage("Target label is required", requiredIf(() => isDataIntegration.value)) },
  closeDateStr: { isDate: dateRule },
  ...(props.showLog
    ? { description: { required: helpers.withMessage("The first Requirement Log entry is required", (value) => hasRichTextContent(value)) } }
    : {})
}));

const v$ = useVuelidate(rules, model, { $lazy: true, $autoDirty: true });

const err = (field) => field?.$errors?.[0]?.$message || "";

// ---- Sections ----
// Every section is an accordion; People and Timeline start closed. A section holding a field that has
// just failed validation opens itself, so a message is never hidden behind a closed head.
const aboutCard = ref(null);
const logCard = ref(null);
const workflowCard = ref(null);
const peopleCard = ref(null);
const timelineCard = ref(null);

const SECTION_FIELDS = [
  [aboutCard, ["title", "projectId", "projectModuleId", "sourceLabel", "targetLabel"]],
  [logCard, ["description"]],
  [workflowCard, ["statusId"]],
  [peopleCard, ["requirementOwnerId", "customerOwnerId"]],
  [timelineCard, ["identifiedDateStr", "closeDateStr"]]
];

const openFailedSections = () => {
  SECTION_FIELDS.forEach(([card, keys]) => {
    if (keys.some((key) => v$.value[key]?.$error)) card.value?.expand();
  });
};

watch(() => v$.value.$errors.length, openFailedSections);

// ---- Load ----
onMounted(async () => {
  loading.projects = true;
  loading.employees = true;
  await Promise.allSettled([
    quietly(projects.load()).finally(() => { loading.projects = false; }),
    quietly(employees.load()).finally(() => { loading.employees = false; }),
    quietly(areas.load("Area")),
    quietly(workspaces.load("Workspace")),
    quietly(types.load("Requirement Type")),
    quietly(statuses.load("Requirement Status")),
    quietly(priorities.load("Requirement Priority")),
    quietly(identifiers.load("Requirement Identifier")),
    quietly(approvalStatuses.load("Approval Status"))
  ]);

  if (props.isNew) {
    const employeeType = identifiers.getValueByLabel("Employee");
    if (employeeType && !model.value.identifiedUserType) model.value.identifiedUserType = employeeType;
    const newStatus = statuses.getValueByLabel("New");
    if (newStatus && !model.value.statusId) model.value.statusId = newStatus;
    const unapproved = approvalStatuses.getValueByLabel("Unapproved");
    if (unapproved && !model.value.approvalStatus) model.value.approvalStatus = unapproved;
  }

  // A preset project (from the list's filter, or the record being edited) needs its dependents loaded
  // without the watcher's clearing. On a new requirement the preset project brings its customer owner too.
  syncProject(model.value.projectId, { fillCustomerOwner: props.isNew });
});

// Only the named fields are checked (and marked), so an auto-saving edit of one field is not held up by
// another that the record has always had empty. Choosing the Data Integration type brings its two labels
// into the check: that type is not saved without them.
const validateKeys = (keys) => {
  let ok = true;
  const checked = keys.includes("requirementTypeId") ? [...new Set([...keys, "sourceLabel", "targetLabel"])] : keys;
  for (const key of checked) {
    const field = v$.value[key];
    if (!field) continue;
    field.$touch();
    if (field.$invalid) ok = false;
  }
  if (!ok) openFailedSections();
  return ok;
};

// A failed check opens the sections it failed in, even one the reader closed again after the last try.
const validate = async () => {
  const ok = await v$.value.$validate();
  if (!ok) openFailedSections();
  return ok;
};

defineExpose({ validate, validateKeys });
</script>

<style scoped>
.rff__badge {
  cursor: pointer;
}
/* A logged date reads like the fields beside it, but it is a value with a button, not an input. */
.rff__logged {
  display: flex;
  align-items: center;
  gap: 2px;
  min-height: 40px;
  padding: 0 4px 0 12px;
  border: 1px solid rgba(0, 0, 0, 0.24);
  border-radius: 4px;
  background: #fafbfc;
}
.rff__logged-value {
  flex: 1 1 auto;
  min-width: 0;
  font-size: 14px;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.rff__logged-note {
  margin-top: -2px;
}
/* Phones: 16px inputs stop iOS zooming on focus. */
@media (max-width: 599px) {
  .rff :deep(.q-field__native),
  .rff :deep(.q-field__input) {
    font-size: 16px;
  }
}
</style>
