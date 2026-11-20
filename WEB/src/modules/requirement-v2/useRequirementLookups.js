import { ref } from "vue";
import { useAuthStore } from "stores/auth";
import projectModule from "src/modules/project/utils/dropdowns.js";
import projectModuleOfProjectModule from "src/modules/project-modules/utils/dropdowns.js";
import projectTaskModule from "src/modules/project-tasks/utils/dropdowns.js";
import employeeModule from "src/modules/employee/utils/dropdowns.js";
import customerModule from "src/modules/customer/utils/dropdowns.js";
import tagModule from "src/modules/tags/utils/dropdowns.js";
import manageDropdownModule from "src/modules/dropdown/utils/dropdowns.js";
import requirementModule from "src/modules/requirement-v2/utils/dropdowns.js";

// Runs a loader once per page, sharing the promise with every later caller; a failed load may be
// retried on the next call. Failures are silent here: an empty picker is still a picker, and the
// interceptor has already said what went wrong.
const once = (loader) => {
  let pending = null;
  return () => {
    if (!pending) {
      pending = Promise.resolve().then(loader).catch((error) => {
        pending = null;
        console.error(error);
      });
    }
    return pending;
  };
};

const settle = (loaders) =>
  Promise.allSettled(loaders.map((fn) => fn())).then((results) => {
    results.forEach((r) => { if (r.status === "rejected") console.error(r.reason); });
  });

// The option lists the requirement list draws from, split by WHEN they are needed: the status and
// priority lists on every load (the row badges, the board columns, the KPI strip), the filter lists when
// the drawer opens or a saved filter has to be named, the tag catalogue when a tag editor opens, the
// dropdown types when an admin opens Manage Dropdowns.
export function useRequirementLookups () {
  const user = useAuthStore().user;

  const { projectNameDropdown: projects } = projectModule();
  const { projectModulesByProjectIdForDropdown: modules } = projectModuleOfProjectModule();
  const { customerContactDropdown: customers } = customerModule();
  const { activeEmployeesDropdown: employees } = employeeModule();
  const { areaForDropdownSingleSelect: areas, workspaceForDropdownSingleSelect: workspaces } = projectTaskModule();
  const { tagsDropdown: tags } = tagModule();
  const { getDropdownTypesByModuleNameForDropdown } = manageDropdownModule();
  const {
    requirementStatusDropdownSingleSelect: statuses,
    requirementPriorityDropdownSingleSelect: priorities,
    requirementApprovalStatusDropdownSingleSelect: approvalStatuses,
    requirementTypeForDropdown: types,
    requirementIdentifiedUserTypeDropdownSingleSelect: identifiers,
    requirementTagsDropdown: requirementTags
  } = requirementModule();

  const ensureRowLookups = once(() => settle([
    () => statuses.load("Requirement Status"),
    () => priorities.load("Requirement Priority")
  ]));

  const ensureFilterLookups = once(() => settle([
    () => projects.load(),
    () => customers.load(),
    () => employees.load(user?.siteId),
    () => types.load("Requirement Type"),
    () => identifiers.load("Requirement Identifier"),
    () => approvalStatuses.load("Approval Status"),
    () => areas.load("Area"),
    () => workspaces.load("Workspace"),
    () => requirementTags.load()
  ]));

  // Modules belong to projects: the list follows the project filter.
  const loadModules = (projectIds) => {
    if (!projectIds?.length) {
      modules.list.value = [];
      return Promise.resolve();
    }
    return modules.load(false, false, projectIds).catch((error) => console.error(error));
  };

  const ensureTagEditor = once(() => tags.load());

  const manageDropDownTypes = ref([]);
  const ensureManageDropdownTypes = once(async () => {
    manageDropDownTypes.value = (await getDropdownTypesByModuleNameForDropdown("SDLC")) || [];
  });

  // Saving tags can create new ones, so the filter's tag list is refreshed afterwards.
  const reloadRequirementTags = () => requirementTags.load().catch((error) => console.error(error));

  return {
    projects,
    modules,
    customers,
    employees,
    areas,
    workspaces,
    tags,
    statuses,
    priorities,
    approvalStatuses,
    types,
    identifiers,
    requirementTags,
    manageDropDownTypes,
    ensureRowLookups,
    ensureFilterLookups,
    loadModules,
    ensureTagEditor,
    ensureManageDropdownTypes,
    reloadRequirementTags
  };
}
