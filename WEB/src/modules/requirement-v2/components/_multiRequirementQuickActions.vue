<template>
  <q-dialog ref="dialogRef" persistent @hide="onDialogHide">
    <q-card class="mrqa">
      <q-card-section class="row items-center no-wrap q-py-sm">
        <q-icon :name="icon" color="primary" size="22px" class="q-mr-sm" />
        <div class="col">
          <div class="text-h6 text-primary">{{ heading }}</div>
          <div class="text-caption text-grey-7">{{ requirementIds.length }} requirement{{ requirementIds.length === 1 ? "" : "s" }} selected</div>
        </div>
        <q-btn flat round dense color="grey-8" icon="o_close" aria-label="Close" @click="onDialogCancel" />
      </q-card-section>
      <q-separator />

      <q-form greedy @submit.prevent.stop="onSubmit">
        <q-card-section>
          <!-- Status / priority: one value for every selected requirement. -->
          <app-select
            v-if="!isTags"
            v-model="value"
            :label="fieldLabel"
            required
            :options="options"
            :loading="loadingOptions"
            :clearable="false"
            :error="showError"
            :error-message="`${fieldLabel} is required`"
          >
            <template #option="scope">
              <q-item v-bind="scope.itemProps">
                <q-item-section>
                  <app-option-badge :option="scope.opt" class="mrqa__badge" />
                </q-item-section>
              </q-item>
            </template>
          </app-select>

          <!-- Tags: added to whatever each requirement already carries; typing a new name creates the tag. -->
          <template v-else>
            <app-field-label label="Tags to add" required />
            <q-select
              v-model="tagNames"
              outlined
              dense
              multiple
              use-input
              use-chips
              hide-dropdown-icon
              new-value-mode="add-unique"
              input-debounce="0"
              :options="tagOptions"
              :loading="loadingOptions"
              placeholder="Type a tag and press Enter"
              aria-label="Tags to add"
              :error="showError"
              error-message="Pick or type at least one tag"
              @filter="filterTags"
            >
              <template #option="scope">
                <q-item v-bind="scope.itemProps">
                  <q-item-section>
                    <span class="mrqa__tag" :style="{ backgroundColor: scope.opt.bgColor || '#e0e0e0', color: scope.opt.color || '#191919' }">{{ scope.opt.label }}</span>
                  </q-item-section>
                </q-item>
              </template>
              <template #selected-item="scope">
                <q-chip removable dense :tabindex="scope.tabindex" class="mrqa__chip" @remove="scope.removeAtIndex(scope.index)">
                  {{ typeof scope.opt === "string" ? scope.opt : scope.opt.label }}
                </q-chip>
              </template>
            </q-select>
            <div class="text-caption text-grey-7 q-mt-xs">Existing tags on the selected requirements are kept.</div>
          </template>
        </q-card-section>
        <q-separator />
        <q-card-actions align="right" class="bg-grey-1">
          <q-btn flat no-caps color="grey-8" label="Cancel" @click="onDialogCancel" />
          <q-btn unelevated no-caps color="primary" :label="applyLabel" type="submit" :loading="processing" :disable="processing" />
        </q-card-actions>
      </q-form>
    </q-card>
  </q-dialog>
</template>

<script setup>
// Bulk status / priority / tag change for the requirements ticked in the list — a short, focused
// decision, so a centred dialog rather than a panel.
import { ref, computed, onMounted } from "vue";
import { useDialogPluginComponent } from "quasar";
import { useNotify } from "composables/useNotify";
import AppSelect from "components/common/AppSelect.vue";
import AppFieldLabel from "components/common/AppFieldLabel.vue";
import AppOptionBadge from "components/common/AppOptionBadge.vue";
import commonService from "services/common.service";
import projectTaskService from "modules/project-tasks/projectTasks.service";
import requirementService from "modules/requirement-v2/requirementV2.service";
import { toBadgeOptions } from "modules/requirement-v2/utils/options";

const props = defineProps({
  requirementIds: { type: Array, required: true },
  // "Status", "Priority" or "Tags".
  selectedField: { type: String, default: "Status" }
});

defineEmits([...useDialogPluginComponent.emits]);
const { dialogRef, onDialogHide, onDialogOK, onDialogCancel } = useDialogPluginComponent();

const notify = useNotify();

const isStatus = computed(() => props.selectedField === "Status");
const isTags = computed(() => props.selectedField === "Tags");
const fieldLabel = computed(() => (isStatus.value ? "Status" : "Priority"));
const heading = computed(() => (isTags.value ? "Add tags" : (isStatus.value ? "Change status" : "Change priority")));
const applyLabel = computed(() => (isTags.value ? "Add tags" : (isStatus.value ? "Set status" : "Set priority")));
const icon = computed(() => (isTags.value ? "o_label" : (isStatus.value ? "o_flag" : "o_priority_high")));

const value = ref(null);
const options = ref([]);
const tagNames = ref([]);
const allTags = ref([]);
const tagOptions = ref([]);
const loadingOptions = ref(false);
const processing = ref(false);
const attempted = ref(false);

const showError = computed(() => attempted.value && (isTags.value ? !tagNames.value.length : !value.value));

const filterTags = (needle, update) => {
  update(() => {
    const n = String(needle || "").toLowerCase();
    tagOptions.value = n ? allTags.value.filter((t) => t.label.toLowerCase().includes(n)) : allTags.value;
  });
};

const plural = computed(() => (props.requirementIds.length === 1 ? "" : "s"));

// The tags endpoint replaces a requirement's tags with the list it is sent. To add and keep, each
// requirement is sent its own tags plus the new ones: the selected requirements are read once for the
// tags they carry, and those that end up with the same set share one request. A requirement the list no
// longer returns (deleted, or out of the reader's sight) is left alone. Returns how many were tagged.
const addTags = async (names) => {
  const ids = props.requirementIds;
  const resp = await requirementService.getAllRequirement({ requirementIds: ids, page: 1, pageSize: ids.length });
  const groups = new Map();
  for (const requirement of resp?.data || []) {
    const current = (requirement.requirementTags || []).map((t) => t.tags?.name).filter(Boolean);
    const have = new Set(current.map((name) => name.toLowerCase()));
    const merged = [...current, ...names.filter((name) => !have.has(name.toLowerCase()))];
    const key = JSON.stringify([...merged].sort());
    if (!groups.has(key)) groups.set(key, { requirementIds: [], tagsNameList: merged });
    groups.get(key).requirementIds.push(requirement.id);
  }
  let tagged = 0;
  for (const group of groups.values()) {
    await requirementService.saveTags({ requirementIds: group.requirementIds, flag: null, tagsNameList: group.tagsNameList });
    tagged += group.requirementIds.length;
  }
  return tagged;
};

const onSubmit = async () => {
  attempted.value = true;
  if (showError.value || processing.value) return;

  processing.value = true;
  try {
    if (isTags.value) {
      const typed = tagNames.value.map((t) => (typeof t === "string" ? t : t.label)).map((s) => s.trim()).filter(Boolean);
      // One of each, whatever the capitals.
      const names = typed.filter((name, index) => typed.findIndex((other) => other.toLowerCase() === name.toLowerCase()) === index);
      const tagged = await addTags(names);
      notify.success(`${names.length} tag${names.length === 1 ? "" : "s"} added to ${tagged} requirement${tagged === 1 ? "" : "s"}.`);
    } else if (isStatus.value) {
      await requirementService.updateRequirementStatus({ requirementIds: props.requirementIds, statusId: value.value });
      notify.success(`Status updated for ${props.requirementIds.length} requirement${plural.value}.`);
    } else {
      await requirementService.updateRequirementPriority({ requirementIds: props.requirementIds, priorityId: value.value });
      notify.success(`Priority updated for ${props.requirementIds.length} requirement${plural.value}.`);
    }
    onDialogOK();
  } catch (error) {
    console.error("Error applying the bulk change", error);
  } finally {
    processing.value = false;
  }
};

onMounted(async () => {
  loadingOptions.value = true;
  try {
    if (isTags.value) {
      const resp = await projectTaskService.getAllTagsListForDropdown();
      allTags.value = (resp || []).filter((t) => t && t.name).map((t) => ({ label: t.name, value: t.name, color: t.color, bgColor: t.bgColor }));
      tagOptions.value = allTags.value;
    } else {
      const resp = await commonService.getDropDown(isStatus.value ? "Requirement Status" : "Requirement Priority");
      options.value = toBadgeOptions((resp || []).map((item) => ({
        text: item.dropdownValue,
        value: item.id,
        color: item.color,
        bgColor: item.bgColor
      })));
    }
  } catch (error) {
    console.error("Error loading options", error);
  } finally {
    loadingOptions.value = false;
  }
});
</script>

<style scoped>
.mrqa {
  width: 460px;
  max-width: 92vw;
  border-radius: 12px;
}
.mrqa__badge {
  cursor: pointer;
}
.mrqa__tag {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 6px;
  font-size: 12.5px;
}
.mrqa__chip {
  background: #e3f1f8;
  color: var(--q-primary);
}
</style>
