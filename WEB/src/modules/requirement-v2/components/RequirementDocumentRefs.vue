<template>
  <div class="rdr">
    <div class="row items-center no-wrap q-mb-sm">
      <template v-if="!hideTitle">
        <q-icon name="o_link" color="primary" size="18px" class="q-mr-xs" />
        <div class="text-subtitle2 text-primary">Document References</div>
        <app-info-tip text="Links to the documents that describe the requirement — a specification, a mock-up, a signed form — each with a name and a note." />
      </template>
      <q-space />
      <q-btn outline dense no-caps color="primary" icon="o_add" label="Add reference" @click="add" />
    </div>

    <div v-if="!rows.length" class="rdr__empty text-grey-6">
      No document references yet.
    </div>

    <q-card
      v-for="(row, index) in rows"
      :key="row.id"
      flat
      bordered
      :class="['rdr__row q-mb-sm', { 'rdr__row--deleted': row.deleted }]"
    >
      <q-card-section class="q-pa-sm">
        <div class="row items-center no-wrap q-mb-xs">
          <div class="text-caption text-weight-medium text-grey-8 col ellipsis">
            {{ row.fileName || `Reference ${index + 1}` }}
          </div>
          <q-badge v-if="row.flag === 'New'" color="blue-1" text-color="primary" class="q-mr-sm">New</q-badge>
          <q-badge v-if="row.deleted" color="grey-5" class="q-mr-sm">Removed when you save</q-badge>
          <q-btn
            v-if="isUrl(row.filePath)"
            flat round dense color="primary" icon="o_open_in_new" type="a" :href="row.filePath" target="_blank" rel="noopener"
          >
            <q-tooltip>Open document</q-tooltip>
          </q-btn>
          <q-btn v-if="!row.deleted" flat round dense color="negative" icon="o_delete" aria-label="Remove reference" @click="remove(row)">
            <q-tooltip>Remove</q-tooltip>
          </q-btn>
          <q-btn v-else flat round dense color="primary" icon="o_restore" aria-label="Undo removal" @click="undo(row)">
            <q-tooltip>Undo</q-tooltip>
          </q-btn>
        </div>

        <div v-if="!row.deleted" class="row q-col-gutter-sm">
          <app-text-field
            v-model="row.filePath"
            class="col-12 col-sm-6"
            label="File Path"
            required
            placeholder="https://…"
            :error="!!errors[row.id]?.filePath"
            :error-message="errors[row.id]?.filePath"
          />
          <app-text-field
            v-model="row.fileName"
            class="col-12 col-sm-6"
            label="File Name"
            required
            placeholder="e.g. Functional specification v2"
            :error="!!errors[row.id]?.fileName"
            :error-message="errors[row.id]?.fileName"
          />
          <app-text-field v-model="row.note" class="col-12" label="Note" type="textarea" autogrow maxlength="500" />
        </div>
        <div v-else class="text-caption text-grey-6 text-strike ellipsis">
          {{ row.fileName }} — {{ row.filePath }}
        </div>
      </q-card-section>
    </q-card>
  </div>
</template>

<script setup>
// The repeatable "document reference" rows of a requirement. Rows carry the flag the API expects:
// "New" (never saved; removing one just drops it), "Edit" (loaded from the record) and "Delete" (a loaded
// row the reader removed — kept on screen struck through, with Undo, until the form is saved).
import { reactive, watch } from "vue";
import { uid } from "quasar";
import AppTextField from "components/common/AppTextField.vue";
import AppInfoTip from "components/common/AppInfoTip.vue";

const rows = defineModel({ type: Array, default: () => [] });

defineProps({
  // The host already shows a "Document references" heading (a section card): only the Add button stays.
  hideTitle: { type: Boolean, default: false }
});

const errors = reactive({});

const isUrl = (value) => /^https?:\/\//i.test(String(value || "").trim());

const add = () => {
  rows.value.unshift({ id: uid(), fileName: "", filePath: "", note: "", flag: "New" });
};

const remove = (row) => {
  if (row.flag === "New") {
    rows.value.splice(rows.value.indexOf(row), 1);
    delete errors[row.id];
    return;
  }
  row.deleted = true;
  row.flag = "Delete";
  delete errors[row.id];
};

const undo = (row) => {
  row.deleted = false;
  row.flag = "Edit";
};

// A row just added and not yet typed into is not a reference yet (auto-saving hosts skip it).
const isBlankNew = (row) => row.flag === "New" && !String(row.filePath || "").trim() && !String(row.fileName || "").trim() && !String(row.note || "").trim();

// Path and name are required, and two live rows may not share a file name.
const validate = ({ ignoreBlankNew = false } = {}) => {
  Object.keys(errors).forEach((key) => delete errors[key]);
  const live = rows.value.filter((r) => !r.deleted && !(ignoreBlankNew && isBlankNew(r)));
  const seen = new Map();
  let ok = true;
  const fail = (row, field, message) => {
    errors[row.id] = { ...(errors[row.id] || {}), [field]: message };
    ok = false;
  };
  for (const row of live) {
    if (!String(row.filePath || "").trim()) fail(row, "filePath", "File path is required");
    if (!String(row.fileName || "").trim()) {
      fail(row, "fileName", "File name is required");
      continue;
    }
    const key = row.fileName.trim().toLowerCase();
    if (seen.has(key)) {
      fail(row, "fileName", "Another reference already uses this file name");
      fail(seen.get(key), "fileName", "Another reference already uses this file name");
    } else {
      seen.set(key, row);
    }
  }
  return ok;
};

// After a failed save, typing re-checks as the reader goes, so a fixed row loses its message at once.
watch(rows, () => {
  if (Object.keys(errors).length) validate();
}, { deep: true });

defineExpose({ validate, isBlankNew });
</script>

<style scoped>
.rdr__empty {
  padding: 18px 12px;
  text-align: center;
  border: 1.5px dashed #c2cad6;
  border-radius: 10px;
  background: #fafbfc;
}
.rdr__row {
  border-radius: 10px;
  background: #fbfcfe;
  box-shadow: none;
}
.rdr__row--deleted {
  background: #f5f5f5;
}
</style>
