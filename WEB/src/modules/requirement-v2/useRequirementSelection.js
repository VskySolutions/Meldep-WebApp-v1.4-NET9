import { ref, computed } from "vue";

// The list's multi-select, which survives paging, reloads and a page refresh: the ids live in
// localStorage (under keys of its own, so the v2 list and the current list each keep their own ticks),
// together with whether each one is editable — bulk actions are refused when any selected row is
// view-only.
const IDS_KEY = "selectedRequirementIdsV2";
const EDITABLE_KEY = "selectedRequirementEditableMapV2";

const readJson = (key, fallback) => {
  try {
    const stored = localStorage.getItem(key);
    if (!stored) return fallback;
    const parsed = JSON.parse(stored);
    const ok = Array.isArray(fallback) ? Array.isArray(parsed) : (parsed && typeof parsed === "object" && !Array.isArray(parsed));
    return ok ? parsed : fallback;
  } catch {
    localStorage.removeItem(key);
    return fallback;
  }
};

export function useRequirementSelection () {
  const selectedIds = ref(readJson(IDS_KEY, []));
  const editableMap = ref(readJson(EDITABLE_KEY, {}));

  const persist = () => {
    localStorage.setItem(IDS_KEY, JSON.stringify(selectedIds.value));
    localStorage.setItem(EDITABLE_KEY, JSON.stringify(editableMap.value));
  };

  const count = computed(() => selectedIds.value.length);
  const hasNonEditable = computed(() => selectedIds.value.some((id) => editableMap.value[id] !== true));
  const isSelected = (id) => selectedIds.value.includes(id);

  // Reconcile with what the table reports for the rows on this page: a page row missing from
  // `pageSelected` was unticked; rows on other pages are left as they were.
  const applyPageSelection = (pageRows, pageSelected) => {
    const checked = new Set(pageSelected.map((r) => r.id));
    const next = new Set(selectedIds.value);
    for (const row of pageRows) {
      if (checked.has(row.id)) {
        next.add(row.id);
        editableMap.value[row.id] = row.isEditable === true;
      } else {
        next.delete(row.id);
        delete editableMap.value[row.id];
      }
    }
    selectedIds.value = [...next];
    persist();
  };

  // A project's permissions may have changed since a row was ticked; every load refreshes the flag.
  const refreshEditable = (rows) => {
    let changed = false;
    for (const row of rows) {
      if (isSelected(row.id) && editableMap.value[row.id] !== (row.isEditable === true)) {
        editableMap.value[row.id] = row.isEditable === true;
        changed = true;
      }
    }
    if (changed) persist();
  };

  const clear = () => {
    selectedIds.value = [];
    editableMap.value = {};
    localStorage.removeItem(IDS_KEY);
    localStorage.removeItem(EDITABLE_KEY);
  };

  return { selectedIds, count, hasNonEditable, isSelected, applyPageSelection, refreshEditable, clear };
}
