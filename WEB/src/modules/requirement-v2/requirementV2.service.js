// The requirement pages (v2): list, details, create and edit. Only the reads those pages added live on
// the v2 API (/requirement-v2, /notes-v2); every write goes to the same endpoints the current screens use.
import { http } from "boot/axios";

export default {
  // ---- v2 reads ----

  // One page of the list: { data, total }. Takes the current list's filters plus the ones the v2 list adds.
  // `config` reaches axios (signal, silent).
  getAllRequirement (model, config = {}) {
    return http.post("/requirement-v2/list", model, config).then(response => response.data);
  },

  // The counts of the strip above the list, over the same filters as the list.
  getRequirementSummary (model) {
    return http.post("/requirement-v2/summary", model).then(response => response.data);
  },

  // The quick search: the few requirements in the reader's sight whose number, title, project or module
  // match the text, best match first: [{ id, requirementNumber, title, projectName, projectModuleName,
  // statusLabel, statusColor, statusBgColor }]. `config` reaches axios (signal, silent).
  searchRequirements (text, take = 8, config = {}) {
    return http.get("/requirement-v2/quick-search", { params: { text, take }, ...config }).then(response => response.data || []);
  },

  // The requirement plus what the signed-in reader may do on it (project.currentUserManage /
  // currentUserNotes) and their own pin, colour and tags.
  getRequirementDetails (id) {
    return http.get(`/requirement-v2/details/${id}`).then(response => response.data);
  },

  // Tasks / issues / test cases / Q&A / action items / hours / notes / log / documents / mappings of one requirement.
  getRequirementRelatedCounts (id) {
    return http.get(`/requirement-v2/related-counts/${id}`).then(response => response.data);
  },

  // One page of the requirement log, newest first, the original description being the oldest entry of all:
  // { data, hasMore, total, hasDescription }. `beforeId` is the oldest entry already shown.
  getRequirementLogThread (id, { beforeId = null, take = 10 } = {}, config = {}) {
    return http.get("/requirement-v2/requirement-change-log/thread", { params: { id, beforeId, take }, ...config }).then(response => ({
      data: response.data?.items || [],
      hasMore: !!response.data?.hasMore,
      total: response.data?.total ?? 0,
      hasDescription: !!response.data?.hasDescription
    }));
  },

  // One page of a record's notes, newest first: { data, hasMore, total, countsByType }. `beforeId` is the
  // oldest note already shown. `config` reaches axios ({ silent: true } keeps the global error toast quiet).
  getNoteThread (subModuleId, type, { noteTypeId = null, beforeId = null, take = 10 } = {}, config = {}) {
    return http.get("/notes-v2/thread", { params: { subModuleId, type, noteTypeId, beforeId, take }, ...config }).then(response => response.data);
  },

  // ---- Reads shared with the current screens ----

  getAllRequirementListForDropdown (projectModuleId, projectId, requirementType) {
    return http.get(`/requirement/dropdown/list?projectModuleId=${projectModuleId}&projectId=${projectId}&requirementType=${requirementType}`).then(response => response.data);
  },

  getAllRequirementTagListForDropdown () {
    return http.get("/requirement/requirementTags/dropdown/list").then(response => response.data);
  },

  getRequirementWorkProgressNotes (requirementId) {
    return http.get(`/requirement/workProgressNotes?requirementId=${requirementId}`).then(response => response.data);
  },

  getAllRequirementDescriptionsById (id, latestOnTop) {
    return http.get(`/requirement/requirement-change-log/?id=${id}&latestOnTop=${latestOnTop}`).then(response => response.data);
  },

  // The data mappings of a requirement, newest first, each with its direction and its number of notes
  // (dataMappingNotesCount).
  getDataMappings (requirementId, config = {}) {
    return http.get(`/requirement-data-mappings/get-data-mappings/${requirementId}`, config).then(response => response.data?.requirementDataMappingsList || []);
  },

  // Status, priority and date change history: one entry per change, with the note given for it.
  getRequirementChangeHistory (id) {
    const columns = "Requirement Status,Requirement Priority,Planned Start Date,Planned End Date,Actual Start Date,Actual End Date";
    return http.get(`/Sites/?subModuleId=${id}&columnNames=${encodeURIComponent(columns)}`).then(response => response.data);
  },

  // ---- Writes (the current endpoints) ----

  // Create (no id) or update. `config` reaches axios: `{ silent: true }` keeps the global error toast
  // quiet (the auto-saving edit page reports a failure in place instead).
  saveRequirement (id, model, config = {}) {
    if (id) {
      return http.put(`/requirement/${id}`, model, config).then(response => response.data);
    } else {
      return http.post("/requirement", model, config).then(response => response.data);
    }
  },

  saveTags (model) {
    return http.post("/requirement/tags", model).then(response => response.data);
  },

  saveResponseLogDescription (model) {
    return http.post("/requirement/requirement-change-log/add-edit-description", model).then(response => response.data);
  },

  updateRequirementStatus (model) {
    return http.post("/requirement/updateRequirementStatus", model).then(response => response.data);
  },

  updateRequirementPriority (model) {
    return http.post("/requirement/updateRequirementPriority", model).then(response => response.data);
  },

  updateRequirementIsPinned (id, pinstatus) {
    return http.put(`/requirement/pinstatus/${id}/${pinstatus}`).then(response => response.data);
  },

  updateRequirementColor (id, requirementColor) {
    return http.put(`/requirement/requirementColor/${id}/${requirementColor}`).then(response => response.data);
  },

  updateRequirementDescription (model) {
    return http.put("/requirement/requirementDescription", model).then(response => response.data);
  },

  // One date at a time, with the reason for the change: { requirementId, <field>Str: "MM-DD-YYYY", <field>Note }.
  // The server writes the change and its note to the date change log.
  updateRequirementDates (model) {
    const payload = { ...model };
    for (const field of ["plannedStartDateStr", "plannedEndDateStr", "actualStartDateStr", "actualEndDateStr"]) {
      if (payload[field]) payload[field] = payload[field].replace(/\//g, "-");
    }
    return http.put("/requirement/update-dates", payload).then(response => response.data);
  },

  // Mappings of one requirement, any number in a call: { requirementId, projectId, projectModuleId,
  // dataMappings: [{ id, source, target, integrationDirectionId, deleted }] }. A row without an id is
  // created, one sent with `deleted` is removed, the rest are updated. Answers the saved rows (a new one
  // with its id).
  saveDataMappings (model, config = {}) {
    return http.post("/requirement-data-mappings/save-data-mappings", model, config).then(response => response.data);
  },

  deleteRequirement (id) {
    return http.delete(`/requirement/${id}`).then(response => response.data);
  },

  deleteRequirementChangeLog (id) {
    return http.delete(`/requirement/requirement-change-log/${id}`).then(response => response.data);
  }
};
