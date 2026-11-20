import { ref, computed, watch } from "vue";
import requirementGroupsService from "modules/requirement-group/requirementGroup.service";
import { labelFor } from "modules/requirement-v2/utils/options";

// The list's filters, as the API's RequirementSearchModel names them. Dates are "MM/DD/YYYY" strings,
// the format the endpoint has always been sent.
export const blankFilters = () => ({
  requirementNumber: "",
  projectIds: [],
  projectModuleIds: [],
  requirementGroupIds: [],
  name: "",
  statusIds: [],
  priorityIds: [],
  approvalStatusIds: [],
  requirementTypeIds: [],
  areaIds: [],
  workspaceIds: [],
  requirementOwnerIds: [],
  customerOwnerIds: [],
  identifiedUserTypeId: null,
  identifiedEmployeeIds: [],
  identifiedCustomerIds: [],
  fromDate: null,
  toDate: null,
  plannedEndFromDate: null,
  plannedEndToDate: null,
  updatedFromDate: null,
  updatedToDate: null,
  requirementTagIds: [],
  // Toggles: null means "no filter"; the attention flags and the drawer set these.
  overdue: null,
  dueSoon: null,
  notApproved: null,
  pinnedOnly: null,
  myRequirements: null,
  hasTasks: null
});

export const LIST_FILTER_KEYS = [
  "projectIds", "projectModuleIds", "requirementGroupIds", "statusIds", "priorityIds", "approvalStatusIds",
  "requirementTypeIds", "areaIds", "workspaceIds", "requirementOwnerIds", "customerOwnerIds",
  "identifiedEmployeeIds", "identifiedCustomerIds", "requirementTagIds"
];

export const TOGGLE_FILTER_KEYS = ["overdue", "pinnedOnly", "myRequirements", "hasTasks"];

// The number the server takes: an int, 0 meaning "no filter". Readers type "#128" or "0128" as readily
// as "128".
export const requirementNumberOf = (value) => {
  const digits = String(value ?? "").replace(/\D/g, "").replace(/^0+(?!$)/, "");
  return digits ? Number(digits) : 0;
};

const sameIds = (a, b) => {
  const x = a || [];
  const y = b || [];
  return x.length === y.length && x.every((v, i) => v === y[i]);
};

// Chip labels, clearing, and the two dependencies between filters (project → modules, identifier →
// who identified). `search` is the ref useSiteTableState hands the page.
export function useRequirementFilters (search, lookups) {
  const identifierText = computed(() =>
    labelFor(lookups.identifiers.list.value, search.value.identifiedUserTypeId));

  // Requirement groups only ever arrive through the link from Requirement Groups; the list is fetched
  // just to name the chip.
  const groups = ref([]);
  const ensureGroups = async () => {
    if (!search.value.requirementGroupIds?.length) return;
    try {
      const resp = await requirementGroupsService.getAllRequirementGroupListForDropdown(search.value.projectIds);
      groups.value = (resp || []).map((item) => ({ text: item.text, value: item.value }));
    } catch (error) {
      console.error(error);
    }
  };

  const names = (ids, list) => (ids || []).map((id) => labelFor(list, id) || id).join(", ");

  const chips = computed(() => {
    const s = search.value;
    const out = [];
    const push = (key, label, text) => {
      if (text) out.push({ key, label: `${label}: ${text}` });
    };
    const number = requirementNumberOf(s.requirementNumber);
    if (number) push("requirementNumber", "Req. No.", `#${number}`);
    push("projectIds", "Project", names(s.projectIds, lookups.projects.list.value));
    push("projectModuleIds", "Module", names(s.projectModuleIds, lookups.modules.list.value));
    push("requirementGroupIds", "Requirement group", names(s.requirementGroupIds, groups.value));
    push("name", "Requirement", s.name?.trim());
    push("statusIds", "Status", names(s.statusIds, lookups.statuses.list.value));
    push("priorityIds", "Priority", names(s.priorityIds, lookups.priorities.list.value));
    push("approvalStatusIds", "Approval", names(s.approvalStatusIds, lookups.approvalStatuses.list.value));
    push("requirementTypeIds", "Type", names(s.requirementTypeIds, lookups.types.list.value));
    push("areaIds", "Area", names(s.areaIds, lookups.areas.list.value));
    push("workspaceIds", "Workspace", names(s.workspaceIds, lookups.workspaces.list.value));
    push("requirementOwnerIds", "Owner", names(s.requirementOwnerIds, lookups.employees.list.value));
    push("customerOwnerIds", "Customer owner", names(s.customerOwnerIds, lookups.customers.list.value));
    push("identifiedUserTypeId", "Identifier", s.identifiedUserTypeId ? (identifierText.value || s.identifiedUserTypeId) : "");
    push("identifiedCustomerIds", "Customer", names(s.identifiedCustomerIds, lookups.customers.list.value));
    push("identifiedEmployeeIds", "Employee", names(s.identifiedEmployeeIds, lookups.employees.list.value));
    push("requirementTagIds", "Tags", names(s.requirementTagIds, lookups.requirementTags.list.value));
    push("fromDate", "Created from", s.fromDate);
    push("toDate", "Created to", s.toDate);
    push("plannedEndFromDate", "Due from", s.plannedEndFromDate);
    push("plannedEndToDate", "Due to", s.plannedEndToDate);
    push("updatedFromDate", "Updated from", s.updatedFromDate);
    push("updatedToDate", "Updated to", s.updatedToDate);
    // Every attention flag counts as a filter: with the header folded, the Filters count is all that
    // says the list is narrowed.
    if (s.overdue === true) out.push({ key: "overdue", label: "Overdue" });
    if (s.dueSoon === true) out.push({ key: "dueSoon", label: "Due within 7 days" });
    if (s.notApproved === true) out.push({ key: "notApproved", label: "Not approved" });
    if (s.pinnedOnly === true) out.push({ key: "pinnedOnly", label: "Pinned by me" });
    if (s.myRequirements === true) out.push({ key: "myRequirements", label: "My requirements" });
    if (s.hasTasks === true) out.push({ key: "hasTasks", label: "Converted into tasks" });
    if (s.hasTasks === false) out.push({ key: "hasTasks", label: "Not yet converted" });
    return out;
  });

  const activeCount = computed(() => chips.value.length);

  // Whether any restored filter needs a lookup to be named — then the lists load on mount rather than
  // when the drawer first opens.
  const hasLookupFilters = computed(() => {
    const s = search.value;
    return LIST_FILTER_KEYS.some((key) => s[key]?.length) || !!s.identifiedUserTypeId;
  });

  const clear = (key) => {
    search.value[key] = blankFilters()[key];
  };

  const clearAll = () => {
    Object.assign(search.value, blankFilters());
  };

  // A new project selection makes the old module picks meaningless.
  watch(() => search.value.projectIds, (next, previous) => {
    if (sameIds(next, previous)) return;
    search.value.projectModuleIds = [];
    lookups.loadModules(next);
    ensureGroups();
  });

  // Who identified the requirement depends on whether an employee or a customer did.
  watch(() => search.value.identifiedUserTypeId, (next, previous) => {
    if (next === previous) return;
    search.value.identifiedEmployeeIds = [];
    search.value.identifiedCustomerIds = [];
  });

  // Called once by the page after the saved filters are restored.
  const init = () => {
    if (search.value.projectIds?.length) lookups.loadModules(search.value.projectIds);
    ensureGroups();
  };

  return { chips, activeCount, hasLookupFilters, identifierText, clear, clearAll, init };
}
