<template>
  <app-filter-drawer v-model="open" :chips="filters.chips.value" :show-chips="showChips" @remove="filters.clear" @clear="filters.clearAll">
    <div class="section-subhead q-mt-none">Where</div>
    <app-select
      v-model="search.projectIds"
      label="Project"
      multiple
      select-all
      option-label="text"
      :options="lookups.projects.list.value"
    />
    <app-select
      v-model="search.projectModuleIds"
      label="Project Module"
      multiple
      select-all
      option-label="text"
      info="Modules of the selected projects."
      :options="lookups.modules.list.value"
      :disable="!search.projectIds?.length"
    />

    <div class="section-subhead">What</div>
    <app-text-field
      v-model="search.requirementNumber"
      label="Requirement No."
      placeholder="e.g. 128"
      mask="##########"
      clearable
    />
    <app-text-field
      v-model="search.name"
      label="Requirement"
      placeholder="Title contains…"
      clearable
    />
    <app-select v-model="search.statusIds" label="Status" multiple select-all option-label="text" :options="lookups.statuses.list.value">
      <template #option="scope">
        <q-item v-bind="scope.itemProps">
          <q-item-section side><q-checkbox :model-value="scope.selected" dense @update:model-value="scope.toggleOption(scope.opt)" /></q-item-section>
          <q-item-section><app-option-badge :option="toBadgeOption(scope.opt)" class="rfd__badge" /></q-item-section>
        </q-item>
      </template>
    </app-select>
    <app-select v-model="search.priorityIds" label="Priority" multiple select-all option-label="text" :options="lookups.priorities.list.value">
      <template #option="scope">
        <q-item v-bind="scope.itemProps">
          <q-item-section side><q-checkbox :model-value="scope.selected" dense @update:model-value="scope.toggleOption(scope.opt)" /></q-item-section>
          <q-item-section><app-option-badge :option="toBadgeOption(scope.opt)" class="rfd__badge" /></q-item-section>
        </q-item>
      </template>
    </app-select>
    <app-select v-model="search.approvalStatusIds" label="Approval Status" multiple select-all option-label="text" :options="lookups.approvalStatuses.list.value" />
    <app-select v-model="search.requirementTypeIds" label="Type" multiple select-all option-label="text" :options="lookups.types.list.value" />
    <div class="row q-col-gutter-sm">
      <app-select v-model="search.areaIds" class="col-6" label="Area" multiple select-all option-label="text" :options="lookups.areas.list.value" />
      <app-select v-model="search.workspaceIds" class="col-6" label="Workspace" multiple select-all option-label="text" :options="lookups.workspaces.list.value" />
    </div>
    <app-select
      v-model="search.requirementTagIds"
      label="Tags"
      multiple
      select-all
      option-label="text"
      :options="lookups.requirementTags.list.value"
    >
      <template #option="scope">
        <q-item v-bind="scope.itemProps">
          <q-item-section side>
            <q-checkbox :model-value="scope.selected" dense @update:model-value="scope.toggleOption(scope.opt)" />
          </q-item-section>
          <q-item-section>
            <span class="rfd__tag" :style="{ backgroundColor: scope.opt.bgColor || '#e0e0e0', color: scope.opt.color || '#191919' }">
              {{ scope.opt.text }}
            </span>
          </q-item-section>
        </q-item>
      </template>
    </app-select>

    <div class="section-subhead">Who</div>
    <app-select v-model="search.requirementOwnerIds" label="Requirement Owner" multiple select-all option-label="text" :options="lookups.employees.list.value" />
    <app-select v-model="search.customerOwnerIds" label="Customer Owner" multiple select-all option-label="text" :options="lookups.customers.list.value" />
    <app-select
      v-model="search.identifiedUserTypeId"
      label="Identifier"
      option-label="text"
      info="Whether the requirement was raised by an employee or by the customer."
      :options="lookups.identifiers.list.value"
    />
    <app-select
      v-if="filters.identifierText.value === 'Customer'"
      v-model="search.identifiedCustomerIds"
      label="Identified By (Customer)"
      multiple
      select-all
      option-label="text"
      :options="lookups.customers.list.value"
    />
    <app-select
      v-if="filters.identifierText.value === 'Employee'"
      v-model="search.identifiedEmployeeIds"
      label="Identified By (Employee)"
      multiple
      select-all
      option-label="text"
      :options="lookups.employees.list.value"
    />

    <div class="section-subhead">When</div>
    <div class="row q-col-gutter-sm">
      <app-date-field v-model="search.plannedEndFromDate" class="col-6" label="Due From" model-format="MM/DD/YYYY" :max-date="search.plannedEndToDate || ''" />
      <app-date-field v-model="search.plannedEndToDate" class="col-6" label="Due To" model-format="MM/DD/YYYY" :min-date="search.plannedEndFromDate || ''" />
    </div>
    <div v-if="invalid(search.plannedEndFromDate, search.plannedEndToDate)" class="text-caption text-negative">"Due From" is after "Due To" — nothing can match both.</div>
    <div class="row q-col-gutter-sm">
      <app-date-field v-model="search.fromDate" class="col-6" label="Created From" model-format="MM/DD/YYYY" :max-date="search.toDate || ''" />
      <app-date-field v-model="search.toDate" class="col-6" label="Created To" model-format="MM/DD/YYYY" :min-date="search.fromDate || ''" />
    </div>
    <div v-if="invalid(search.fromDate, search.toDate)" class="text-caption text-negative">"Created From" is after "Created To" — nothing can match both.</div>
    <div class="row q-col-gutter-sm">
      <app-date-field v-model="search.updatedFromDate" class="col-6" label="Updated From" model-format="MM/DD/YYYY" :max-date="search.updatedToDate || ''" />
      <app-date-field v-model="search.updatedToDate" class="col-6" label="Updated To" model-format="MM/DD/YYYY" :min-date="search.updatedFromDate || ''" />
    </div>
    <div v-if="invalid(search.updatedFromDate, search.updatedToDate)" class="text-caption text-negative">"Updated From" is after "Updated To" — nothing can match both.</div>

    <div class="section-subhead">Flags</div>
    <div class="rfd__toggles">
      <q-toggle :model-value="search.overdue === true" label="Overdue only" color="negative" dense @update:model-value="search.overdue = $event ? true : null" />
      <q-toggle :model-value="search.dueSoon === true" label="Due within 7 days" color="warning" dense @update:model-value="search.dueSoon = $event ? true : null" />
      <q-toggle :model-value="search.notApproved === true" label="Not approved" color="info" dense @update:model-value="search.notApproved = $event ? true : null" />
      <q-toggle :model-value="search.myRequirements === true" label="My requirements" color="primary" dense @update:model-value="search.myRequirements = $event ? true : null" />
      <q-toggle :model-value="search.pinnedOnly === true" label="Pinned by me" color="primary" dense @update:model-value="search.pinnedOnly = $event ? true : null" />
    </div>
    <div>
      <app-field-label label="Converted into tasks" />
      <q-btn-toggle
        :model-value="search.hasTasks === null || search.hasTasks === undefined ? 'any' : (search.hasTasks ? 'yes' : 'no')"
        no-caps
        unelevated
        dense
        toggle-color="primary"
        color="grey-3"
        text-color="grey-8"
        :options="[{ label: 'Any', value: 'any' }, { label: 'Yes', value: 'yes' }, { label: 'Not yet', value: 'no' }]"
        @update:model-value="search.hasTasks = $event === 'any' ? null : $event === 'yes'"
      />
    </div>
  </app-filter-drawer>
</template>

<script setup>
// The requirement list's filter drawer: one control per server filter, bound straight to the search
// model, grouped Where / What / Who / When / Flags, with the lookups loaded the first time it opens.
import { watch } from "vue";
import AppFilterDrawer from "components/common/AppFilterDrawer.vue";
import AppSelect from "components/common/AppSelect.vue";
import AppTextField from "components/common/AppTextField.vue";
import AppDateField from "components/common/AppDateField.vue";
import AppFieldLabel from "components/common/AppFieldLabel.vue";
import AppOptionBadge from "components/common/AppOptionBadge.vue";
import { toIsoDate } from "composables/useDateFormat";
import { toBadgeOption } from "modules/requirement-v2/utils/options";

const open = defineModel({ type: Boolean, default: false });
const search = defineModel("search", { type: Object, required: true });

const props = defineProps({
  // useRequirementLookups() and useRequirementFilters() from the page.
  lookups: { type: Object, required: true },
  filters: { type: Object, required: true },
  // The row of active-filter chips above the list (the page hides it while its header is folded).
  showChips: { type: Boolean, default: true }
});

const invalid = (fromText, toText) => {
  const from = toIsoDate(fromText);
  const to = toIsoDate(toText);
  return !!from && !!to && from > to;
};

watch(open, (isOpen) => {
  if (isOpen) props.lookups.ensureFilterLookups();
});
</script>

<style scoped>
/* Compact: less space between the fields and above each group's heading. */
:deep(.app-filter-drawer__fields) {
  gap: 6px;
  padding: 10px 12px;
}
:deep(.app-filter-drawer__fields .section-subhead) {
  margin-top: 6px;
  margin-bottom: 0;
}
.rfd__tag {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 6px;
  font-size: 12.5px;
  white-space: normal;
  overflow-wrap: break-word;
}
.rfd__badge {
  cursor: pointer;
}
.rfd__toggles {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
</style>
