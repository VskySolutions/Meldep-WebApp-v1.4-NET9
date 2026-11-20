// Adapters between Meldep's dropdown shapes and the option shape the App* badges draw, plus the small
// date reckonings the requirement screens share.
//
// Two shapes arrive from the API: a dropdown LIST item ({ text, value, color, bgColor }, from the
// useDropdown composables) and a dropdown VALUE on a record ({ id, dropDownValue, color, bgColor }, the
// DropDown entity). Both become { label, value, backgroundColor, textColor } so every status, priority
// and approval state renders through AppOptionBadge the same way.

export const toBadgeOption = (item) => ({
  label: item?.text ?? "",
  value: item?.value ?? null,
  backgroundColor: item?.bgColor || "",
  textColor: item?.color || ""
});

export const toBadgeOptions = (items) => (items || []).map(toBadgeOption);

/** The badge for a DropDown value carried on a record, or null when the record has none. */
export const dropDownBadge = (dropDown) => {
  const label = dropDown?.dropDownValue;
  if (!label) return null;
  return {
    label,
    value: dropDown.id ?? null,
    backgroundColor: dropDown.bgColor || "",
    textColor: dropDown.color || ""
  };
};

/** A person's display name from the nested shapes the API uses (employee.person, customer). */
export const personName = (entity) => {
  const person = entity?.person || entity;
  if (!person) return "";
  if (person.fullName) return person.fullName.trim();
  return [person.firstName, person.lastName].filter(Boolean).join(" ");
};

/** The label for a dropdown item id, resolved against a loaded list. */
export const labelFor = (list, value) => list?.find((item) => item.value === value)?.text ?? "";

// ---- Dates ----
// The API renders calendar dates as "MM/dd/yyyy" (and timestamps as "MM/dd/yyyy hh:mm tt"); nothing here
// goes through the browser's time zone.

const DISPLAY_DATE = /^(\d{1,2})\/(\d{1,2})\/(\d{4})/;

/** A "MM/dd/yyyy…" string as a UTC-midnight Date, or null. */
export const parseDisplayDate = (value) => {
  const m = DISPLAY_DATE.exec(String(value || "").trim());
  if (!m) return null;
  return new Date(Date.UTC(Number(m[3]), Number(m[1]) - 1, Number(m[2])));
};

const DISPLAY_DATETIME = /^(\d{1,2})\/(\d{1,2})\/(\d{4})(?:\s+(\d{1,2}):(\d{2})(?::(\d{2}))?\s*([AaPp][Mm])?)?/;

/** A "MM/dd/yyyy hh:mm tt" (or date-only, or ISO) string as a sortable timestamp, 0 when unreadable. */
export const displayTimestamp = (value) => {
  const text = String(value || "").trim();
  if (!text) return 0;
  const m = DISPLAY_DATETIME.exec(text);
  if (m) {
    let hours = Number(m[4] || 0);
    const minutes = Number(m[5] || 0);
    const seconds = Number(m[6] || 0);
    const meridiem = (m[7] || "").toUpperCase();
    if (meridiem === "PM" && hours < 12) hours += 12;
    if (meridiem === "AM" && hours === 12) hours = 0;
    return Date.UTC(Number(m[3]), Number(m[1]) - 1, Number(m[2]), hours, minutes, seconds);
  }
  const iso = Date.parse(text);
  return Number.isNaN(iso) ? 0 : iso;
};

/** The calendar day ("MM/dd/yyyy") of a display timestamp, for grouping a feed by day. */
export const displayDay = (value) => {
  const m = DISPLAY_DATETIME.exec(String(value || "").trim());
  return m ? `${m[1].padStart(2, "0")}/${m[2].padStart(2, "0")}/${m[3]}` : "";
};

const todayUtc = () => {
  const d = new Date();
  return new Date(Date.UTC(d.getFullYear(), d.getMonth(), d.getDate()));
};

/** "Today", "Yesterday" or "28 Oct 2026" for a display timestamp's calendar day: the day pills of a thread. */
export const dayLabel = (value) => {
  const day = displayDay(value);
  if (!day) return "";
  const [mm, dd, yyyy] = day.split("/").map(Number);
  const that = new Date(yyyy, mm - 1, dd);
  const now = new Date();
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const daysAgo = Math.round((today.getTime() - that.getTime()) / 86400000);
  if (daysAgo === 0) return "Today";
  if (daysAgo === 1) return "Yesterday";
  return that.toLocaleDateString("en-GB", { day: "numeric", month: "short", year: "numeric" });
};

/** Statuses that count as finished for the overdue reckoning. */
export const CLOSED_STATUS_LABELS = ["Close", "Closed", "Completed", "Cancelled", "Done"];

export const isClosedStatus = (label) => CLOSED_STATUS_LABELS.includes(String(label || ""));

/**
 * Where a requirement stands against its planned end date:
 * { state: "overdue" | "due-soon" | "scheduled" | "done" | "none", days, label }.
 */
export const dueInfo = (plannedEndDate, statusLabel) => {
  if (isClosedStatus(statusLabel)) return { state: "done", days: null, label: "" };
  const end = parseDisplayDate(plannedEndDate);
  if (!end) return { state: "none", days: null, label: "" };
  const days = Math.round((end.getTime() - todayUtc().getTime()) / 86400000);
  if (days < 0) return { state: "overdue", days, label: `${-days} day${-days === 1 ? "" : "s"} overdue` };
  if (days === 0) return { state: "due-soon", days, label: "Due today" };
  if (days <= 7) return { state: "due-soon", days, label: `Due in ${days} day${days === 1 ? "" : "s"}` };
  return { state: "scheduled", days, label: `Due in ${days} days` };
};

/** Planned-window progress for a bar: 0..1 of the planned span that has elapsed, or null without both dates. */
export const plannedProgress = (plannedStart, plannedEnd) => {
  const start = parseDisplayDate(plannedStart);
  const end = parseDisplayDate(plannedEnd);
  if (!start || !end || end <= start) return null;
  const now = todayUtc().getTime();
  return Math.min(1, Math.max(0, (now - start.getTime()) / (end.getTime() - start.getTime())));
};
