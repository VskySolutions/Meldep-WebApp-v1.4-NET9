// Row-level writes shared by every screen that lists requirements (this module, Project Center, the
// project planner). The signatures are a contract: other modules call these as they stand.
//
// Failures are not toasted here: boot/interceptors.js already shows the server's sentence for every
// failed request, and a second toast on top of it was the old behaviour's wall of toasts.
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import requirementService from "modules/requirement-v2/requirementV2.service";

const notify = useNotify();
const { confirm } = useConfirm();

let activeRowId;

export function initRequirementActions (rowRef) {
  activeRowId = rowRef;
}

const setActive = (id) => {
  if (activeRowId) activeRowId.value = id;
};

// ---- Pin / unpin (private to the signed-in user; the server floats pinned rows to the top) ----
export const onSubmitRequirementPinned = async (id, status, refreshRequirementList) => {
  try {
    await requirementService.updateRequirementIsPinned(id, status);
    notify.success(status ? "Requirement pinned to the top of the list, for you only." : "Requirement unpinned.");
    refreshRequirementList?.();
  } catch (error) {
    console.error("Error updating requirement pin status", error);
  } finally {
    setActive(id);
  }
};

// Pin or unpin several at once (one request each; the server has no bulk endpoint for pins).
export const onSubmitRequirementBulkPinned = async (ids, status, refreshRequirementList) => {
  const results = await Promise.allSettled(ids.map((id) => requirementService.updateRequirementIsPinned(id, status)));
  const done = results.filter((r) => r.status === "fulfilled").length;
  if (done) notify.success(`${done} requirement${done === 1 ? "" : "s"} ${status ? "pinned" : "unpinned"}.`);
  refreshRequirementList?.();
  return done;
};

// ---- Row colour ----
export const onSubmitRequirementColor = async (id, color, isSliderActive, previousColor, refreshRequirementList) => {
  if (!isSliderActive || color === previousColor) return false;

  try {
    await requirementService.updateRequirementColor(id, encodeURIComponent(color));
    notify.success(color ? "Requirement coloured, for you only." : "Requirement colour cleared.");
    refreshRequirementList?.();
  } catch (error) {
    console.error("Error updating requirement colour", error);
  } finally {
    setActive(id);
  }
};

// ---- Tags ----
export const onSubmitRequirementTags = async (requirementId, tagNames, refreshRequirementList, refreshRequirementTagDropdown) => {
  setActive(requirementId);
  const model = {
    requirementIds: [requirementId],
    flag: null,
    tagsNameList: (tagNames || []).map((tag) => (typeof tag === "string" ? tag : tag.text))
  };

  try {
    await requirementService.saveTags(model);
    notify.success("Tags saved.");
    refreshRequirementList?.();
    refreshRequirementTagDropdown?.();
  } catch (error) {
    console.error("Error saving requirement tags", error);
  }
};

// ---- Status ----
// `note` (optional) is kept with the change in the status change log.
export const onSubmitRequirementStatus = async (id, statusId, refreshRequirementList, note = null) => {
  try {
    await requirementService.updateRequirementStatus({ requirementIds: [id], statusId, statusNote: note });
    notify.success("Status updated.");
    refreshRequirementList?.();
  } catch (error) {
    console.error("Error updating requirement status", error);
  } finally {
    setActive(id);
  }
};

// ---- Dates (one at a time; the change and its note go to the date change log) ----
const DATE_FIELDS = {
  plannedStartDate: "Planned start date",
  plannedEndDate: "Planned end date",
  actualStartDate: "Actual start date",
  actualEndDate: "Actual end date"
};

// `dateStr` is "MM/DD/YYYY", as the date picker and the API both use.
export const onSubmitRequirementDate = async (id, dateStr, field, note = null, refreshRequirementList) => {
  if (!DATE_FIELDS[field]) throw new Error("Invalid requirement date field.");
  try {
    await requirementService.updateRequirementDates({
      requirementId: id,
      [`${field}Str`]: dateStr,
      [`${field}Note`]: note
    });
    notify.success(`${DATE_FIELDS[field]} updated.`);
    refreshRequirementList?.();
  } catch (error) {
    console.error(`Error updating ${DATE_FIELDS[field]}`, error);
  } finally {
    setActive(id);
  }
};

// ---- Priority ----
export const onSubmitRequirementPriority = async (id, priorityId, refreshRequirementList) => {
  try {
    await requirementService.updateRequirementPriority({ requirementIds: [id], priorityId });
    notify.success("Priority updated.");
    refreshRequirementList?.();
  } catch (error) {
    console.error("Error updating requirement priority", error);
  } finally {
    setActive(id);
  }
};

// ---- Delete (confirmed: names the record and the consequence) ----
export const onSubmitRequirementDelete = async (id, requirementName, refreshRequirementList) => {
  setActive(id);

  const ok = await confirm({
    title: "Delete requirement",
    message: `Delete "${requirementName}"? Its log, notes and document references go with it. This cannot be undone.`,
    confirmLabel: "Delete",
    type: "danger"
  });

  if (!ok) {
    setActive(null);
    return false;
  }

  try {
    await requirementService.deleteRequirement(id);
    notify.success("Requirement deleted.");
    refreshRequirementList?.();
    return true;
  } catch (error) {
    console.error("Error deleting requirement", error);
    return false;
  } finally {
    setActive(null);
  }
};

// Delete the selection (confirmed once with the count; one request each).
export const onSubmitRequirementBulkDelete = async (rows, refreshRequirementList) => {
  const count = rows.length;
  if (!count) return 0;
  const ok = await confirm({
    title: "Delete requirements",
    message: `Delete ${count} requirement${count === 1 ? "" : "s"}? Their logs, notes and document references go with them. This cannot be undone.`,
    confirmLabel: `Delete ${count}`,
    type: "danger"
  });
  if (!ok) return 0;

  const results = await Promise.allSettled(rows.map((row) => requirementService.deleteRequirement(row.id)));
  const done = results.filter((r) => r.status === "fulfilled").length;
  const failed = count - done;
  if (done) {
    notify.success(`${done} requirement${done === 1 ? "" : "s"} deleted.`, failed ? { caption: `${failed} could not be deleted.` } : undefined);
  }
  refreshRequirementList?.();
  return done;
};

// ---- Permalink ----
export const copyRequirementLink = async (href) => {
  try {
    await navigator.clipboard.writeText(href);
    notify.success("Link copied.");
  } catch {
    notify.warning("Copy failed — please select and copy the address manually.");
  }
};
