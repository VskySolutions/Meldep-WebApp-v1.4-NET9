import { useMultiSelectDropdown, useSingleSelectDropdown } from "composables/form-inputs/useDropdown.js";
import commonService from "services/common.service";
import requirementService from "modules/requirement-v2/requirementV2.service";

export default function requirementModule () {
  const requirementStatusForDropdown = useMultiSelectDropdown(commonService.getDropDown, {
    labelKey: "dropdownValue",
    valueKey: "id"
  });

  const requirementsByProjectModuleIdForDropdown = useMultiSelectDropdown(requirementService.getAllRequirementListForDropdown, {
    labelKey: "text",
    valueKey: "value"
  });

  const requirementByProjectModuleIdForDropdownSingleSelect = useSingleSelectDropdown(requirementService.getAllRequirementListForDropdown, {
    labelKey: "text",
    valueKey: "value"
  });

  // single select (the sort order rides along in `data`: the KPI strip and the board keep the site's order)
  const requirementStatusDropdownSingleSelect = useSingleSelectDropdown(commonService.getDropDown, {
    labelKey: "dropdownValue",
    valueKey: "id",
    colorKey: "color",
    bgColorKey: "bgColor",
    dataKey: "sortOrder"
  });

  const requirementPriorityDropdownSingleSelect = useSingleSelectDropdown(commonService.getDropDown, {
    labelKey: "dropdownValue",
    valueKey: "id",
    colorKey: "color",
    bgColorKey: "bgColor",
    dataKey: "sortOrder"
  });

  const requirementTagsDropdown = useMultiSelectDropdown(requirementService.getAllRequirementTagListForDropdown, {
    labelKey: "text",
    valueKey: "value",
    colorKey: "color",
    bgColorKey: "bgColor"
  });

  const requirementIdentifiedUserTypeDropdownSingleSelect = useSingleSelectDropdown(commonService.getDropDown, {
    labelKey: "dropdownValue",
    valueKey: "id"
  });

  const requirementApprovalStatusDropdownSingleSelect = useSingleSelectDropdown(commonService.getDropDown, {
    labelKey: "dropdownValue",
    valueKey: "id"
  });

  const requirementTypeDropdownSingleSelect = useSingleSelectDropdown(commonService.getDropDown, {
    labelKey: "dropdownValue",
    valueKey: "id"
  });

  const requirementTypeForDropdown = useSingleSelectDropdown(commonService.getDropDown, {
    labelKey: "dropdownValue",
    valueKey: "id"
  });

  const noteTypeListSingleSelect = useSingleSelectDropdown(commonService.getDropDown, {
    labelKey: "dropdownValue",
    valueKey: "id",
    dataKey: "sortOrder"
  });

  return {
    requirementStatusForDropdown,
    requirementsByProjectModuleIdForDropdown,
    requirementByProjectModuleIdForDropdownSingleSelect,
    requirementStatusDropdownSingleSelect,
    requirementPriorityDropdownSingleSelect,
    requirementTagsDropdown,
    requirementIdentifiedUserTypeDropdownSingleSelect,
    requirementApprovalStatusDropdownSingleSelect,
    requirementTypeDropdownSingleSelect,
    requirementTypeForDropdown,
    noteTypeListSingleSelect
  };
}
