// A status change or a date change may carry a note (optional), as on the current requirement list;
// the note is kept with the change in its change log. Shared by the v2 list, record and edit pages.
import { Dialog } from "quasar";

// Resolves { ok, note }: Save gives the note typed (or null); Cancel leaves the value as it was.
export const askChangeNote = (title, message) => new Promise((resolve) => {
  Dialog.create({
    title,
    message,
    prompt: { model: "", type: "textarea", outlined: true, autogrow: true },
    ok: { label: "Save", color: "primary", unelevated: true, noCaps: true },
    cancel: { label: "Cancel", flat: true, noCaps: true },
    persistent: true
  }).onOk((note) => resolve({ ok: true, note: (note || "").trim() || null }))
    .onCancel(() => resolve({ ok: false, note: null }));
});

// The four dates that are changed one at a time with a note. `log` is the name each one's change log
// is kept under (the same names the current list writes).
export const LOGGED_DATES = [
  { field: "plannedStartDate", label: "Planned start", log: "Planned Start Date" },
  { field: "plannedEndDate", label: "Planned end", log: "Planned End Date", after: "plannedStartDate" },
  { field: "actualStartDate", label: "Actual start", log: "Actual Start Date" },
  { field: "actualEndDate", label: "Actual end", log: "Actual End Date", after: "actualStartDate" }
];

// q-date hands its `options` function a "YYYY/MM/DD" day; an end date may not fall before its start
// ("MM/DD/YYYY", or nothing when the start is not set).
export const notBefore = (startDate) => {
  const m = /^(\d{1,2})\/(\d{1,2})\/(\d{4})/.exec(String(startDate || "").trim());
  if (!m) return undefined;
  const min = `${m[3]}/${m[1].padStart(2, "0")}/${m[2].padStart(2, "0")}`;
  return (day) => day >= min;
};
