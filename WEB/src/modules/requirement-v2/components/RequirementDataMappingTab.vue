<template>
  <div class="rdm">
    <app-data-table
      page-key="requirement-v2-data-mappings"
      row-key="key"
      :title="title"
      :rows="filtered"
      :columns="columns"
      :loading="loading"
      :pagination="CLIENT_PAGING"
      :rows-per-page-options="[20, 50, 100, 200, 500]"
      default-sort-by="createdOn"
      client-sort
      dense
      @refresh="refresh"
    >
      <template #actions>
        <q-input v-model="search" dense outlined clearable debounce="150" placeholder="Search mappings" aria-label="Search mappings" class="rdm__search">
          <template #prepend><q-icon name="o_search" size="18px" /></template>
        </q-input>
        <app-auto-save-state
          v-if="editable"
          :state="saveState.state"
          :message="saveState.message"
          :saved-at="savedAt"
          idle-text="Changes save as you edit"
          @save-now="saveAllPending"
        />
        <q-btn flat round dense color="grey-8" icon="o_download" aria-label="Export to CSV" :disable="!savedRows.length" @click="exportMappings">
          <q-tooltip>Export to CSV</q-tooltip>
        </q-btn>
        <q-btn v-if="editable" unelevated no-caps dense color="primary" icon="o_add" label="Add mapping" class="rdm__add" @click="addRow">
          <q-tooltip>Add a source → target mapping</q-tooltip>
        </q-btn>
      </template>

      <template #no-data>
        <div v-if="loading" class="full-width row flex-center q-pa-lg">
          <q-spinner color="primary" size="28px" />
        </div>
        <app-empty-state v-else-if="loadFailed" icon="o_cloud_off" title="The mappings could not be loaded" text="Check the connection and try again.">
          <q-btn flat no-caps color="primary" icon="o_refresh" label="Try again" @click="load" />
        </app-empty-state>
        <app-empty-state v-else-if="rows.length" icon="o_search_off" :title="`No mapping matches “${search}”`" />
        <app-empty-state
          v-else-if="!draft"
          icon="o_account_tree"
          title="No data mappings yet"
          :text="editable ? 'Add the first source → target mapping with Add mapping.' : 'Source → target field mappings will show here once they are added.'"
        />
        <span v-else />
      </template>

      <!-- The line a new mapping is typed into stays above the table's own rows, whatever they are sorted by. -->
      <template #top-row="top">
        <requirement-data-mapping-row
          v-if="draft"
          ref="draftLine"
          :row="draft"
          :cols="top.cols"
          editable
          is-new
          :direction-options="directionOptions"
          @update="(field, value) => { draft[field] = value; }"
          @enter="commitDraft(true)"
          @leave="commitDraft(false)"
          @revert="(field) => { draft[field] = ''; }"
          @direction="setDirection(draft, $event)"
          @remove="draft = null"
        />
      </template>

      <template #body="scope">
        <requirement-data-mapping-row
          :row="scope.row"
          :cols="scope.cols"
          :scope="scope"
          :editable="editable"
          :direction-options="directionOptions"
          @update="(field, value) => { scope.row[field] = value; }"
          @enter="commit(scope.row, true)"
          @leave="commit(scope.row, true)"
          @revert="(field) => revert(scope.row, field)"
          @direction="setDirection(scope.row, $event)"
          @notes="openNotes(scope.row)"
          @retry="saveRow(scope.row)"
          @remove="removeRow(scope.row)"
        />
      </template>
    </app-data-table>
  </div>
</template>

<script setup>
// The data mappings of one requirement, on its Data mapping tab (v2), in the standard data table: sorting,
// paging, the column menu (show, reorder, resize, remembered), refresh, search and CSV export. The rows
// are edited where they stand: a row saves itself when the reader leaves it (or presses Enter), and a new
// mapping is created once its source, target and direction are all given. Each row carries its notes
// behind a note icon with their count. Reads and writes go to the current data-mapping endpoints, so the
// save rules are the ones the Edit data mapping dialog has.
import { ref, computed, watch, nextTick, onBeforeUnmount } from "vue";
import AppDataTable from "components/common/AppDataTable.vue";
import AppEmptyState from "components/common/AppEmptyState.vue";
import AppAutoSaveState from "components/common/AppAutoSaveState.vue";
import RequirementDataMappingRow from "modules/requirement-v2/components/RequirementDataMappingRow.vue";
import { useConfirm } from "composables/useConfirm";
import { useNotify } from "composables/useNotify";
import { exportCsv } from "composables/useCsvExport";
import { useSingleSelectDropdown } from "composables/form-inputs/useDropdown.js";
import commonService from "services/common.service";
import requirementService from "modules/requirement-v2/requirementV2.service";
import { personName, displayTimestamp } from "modules/requirement-v2/utils/options";
import { initRequirementDataMappingDialogs, onRequirementDataMappingNoteEdit } from "src/modules/requirement-data-mapping/utils/dialogs.js";

const props = defineProps({
  id: { type: String, required: true },
  // The requirement: its project and module go with a new mapping, its labels name the two columns.
  model: { type: Object, default: null },
  // The reader may add, change and delete mappings (and manage their notes); otherwise the grid is read-only.
  editable: { type: Boolean, default: false }
});

// A mapping was added or deleted: the count on the tab's title is out of date.
const emit = defineEmits(["changed"]);

// Every row is here already, so the table pages and sorts them itself (no row count from a server).
const CLIENT_PAGING = { rowsNumber: undefined };

const { confirm } = useConfirm();
const notify = useNotify();

const activeRowId = ref(null);
initRequirementDataMappingDialogs(activeRowId);

const sourceLabel = computed(() => props.model?.sourceLabel || "Source");
const targetLabel = computed(() => props.model?.targetLabel || "Target");

// ---- Directions (a managed dropdown) ----
const directions = useSingleSelectDropdown(commonService.getDropDown, { labelKey: "dropdownValue", valueKey: "id", colorKey: "color", bgColorKey: "bgColor" });
const directionOptions = computed(() => directions.list.value.map((item) => ({
  label: item.text,
  value: item.value,
  backgroundColor: item.bgColor || "#e8eef1",
  textColor: item.color || "#3a4d54"
})));
const directionText = (row) => directionOptions.value.find((o) => o.value === row.directionId)?.label || row.directionLabel || "";

// ---- Rows ----
const rows = ref([]);
const draft = ref(null);
const draftLine = ref(null);
const loading = ref(false);
const loadFailed = ref(false);
const savedAt = ref(null);
const search = ref("");
let created = 0;
// The order of creation, oldest 1: the server lists newest first, to the instant, while the date it
// shows is to the minute, so the rows are numbered by where the server put them.
let sequence = 0;

const toRow = (item, createdSeq = 0) => ({
  key: item.id,
  id: item.id,
  source: item.source || "",
  target: item.target || "",
  directionId: item.integrationDirectionId || null,
  directionLabel: item.integrationDirection?.dropDownValue || "",
  notesCount: item.dataMappingNotesCount || 0,
  createdBy: personName(item.createdBy),
  createdOn: item.createdOnUtc || "",
  createdSeq,
  updatedOn: item.updatedOnUtc || "",
  updatedAt: displayTimestamp(item.updatedOnUtc),
  // What the server holds for this row; a row not yet created holds nothing.
  saved: { source: item.source || "", target: item.target || "", directionId: item.integrationDirectionId || null },
  touched: false,
  saving: false,
  again: false,
  error: ""
});

const isComplete = (row) => !!row.source.trim() && !!row.target.trim() && !!row.directionId;
const isDirty = (row) => !row.id
  || row.source.trim() !== row.saved.source
  || row.target.trim() !== row.saved.target
  || row.directionId !== row.saved.directionId;

// A row with something of the reader's still in it (typed, being saved, or refused) is kept as it is by
// a reload: only the rest is read afresh.
const load = async () => {
  loading.value = true;
  loadFailed.value = false;
  try {
    const list = await requirementService.getDataMappings(props.id);
    const kept = rows.value.filter((row) => !row.id || row.saving || row.error || isDirty(row));
    const keptById = new Map(kept.filter((row) => row.id).map((row) => [row.id, row]));
    sequence = list.length;
    const read = list.map((item, index) => {
      const mine = keptById.get(item.id);
      if (!mine) return toRow(item, list.length - index);
      mine.createdSeq = list.length - index;
      return mine;
    });
    // Rows the server does not have yet are the newest of all.
    const unsaved = kept.filter((row) => !row.id);
    for (const row of [...unsaved].reverse()) {
      sequence += 1;
      row.createdSeq = sequence;
    }
    rows.value = [...unsaved, ...read];
  } catch (error) {
    // The interceptor has said what went wrong; the grid keeps what it had.
    loadFailed.value = true;
    console.error("Error loading data mappings", error);
  } finally {
    loading.value = false;
  }
};

watch(() => props.id, () => {
  rows.value = [];
  draft.value = null;
  search.value = "";
  savedAt.value = null;
  load();
}, { immediate: true });

directions.load("Data Mapping Integration Direction").catch((error) => console.error(error));

const savedRows = computed(() => rows.value.filter((row) => row.id));
const filtered = computed(() => {
  const needle = (search.value || "").trim().toLowerCase();
  if (!needle) return rows.value;
  // A row not yet created stays in sight whatever the search says.
  return rows.value.filter((row) => !row.id || `${row.source} ${row.target} ${directionText(row)} ${row.createdBy}`.toLowerCase().includes(needle));
});

const title = computed(() => {
  const all = savedRows.value.length;
  const shown = filtered.value.filter((row) => row.id).length;
  return (search.value || "").trim() ? `Data mappings (${shown} of ${all})` : `Data mappings (${all})`;
});

// ---- Columns ----
// Sorting reads what is saved, not what is being typed: a row does not jump while its cell is edited.
const held = (row, field) => (row.id ? row.saved[field] : row[field]) || "";
const columns = computed(() => [
  { name: "source", label: sourceLabel.value, field: "source", align: "left", sortable: true, default: true, width: 320, sort: (row) => held(row, "source").toLowerCase() },
  { name: "target", label: targetLabel.value, field: "target", align: "left", sortable: true, default: true, width: 320, sort: (row) => held(row, "target").toLowerCase() },
  { name: "direction", label: "Direction", field: (row) => directionText(row), align: "left", sortable: true, default: true, width: 190, sort: (row) => directionText(row).toLowerCase() },
  { name: "createdBy", label: "Created by", field: "createdBy", align: "left", sortable: true, width: 170, sort: (row) => (row.createdBy || "").toLowerCase() },
  { name: "createdOn", label: "Created on", field: "createdOn", align: "left", sortable: true, width: 170, sort: (row) => row.createdSeq },
  { name: "updatedOn", label: "Updated on", field: "updatedOn", align: "left", sortable: true, width: 170, sort: (row) => row.updatedAt },
  { name: "actions", label: "Actions", field: "actions", align: "center" }
]);

// ---- Saving ----
// The first line of what the server said; the fallback when it said nothing readable.
const errorText = (error, fallback) => {
  const data = error?.response?.data;
  const text = typeof data === "string" ? data : data?.message || data?.title || error?.message;
  return text ? String(text).split("\n")[0].slice(0, 200) : fallback;
};

const payload = (item) => ({
  requirementId: props.id,
  projectId: props.model?.project?.id || props.model?.projectId || "",
  projectModuleId: props.model?.projectModule?.id || props.model?.projectModuleId || "",
  dataMappings: [{ ...item, notes: [] }]
});

const saveRow = async (row) => {
  // One write per row at a time: a change made meanwhile is saved when this one is back.
  if (row.saving) {
    row.again = true;
    return;
  }
  const sent = { source: row.source.trim(), target: row.target.trim(), directionId: row.directionId };
  const creating = !row.id;
  row.saving = true;
  row.error = "";
  try {
    const answer = await requirementService.saveDataMappings(
      payload({ id: row.id || "", source: sent.source, target: sent.target, integrationDirectionId: sent.directionId, deleted: false }),
      { silent: true }
    );
    const stored = Array.isArray(answer) ? answer[0] : null;
    if (creating) {
      if (!stored?.id) throw new Error("The server did not answer with the new mapping.");
      row.id = stored.id;
      if (stored.createdOnUtc) row.createdOn = stored.createdOnUtc;
    }
    if (stored?.updatedOnUtc) {
      row.updatedOn = stored.updatedOnUtc;
      row.updatedAt = displayTimestamp(stored.updatedOnUtc) || Date.now();
    }
    row.saved = sent;
    // The cell shows what was stored (without stray spaces), unless the reader has typed on meanwhile.
    if (row.source.trim() === sent.source) row.source = sent.source;
    if (row.target.trim() === sent.target) row.target = sent.target;
    savedAt.value = Date.now();
    if (creating) emit("changed");
  } catch (error) {
    row.error = errorText(error, "The mapping could not be saved.");
    console.error("Error saving a data mapping", error);
  } finally {
    row.saving = false;
    if (row.again) {
      row.again = false;
      if (isDirty(row) && isComplete(row)) saveRow(row);
    }
  }
};

// Saves the row if there is something to save and it can be saved. `touch` marks what is missing: set
// when the reader has finished with the row (left it, or pressed Enter), not while moving inside it.
const commit = (row, touch = false) => {
  if (!props.editable) return;
  if (touch) row.touched = true;
  if (!isDirty(row) || !isComplete(row)) return;
  saveRow(row);
};

const revert = (row, field) => {
  row[field] = row.saved[field] ?? "";
};

const setDirection = (row, value) => {
  row.directionId = value;
  row.directionLabel = directionOptions.value.find((o) => o.value === value)?.label || "";
  // The new-mapping line is created when it is left or Enter is pressed, not by its direction alone.
  if (row !== draft.value) commit(row);
};

// Retry, from the indicator: every row that has something to save and can be saved.
const saveAllPending = () => {
  for (const row of rows.value) {
    row.touched = true;
    if (isDirty(row) && isComplete(row)) saveRow(row);
  }
  if (draft.value) commitDraft(false);
};

// Where the grid as a whole stands, for the indicator.
const saveState = computed(() => {
  const list = draft.value ? [draft.value, ...rows.value] : rows.value;
  if (list.some((row) => row.saving)) return { state: "saving", message: "" };
  const failed = list.find((row) => row.error);
  if (failed) return { state: "error", message: failed.error };
  if (list.some((row) => row.touched && isDirty(row) && !isComplete(row))) {
    return { state: "blocked", message: `Fill in ${sourceLabel.value}, ${targetLabel.value} and Direction to save the mapping` };
  }
  return savedAt.value ? { state: "saved", message: "" } : { state: "idle", message: "" };
});

// ---- The new-mapping line ----
// The direction most mappings of this requirement already have is the likeliest for the next one.
const likelyDirection = () => {
  const counts = rows.value.map((row) => row.directionId).filter(Boolean)
    .reduce((acc, id) => acc.set(id, (acc.get(id) || 0) + 1), new Map());
  return [...counts.entries()].sort((a, b) => b[1] - a[1])[0]?.[0]
    || (directionOptions.value.length === 1 ? directionOptions.value[0].value : null);
};

const blankLine = (directionId) => ({
  ...toRow({ id: "", integrationDirectionId: directionId }),
  key: "new-mapping",
  directionLabel: directionOptions.value.find((o) => o.value === directionId)?.label || ""
});

const addRow = async () => {
  if (!draft.value) draft.value = blankLine(likelyDirection());
  await nextTick();
  draftLine.value?.focusSource();
};

// The line is finished with: left (`stay` false) or Enter pressed in it (`stay` true). A complete line
// becomes a row of the table and is created on the server; after Enter a fresh line is ready for the
// next mapping, with the same direction. An untouched line that is left simply goes away.
const commitDraft = (stay) => {
  const line = draft.value;
  if (!line) return;
  if (!line.source.trim() && !line.target.trim()) {
    if (!stay) draft.value = null;
    return;
  }
  line.touched = true;
  if (!isComplete(line)) return;

  created += 1;
  sequence += 1;
  rows.value.unshift({ ...line, key: `new-${created}`, touched: false, createdSeq: sequence, updatedAt: Date.now() });
  saveRow(rows.value[0]);

  if (stay) {
    draft.value = blankLine(line.directionId);
    nextTick(() => draftLine.value?.focusSource());
  } else {
    draft.value = null;
  }
};

// ---- Removing ----
const drop = (row) => {
  rows.value = rows.value.filter((r) => r !== row);
};

const removeRow = async (row) => {
  // A row the server never took (its creation was refused) is only on this screen.
  if (!row.id) {
    drop(row);
    return;
  }
  const notes = row.notesCount ? ` Its ${row.notesCount} note${row.notesCount === 1 ? "" : "s"} will no longer be reachable.` : "";
  const ok = await confirm({
    title: "Delete mapping",
    message: `Delete the mapping “${row.saved.source}” → “${row.saved.target}”?${notes}`,
    confirmLabel: "Delete",
    type: "danger"
  });
  if (!ok) return;
  row.saving = true;
  row.error = "";
  try {
    await requirementService.saveDataMappings(
      payload({ id: row.id, source: row.saved.source, target: row.saved.target, integrationDirectionId: row.saved.directionId, deleted: true }),
      { silent: true }
    );
    drop(row);
    notify.success("Mapping deleted.");
    emit("changed");
  } catch (error) {
    row.error = errorText(error, "The mapping could not be deleted.");
    console.error("Error deleting a data mapping", error);
  } finally {
    row.saving = false;
  }
};

// ---- Notes ----
// Only the counts are read back: what is being typed in the grid is left as it is.
let countsPending = null;
const refreshNoteCounts = () => {
  if (countsPending) return countsPending;
  countsPending = requirementService.getDataMappings(props.id, { silent: true })
    .then((list) => {
      const counts = new Map(list.map((item) => [item.id, item.dataMappingNotesCount || 0]));
      for (const row of rows.value) {
        if (row.id && counts.has(row.id)) row.notesCount = counts.get(row.id);
      }
    })
    .catch((error) => console.error("Error refreshing note counts", error))
    .finally(() => { countsPending = null; });
  return countsPending;
};

const openNotes = (row) => {
  onRequirementDataMappingNoteEdit(row.id, row.saved.source, row.saved.target, props.editable, refreshNoteCounts);
};

// ---- Refresh and export ----
// What can be saved is sent first, then the rows are read again.
const refresh = async () => {
  await Promise.allSettled(rows.value.filter((row) => isDirty(row) && isComplete(row) && !row.saving).map(saveRow));
  await load();
};

const exportMappings = () => {
  const list = filtered.value.filter((row) => row.id);
  if (!list.length) return;
  exportCsv({
    filename: `data-mappings-${props.model?.requirementNumber || props.id}-${new Date().toISOString().slice(0, 10)}.csv`,
    columns: [
      { label: sourceLabel.value, value: (row) => row.saved.source },
      { label: targetLabel.value, value: (row) => row.saved.target },
      { label: "Direction", value: (row) => directionText(row) },
      { label: "Notes", value: (row) => row.notesCount },
      { label: "Created by", value: (row) => row.createdBy },
      { label: "Created on", value: (row) => row.createdOn },
      { label: "Updated on", value: (row) => row.updatedOn }
    ],
    rows: list
  });
  notify.success(`${list.length} row${list.length === 1 ? "" : "s"} exported.`);
};

// Leaving the tab's page with a finished edit still in a cell: it is sent on the way out.
onBeforeUnmount(() => {
  for (const row of rows.value) {
    if (isDirty(row) && isComplete(row) && !row.saving) saveRow(row);
  }
});

defineExpose({ reload: load });
</script>

<style scoped>
.rdm__search {
  width: 240px;
  max-width: 100%;
}
.rdm__search.q-field--dense :deep(.q-field__control) {
  height: 34px;
  min-height: 34px;
  background: #fff;
}
.rdm__search.q-field--dense :deep(.q-field__marginal) {
  height: 34px;
}
.rdm__add {
  margin: 0 4px;
}
@media (max-width: 599px) {
  .rdm__search {
    width: 100%;
  }
}
</style>
