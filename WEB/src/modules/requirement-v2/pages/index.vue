<template>
  <q-page padding class="req-page">
    <!-- One top card: the bar, then (foldable, as on the Analytics registers) the status tiles with the
         attention flags after them. Folded, only the bar is left and the list moves up into the room. -->
    <app-list-header
      v-model:collapsed="collapsed"
      collapsible
      collapse-hint="Hide the status tiles and attention flags to give the list the whole screen"
      expand-hint="Show the status tiles and attention flags"
      :breadcrumbs="breadcrumbs"
      :search="search.searchText"
      show-search
      search-placeholder="Search number, title, project, status, people or tags"
      show-filters
      :filter-count="filterCount"
      :show-add="!isViewer"
      add-label="Create Requirement"
      :show-back="showBack"
      @update:search="search.searchText = $event"
      @filters="openFilters"
      @add="onAdd"
      @back="$router.back()"
    >
      <template #note>
        Every requirement of the projects you can see. The tiles count them by status and the chips under
        them by what needs attention; click one to narrow the list. Click a row to open the requirement's
        page, and tick rows for bulk changes. A status or date change asks for an optional note, kept in
        that field's change log. The arrow on the right folds the tiles and chips away so the list gets
        the whole screen; the Filters button keeps counting whatever is narrowing the list.
      </template>
      <!-- The folding part: the status tiles (.rss), then the attention flags (.rss-flags). -->
      <template #body>
        <requirement-summary-strip
          :summary="summary"
          :loading="summaryLoading"
          :filters="search"
          :statuses="lookups.statuses.list.value"
          @toggle-status="toggleStatus"
          @toggle-flag="toggleFlag"
          @clear-quick="clearQuick"
        />
      </template>
      <template #actions>
        <q-btn-dropdown outline no-caps color="primary" icon="o_download" label="Export" aria-label="Export">
          <q-list dense style="min-width: 240px">
            <q-item v-close-popup clickable :disable="!rows.length" @click="exportRows(rows, 'requirements')">
              <q-item-section avatar><q-icon name="o_table_view" size="18px" /></q-item-section>
              <q-item-section>This page (CSV)</q-item-section>
            </q-item>
            <q-item v-close-popup clickable :disable="!totalRecords || exporting" @click="exportAll">
              <q-item-section avatar><q-icon name="o_cloud_download" size="18px" /></q-item-section>
              <q-item-section>
                All matching (CSV)
                <q-item-label caption>Up to {{ EXPORT_LIMIT.toLocaleString() }} rows</q-item-label>
              </q-item-section>
            </q-item>
          </q-list>
        </q-btn-dropdown>
        <q-btn
          v-if="isAdmin"
          outline
          no-caps
          color="primary"
          icon="o_tune"
          aria-label="Manage dropdown values"
          @click="openManageDropdowns"
        >
          <q-tooltip>Manage dropdown values</q-tooltip>
          <manage-dropdown-options v-model="showManageDropdowns" :manage-drop-down-types="manageDropDownTypes" />
        </q-btn>
      </template>
    </app-list-header>

    <!-- The active-filter chips fold with the header; the Filters button keeps their count. -->
    <requirement-filter-drawer v-model="filterOpen" v-model:search="search" :lookups="lookups" :filters="filters" :show-chips="!collapsed" />

    <!-- The list: compact rows. -->
    <app-data-table
      page-key="requirements-v2"
      row-key="id"
      title="All requirements"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :total-records="totalRecords"
      :pagination="pagination"
      :rows-per-page-options="[20, 50, 100, 200, 500]"
      default-sort-by="createdOnUtc"
      :selectable="!isViewer"
      :selected="selectedRows"
      :selection-count="selectionCount"
      :pinned-row-keys="pinnedRowKeys"
      :row-colours="rowColours"
      :row-class="rowClass"
      dense
      separator="cell"
      @request="onRequest"
      @refresh="load"
      @update:selected="onSelectedChange"
    >
      <template #actions>
        <q-btn flat round dense color="grey-8" icon="o_sort" aria-label="Multi-column sort" @click="showSortDialog = true">
          <q-badge v-if="selectedSortCount" floating color="primary">{{ selectedSortCount }}</q-badge>
          <q-tooltip>Sort by several columns</q-tooltip>
        </q-btn>
      </template>

      <template #bulk-actions>
        <q-btn flat dense no-caps color="primary" icon="o_flag" label="Status" :disable="hasNonEditableSelection" @click="runBulkChange('Status')">
          <q-tooltip>Set one status on every selected requirement</q-tooltip>
        </q-btn>
        <q-btn flat dense no-caps color="primary" icon="o_priority_high" label="Priority" :disable="hasNonEditableSelection" @click="runBulkChange('Priority')">
          <q-tooltip>Set one priority on every selected requirement</q-tooltip>
        </q-btn>
        <q-btn flat dense no-caps color="primary" icon="o_label" label="Add tags" :disable="hasNonEditableSelection" @click="runBulkChange('Tags')">
          <q-tooltip>Add tags, keeping the ones already there</q-tooltip>
        </q-btn>
        <q-btn flat dense no-caps color="primary" icon="o_push_pin" label="Pin" @click="bulkPin(true)">
          <q-tooltip>Pin the selected requirements to the top, for you</q-tooltip>
        </q-btn>
        <q-btn flat dense no-caps color="grey-8" icon="o_push_pin" label="Unpin" @click="bulkPin(false)" />
        <q-btn flat dense no-caps color="primary" icon="o_swap_horiz" label="Convert into tasks" :disable="hasNonEditableSelection" :loading="converting" @click="convertSelectedToTasks" />
        <q-btn flat dense no-caps color="primary" icon="o_download" label="Export" @click="exportRows(selectedRows, 'selected-requirements')">
          <q-tooltip>CSV of the selected rows on this page</q-tooltip>
        </q-btn>
        <q-btn flat dense no-caps color="negative" icon="o_delete" label="Delete" :disable="hasNonEditableSelection" @click="bulkDelete" />
        <q-btn flat dense no-caps color="grey-8" icon="o_close" label="Clear selection" @click="selection.clear()" />
        <span v-if="hasNonEditableSelection" class="text-caption text-orange-9">
          Some selected requirements are view-only for you, so the write actions are off.
        </span>
      </template>

      <!-- Requirement: number and title (opens the record). Compact rows leave the short description to
           its own column. -->
      <template #body-cell-title="cell">
        <q-td :props="cell">
          <requirement-title-cell :row="cell.row" :show-description="false" @open="markActive" />
        </q-td>
      </template>

      <!-- Project: the name opens the project, the target opens its Project Center. Rows that follow one
           another in the same project show the name once, on the first of them (as the old list does). -->
      <template #[cellSlot.project]="cell">
        <q-td :props="cell">
          <template v-if="runStarts[cell.row.id]?.project !== false">
            <a class="req-link" role="button" tabindex="0" @click.stop="onProjectView(cell.row.project?.id)" @keyup.enter="onProjectView(cell.row.project?.id)">
              {{ cell.row.project?.name || "—" }}
            </a>
            <q-btn flat round dense size="sm" color="grey-7" icon="o_radio_button_checked" class="req-cell-btn" @click.stop="goToProjectCenter(cell.row)">
              <q-tooltip>Project Center</q-tooltip>
            </q-btn>
          </template>
          <span v-else class="req-sr">{{ cell.row.project?.name }}</span>
        </q-td>
      </template>

      <template #[cellSlot.module]="cell">
        <q-td :props="cell">
          <a v-if="runStarts[cell.row.id]?.module !== false" class="req-link" role="button" tabindex="0" @click.stop="onProjectModuleView(cell.row.projectModule?.id)" @keyup.enter="onProjectModuleView(cell.row.projectModule?.id)">
            {{ cell.row.projectModule?.name || "—" }}
          </a>
          <span v-else class="req-sr">{{ cell.row.projectModule?.name }}</span>
        </q-td>
      </template>

      <template #body-cell-shortDescription="cell">
        <q-td :props="cell">
          <div class="req-ellipsis">
            {{ cell.row.shortDescriptionText || "—" }}
            <q-tooltip v-if="cell.row.shortDescriptionText" max-width="420px" :delay="300">{{ cell.row.shortDescriptionText }}</q-tooltip>
          </div>
        </q-td>
      </template>

      <template #body-cell-projectTaskRelatedMappings="cell">
        <q-td :props="cell">
          <template v-if="cell.row.projectTaskRelatedMappings?.length">
            <q-chip
              v-for="task in cell.row.projectTaskRelatedMappings"
              :key="task.id"
              dense
              clickable
              square
              size="sm"
              color="grey-3"
              text-color="grey-9"
              class="req-task-chip"
              @click.stop="onProjectTaskView(task.id)"
            >
              #{{ task.projectTaskNumber }}
              <q-tooltip v-if="task.status">{{ task.status.dropDownValue }}</q-tooltip>
            </q-chip>
          </template>
          <span v-else class="text-grey-6">—</span>
        </q-td>
      </template>

      <template #[cellSlot.status]="cell">
        <q-td :props="cell">
          <app-badge-select
            :model-value="cell.row.status?.id ?? null"
            :options="statusOptions"
            :fallback="dropDownBadge(cell.row.status)"
            :readonly="!cell.row.isEditable"
            :saving="busy.id === cell.row.id && busy.field === 'status'"
            header="Set status"
            @update:model-value="(value) => setStatus(cell.row, value)"
          />
          <q-btn
            v-if="cell.row.status?.dropDownValue && hasChangeLog(cell.row, 'Requirement Status')"
            flat round dense size="xs" color="grey-6" icon="o_history" class="req-cell-btn"
            @click.stop="onSiteModifiedLog(cell.row.id, cell.row.status.dropDownValue, 'Requirement Status')"
          >
            <q-tooltip>Status change log</q-tooltip>
          </q-btn>
        </q-td>
      </template>

      <template #[cellSlot.priority]="cell">
        <q-td :props="cell">
          <!-- Plain text; for those who may change it, a click opens the list of priorities. -->
          <span
            v-if="cell.row.isEditable"
            class="req-plain-pick"
            role="button"
            tabindex="0"
            :aria-label="`Priority: ${cell.row.priority?.dropDownValue || 'none'}. Change`"
            @click.stop
            @keydown.enter.prevent="$event.currentTarget.click()"
          >
            {{ cell.row.priority?.dropDownValue || "—" }}
            <q-spinner v-if="busy.id === cell.row.id && busy.field === 'priority'" size="12px" color="primary" class="q-ml-xs" />
            <q-menu anchor="bottom left" self="top left">
              <q-list dense class="req-pick-menu">
                <q-item-label header class="q-py-xs">Set priority</q-item-label>
                <q-item
                  v-for="option in priorityOptions"
                  :key="option.value"
                  v-close-popup
                  clickable
                  :active="option.value === cell.row.priority?.id"
                  active-class="req-pick-menu__on"
                  @click="option.value !== cell.row.priority?.id && setPriority(cell.row, option.value)"
                >
                  <q-item-section>{{ option.label }}</q-item-section>
                  <q-item-section v-if="option.value === cell.row.priority?.id" side><q-icon name="o_check" size="16px" /></q-item-section>
                </q-item>
              </q-list>
            </q-menu>
          </span>
          <span v-else>{{ cell.row.priority?.dropDownValue || "—" }}</span>
        </q-td>
      </template>

      <!-- Owner: the initials are enough in the list; the full name is on the disc's tooltip. -->
      <template #[cellSlot.owner]="cell">
        <q-td :props="cell">
          <app-avatar :name="cell.row.ownerName" :size="24" empty="No owner" />
        </q-td>
      </template>

      <template #[cellSlot.customerOwner]="cell">
        <q-td :props="cell">
          <app-avatar :name="personName(cell.row.customerOwner)" :size="24" show-name empty="—" />
        </q-td>
      </template>

      <template #[cellSlot.identifiedBy]="cell">
        <q-td :props="cell">
          <app-avatar :name="identifiedBy(cell.row)" :size="24" show-name empty="—" />
        </q-td>
      </template>

      <!-- The four planned / actual dates: changed here with a note, each with its change log. -->
      <template v-for="def in LOGGED_DATES" :key="def.field" #[`body-cell-${def.field}`]="cell">
        <q-td :props="cell">
          <requirement-date-cell
            :date="cell.row[def.field] || ''"
            :log="def.log"
            :has-log="hasChangeLog(cell.row, def.log)"
            :editable="cell.row.isEditable"
            :saving="busy.id === cell.row.id && busy.field === def.field"
            :due="def.field === 'plannedEndDate'"
            :status-label="cell.row.status?.dropDownValue || ''"
            :min-date="def.after ? (cell.row[def.after] || '') : ''"
            @change="(value) => setDate(cell.row, def, value)"
            @log="onSiteModifiedLog(cell.row.id, cell.row.title, def.log)"
          />
        </q-td>
      </template>

      <template #[cellSlot.approval]="cell">
        <q-td :props="cell">
          <app-option-badge v-if="cell.row.approvalStatusDropDown?.dropDownValue" :option="approvalBadge(cell.row)" />
          <span v-else class="text-grey-6">—</span>
        </q-td>
      </template>

      <template #body-cell-requirementTags="cell">
        <q-td :props="cell">
          <requirement-tags-cell
            :tags="cell.row.requirementTags"
            :row-id="cell.row.id"
            :editable="cell.row.isEditable"
            :available-tags="tags.list.value"
            @before-edit="lookups.ensureTagEditor"
            @filter="tags.filter"
            @save="(value) => saveTags(cell.row, value)"
            @remove="(tag) => removeTag(cell.row, tag)"
          />
        </q-td>
      </template>

      <template #body-cell-lastNote="cell">
        <q-td :props="cell">
          <a v-if="cell.row.lastNoteText" class="req-link req-ellipsis" role="button" tabindex="0" @click.stop="openNotes(cell.row)" @keyup.enter="openNotes(cell.row)">
            {{ cell.row.lastNoteText }}
            <q-tooltip max-width="420px" :delay="300">{{ cell.row.lastNoteText }}<br><span class="text-grey-4">Click to open the notes</span></q-tooltip>
          </a>
          <span v-else class="text-grey-6">—</span>
        </q-td>
      </template>

      <!-- One button per action; the rarer ones under ⋮. -->
      <template #body-cell-actions="cell">
        <q-td :props="cell">
          <!-- View is a real link, so it can be opened in a new tab. -->
          <q-btn flat dense color="primary" icon="o_visibility" class="req-act" :to="{ name: 'requirement-v2-detail', params: { id: cell.row.id } }" @click.stop="markActive(cell.row)">
            <q-tooltip>View</q-tooltip>
          </q-btn>
          <q-btn v-if="cell.row.isEditable && !isViewer" flat dense icon="o_edit" class="req-act" @click.stop="goToEdit(cell.row)">
            <q-tooltip>Edit</q-tooltip>
          </q-btn>
          <q-btn v-if="cell.row.isEditable && !isViewer" flat dense icon="o_history_edu" class="req-act" @click.stop="goToRecordPage(cell.row, 'log')">
            <q-tooltip>Requirement Log</q-tooltip>
          </q-btn>
          <q-btn v-if="cell.row.isEditable || cell.row.isNotes" flat dense icon="o_sticky_note_2" class="req-act" @click.stop="openNotes(cell.row)">
            <q-badge v-if="cell.row.requirementNotesCount > 0" floating color="positive" :label="cell.row.requirementNotesCount" />
            <q-tooltip>Notes</q-tooltip>
          </q-btn>
          <!-- This one requirement as an .xlsx workbook, whatever is selected or filtered. -->
          <q-btn
            flat dense icon="o_file_download" class="req-act" aria-label="Export Requirement"
            :loading="requirementExport.isExporting(cell.row.id)"
            :disable="requirementExport.isExporting(cell.row.id)"
            @click.stop="requirementExport.exportRow(cell.row)"
          >
            <q-tooltip>Export Requirement</q-tooltip>
          </q-btn>
          <q-btn flat dense color="primary" icon="o_more_vert" class="req-act" aria-label="More actions" @click.stop>
            <q-tooltip>More</q-tooltip>
            <q-menu anchor="bottom right" self="top right">
              <q-list dense class="req-menu">
                <q-item v-close-popup clickable @click="copyLink(cell.row)">
                  <q-item-section avatar><q-icon name="o_link" size="18px" /></q-item-section>
                  <q-item-section>Copy link</q-item-section>
                </q-item>
                <q-separator v-if="cell.row.isEditable" />
                <q-item v-if="cell.row.isEditable" v-close-popup clickable @click="onConvertToTask(cell.row)">
                  <q-item-section avatar><q-icon name="o_swap_horiz" size="18px" /></q-item-section>
                  <q-item-section>Convert into task</q-item-section>
                </q-item>
                <q-item
                  v-if="cell.row.isEditable && cell.row.requirementType?.dropDownValue === 'Data Integration'"
                  v-close-popup clickable @click="onEditRequirementDataMapping(cell.row.id, cell.row.projectId, cell.row.projectModuleId, true, load)"
                >
                  <q-item-section avatar><q-icon name="o_account_tree" size="18px" /></q-item-section>
                  <q-item-section>Edit data mapping</q-item-section>
                </q-item>
                <q-separator />
                <q-item v-close-popup clickable @click="onSubmitRequirementPinned(cell.row.id, !cell.row.isPinned, load)">
                  <q-item-section avatar><q-icon name="o_push_pin" size="18px" :color="cell.row.isPinned ? 'primary' : undefined" /></q-item-section>
                  <q-item-section>{{ cell.row.isPinned ? "Unpin" : "Pin to top" }}</q-item-section>
                </q-item>
                <q-item v-if="cell.row.isEditable" clickable>
                  <q-item-section avatar>
                    <q-icon name="o_palette" size="18px" :style="cell.row.requirementColor ? { color: cell.row.requirementColor } : {}" />
                  </q-item-section>
                  <q-item-section>Colour</q-item-section>
                  <q-item-section side><q-icon name="o_chevron_right" size="18px" /></q-item-section>
                  <q-menu anchor="top end" self="top start" :offset="[4, 0]">
                    <div class="q-pa-sm">
                      <div class="text-caption text-grey-7 q-mb-xs">Colour this row — private to you</div>
                      <div class="req-swatches">
                        <button
                          v-for="swatch in SWATCHES"
                          :key="swatch"
                          v-close-popup="2"
                          type="button"
                          class="req-swatch"
                          :class="{ 'req-swatch--on': sameColour(cell.row.requirementColor, swatch) }"
                          :style="{ backgroundColor: swatch }"
                          :aria-label="`Colour ${swatch}`"
                          @click="setColour(cell.row, swatch)"
                        />
                      </div>
                    </div>
                  </q-menu>
                </q-item>
                <template v-if="cell.row.isEditable">
                  <q-separator />
                  <q-item v-close-popup clickable class="text-negative" @click="onSubmitRequirementDelete(cell.row.id, cell.row.title, load)">
                    <q-item-section avatar><q-icon name="o_delete" size="18px" color="negative" /></q-item-section>
                    <q-item-section>Delete</q-item-section>
                  </q-item>
                </template>
              </q-list>
            </q-menu>
          </q-btn>
        </q-td>
      </template>

      <template #no-data>
        <!-- The empty state waits for the first answer: "no requirements yet" must not flash while loading. -->
        <div v-if="loading" class="full-width row flex-center q-pa-xl text-grey-6">Loading requirements…</div>
        <app-empty-state
          v-else
          icon="o_assignment"
          :title="hasAnyFilter ? 'No matching requirements' : 'No requirements yet'"
          :text="hasAnyFilter ? 'Try clearing the filters or the search.' : (isViewer ? '' : 'Create the first requirement for one of your projects.')"
        >
          <q-btn v-if="hasAnyFilter" flat no-caps color="primary" icon="o_filter_alt_off" label="Clear filters and search" @click="clearEverything" />
          <q-btn v-else-if="!isViewer" unelevated no-caps color="primary" icon="o_add" label="Create Requirement" @click="onAdd" />
        </app-empty-state>
      </template>
    </app-data-table>

    <multi-column-sorting-dialog
      v-model="showSortDialog"
      :columns="sortDialogColumns"
      :multi-sort="multiSort"
      :exclude-columns="['Tasks', 'Tags', 'Notes']"
      @add="addSortLevel"
      @remove="removeSortLevel"
      @apply="applyMultiSort"
    />
  </q-page>
</template>

<script setup>
// The requirement list (v2): a server-paged, server-sorted table of compact rows over every requirement
// the signed-in user may see, under one top card whose status tiles and counts fold away (as on the
// Analytics registers); quick status / priority / date edits on the row, persisted multi-select for
// bulk actions and CSV export. Records, creation and editing are pages of their own.
import { ref, reactive, computed, watch, onMounted } from "vue";
import { useQuasar, debounce } from "quasar";
import { useRoute, useRouter } from "vue-router";
import { useAuthStore } from "stores/auth";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { usePreferences } from "composables/usePreferences";
import { exportCsv } from "composables/useCsvExport";

import AppListHeader from "components/common/AppListHeader.vue";
import AppDataTable from "components/common/AppDataTable.vue";
import AppBadgeSelect from "components/common/AppBadgeSelect.vue";
import AppOptionBadge from "components/common/AppOptionBadge.vue";
import AppAvatar from "components/common/AppAvatar.vue";
import AppEmptyState from "components/common/AppEmptyState.vue";
import RequirementFilterDrawer from "modules/requirement-v2/components/RequirementFilterDrawer.vue";
import RequirementSummaryStrip from "modules/requirement-v2/components/RequirementSummaryStrip.vue";
import RequirementTitleCell from "modules/requirement-v2/components/RequirementTitleCell.vue";
import RequirementDateCell from "modules/requirement-v2/components/RequirementDateCell.vue";
import RequirementTagsCell from "modules/requirement-v2/components/RequirementTagsCell.vue";
import multiColumnSortingDialog from "src/components/dataTable/_multiColumnSortingDialog.vue";
import manageDropdownOptions from "src/components/dataTable/_manageDropdownOptions.vue";

import requirementService from "modules/requirement-v2/requirementV2.service";
import taskService from "modules/project-tasks/projectTasks.service";
import useSiteTableState from "composables/dataTable/useSiteTableState.js";
import useMultiSort from "composables/dataTable/useMultiSort.js";
import { useRequirementLookups } from "modules/requirement-v2/useRequirementLookups";
import { useRequirementFilters, blankFilters, requirementNumberOf, LIST_FILTER_KEYS, TOGGLE_FILTER_KEYS } from "modules/requirement-v2/useRequirementFilters";
import { useRequirementSelection } from "modules/requirement-v2/useRequirementSelection";
import { toBadgeOptions, dropDownBadge, personName } from "modules/requirement-v2/utils/options";
import { askChangeNote, LOGGED_DATES } from "modules/requirement-v2/utils/changeNote";
import { stripHtml } from "src/utils/richText";
import legacyRequirementService from "modules/requirement/requirement.service";
import { createRequirementExportAction } from "modules/requirement/utils/requirementExport.js";
import { downloadBlob } from "src/composables/excelImport/downloadBlob.js";

import { initProjectDialogs, onProjectView } from "src/modules/project/utils/dialogs.js";
import { initSiteDialogs, onSiteModifiedLog } from "src/modules/sites/utils/dialogs.js";
import { initProjectModuleDialogs, onProjectModuleView } from "src/modules/project-modules/utils/dialogs.js";
import { initProjectTaskDialogs, onProjectTaskView } from "src/modules/project-tasks/utils/dialogs.js";
import { initRequirementDataMappingDialogs, onEditRequirementDataMapping } from "src/modules/requirement-data-mapping/utils/dialogs.js";
import { initRequirementDialogs } from "src/modules/requirement/utils/dialogs.js";
import {
  initRequirementActions,
  onSubmitRequirementPinned,
  onSubmitRequirementBulkPinned,
  onSubmitRequirementColor,
  onSubmitRequirementTags,
  onSubmitRequirementStatus,
  onSubmitRequirementPriority,
  onSubmitRequirementDate,
  onSubmitRequirementDelete,
  onSubmitRequirementBulkDelete,
  copyRequirementLink
} from "src/modules/requirement-v2/utils/actions.js";

const $q = useQuasar();
const route = useRoute();
const router = useRouter();
const notify = useNotify();
const { confirm } = useConfirm();
const user = useAuthStore().user;

const EXPORT_LIMIT = 2000;

// ---- Who is looking ----
const adminRoles = ["admin", "site-super-admin", "system-super-admin", "project admin"];
const isAdmin = user?.roles?.some((r) => adminRoles.includes(r)) ?? false;
const isViewer = user?.roles?.some((r) => r?.toLowerCase() === "viewer") ?? false;

// The project list links here with the project in the history state.
const cameFromProjectId = history.state?.projectId || null;

const toList = (value) => (value == null || value === "" ? [] : (Array.isArray(value) ? value : [value]));

// ---- Persisted list state (search, paging, sort, highlighted row), per site ----
const { search, pagination, activeRowId, sorts, saveDataTableState } = useSiteTableState({
  storageKey: "requirements-v2-Index",
  siteId: user?.siteId,
  defaultSearch: {
    searchText: "",
    ...blankFilters(),
    projectIds: toList(route.query.projectId),
    requirementGroupIds: toList(route.query.requirementGroupId)
  },
  defaultPagination: { sortBy: "createdOnUtc", descending: true, rowsPerPage: 20, page: 1 },
  defaultSorts: {},
  defaultResizableWidth: {},
  defaultColumns: []
});

// A saved state may predate this page: list filters must be arrays, toggles must exist, and the number
// box must not show the "0" the old page wrote into it as a "no filter" sentinel.
for (const key of LIST_FILTER_KEYS) {
  if (!Array.isArray(search.value[key])) search.value[key] = [];
}
for (const key of [...TOGGLE_FILTER_KEYS, "notApproved", "dueSoon"]) {
  if (search.value[key] === undefined) search.value[key] = null;
}
for (const key of ["plannedEndFromDate", "plannedEndToDate", "updatedFromDate", "updatedToDate"]) {
  if (search.value[key] === undefined) search.value[key] = null;
}
if (String(search.value.requirementNumber ?? "") === "0") search.value.requirementNumber = "";
if (cameFromProjectId && !(search.value.projectIds.length === 1 && search.value.projectIds[0] === cameFromProjectId)) {
  search.value.projectIds = [cameFromProjectId];
  search.value.projectModuleIds = [];
}

// ---- Reader preference: whether the top card's tiles and counts are folded away (remembered) ----
const prefs = usePreferences("requirements-v2");
const collapsed = ref(!!prefs.get("topCollapsed", false));
watch(collapsed, (value) => prefs.set("topCollapsed", value));

// ---- Lookups, filters, selection ----
const lookups = useRequirementLookups();
const { tags, manageDropDownTypes } = lookups;
const filters = useRequirementFilters(search, lookups);
const { activeCount: filterCount, hasLookupFilters } = filters;
const selection = useRequirementSelection();
const { count: selectionCount, hasNonEditable: hasNonEditableSelection } = selection;

const filterOpen = ref(false);
const showManageDropdowns = ref(false);
const showSortDialog = ref(false);

const statusOptions = computed(() => toBadgeOptions(lookups.statuses.list.value));
const priorityOptions = computed(() => toBadgeOptions(lookups.priorities.list.value));
const approvalBadge = (row) => dropDownBadge(row.approvalStatusDropDown);

// ---- Dialog / action helpers shared with other modules take the highlighted-row ref ----
initRequirementDialogs(activeRowId);
initProjectDialogs(activeRowId);
initProjectTaskDialogs(activeRowId);
initSiteDialogs(activeRowId);
initRequirementActions(activeRowId);
initProjectModuleDialogs(activeRowId);
initRequirementDataMappingDialogs();

// ---- Columns ----
// Names are the entity paths the server orders by (it takes `sortBy` as a dynamic ORDER BY), so they
// stay exactly as they were; the cells read through functions because the rows nest their values.
const text = (read) => (row) => read(row) || "—";
const identifiedBy = (row) =>
  (row.userType?.dropDownValue === "Customer" ? personName(row.customer) : personName(row.employee));

const columns = [
  { name: "title", label: "Requirement", field: "title", align: "left", sortable: true, default: true, width: 340 },
  { name: "project.name", label: "Project", field: text((r) => r.project?.name), align: "left", sortable: true, default: true, width: 180 },
  { name: "projectModule.name", label: "Module", field: text((r) => r.projectModule?.name), align: "left", sortable: true, default: true, width: 150 },
  { name: "status.dropDownValue", label: "Status", field: text((r) => r.status?.dropDownValue), align: "left", sortable: true, default: true, width: 160 },
  { name: "priority.dropDownValue", label: "Priority", field: text((r) => r.priority?.dropDownValue), align: "left", sortable: true, default: true, width: 140 },
  { name: "requirementOwner.person.firstName", label: "Owner", field: text((r) => r.ownerName), align: "left", sortable: true, default: true, width: 90 },
  { name: "plannedEndDate", label: "Due", field: text((r) => r.plannedEndDate), align: "left", sortable: true, default: true, width: 130 },
  { name: "requirementNumber", label: "Req. No.", field: (r) => (r.requirementNumber ? `#${r.requirementNumber}` : "—"), align: "left", sortable: true, default: false, width: 90 },
  { name: "shortDescription", label: "Short Description", field: (r) => r.shortDescriptionText, align: "left", sortable: true, default: false, width: 240 },
  { name: "projectTaskRelatedMappings", label: "Tasks", field: (r) => (r.projectTaskRelatedMappings || []).map((t) => `#${t.projectTaskNumber}`).join(", "), align: "left", sortable: false, default: false, width: 150 },
  { name: "requirementTags", label: "Tags", field: (r) => (r.requirementTags || []).map((t) => t.text).join(", "), align: "left", sortable: false, default: false, width: 220 },
  { name: "lastNote", label: "Notes", field: (r) => r.lastNoteText, align: "left", sortable: false, default: true, width: 240 },
  { name: "approvalStatusDropDown.dropDownValue", label: "Approval", field: text((r) => r.approvalStatusDropDown?.dropDownValue), align: "left", sortable: true, default: false, width: 140 },
  { name: "requirementType.dropDownValue", label: "Type", field: text((r) => r.requirementType?.dropDownValue), align: "left", sortable: true, default: false },
  // The labels a Data Integration requirement gives the two sides of its data mapping.
  { name: "sourceLabel", label: "Source Label", field: text((r) => r.sourceLabel), align: "left", sortable: true, default: false, width: 150 },
  { name: "targetLabel", label: "Target Label", field: text((r) => r.targetLabel), align: "left", sortable: true, default: false, width: 150 },
  { name: "area.dropDownValue", label: "Area", field: text((r) => r.area?.dropDownValue), align: "left", sortable: true, default: false },
  { name: "workspace.dropDownValue", label: "Workspace", field: text((r) => r.workspace?.dropDownValue), align: "left", sortable: true, default: false },
  { name: "userType.dropDownValue", label: "Identifier", field: text((r) => r.userType?.dropDownValue), align: "left", sortable: true, default: false },
  { name: "employee.person.firstName", label: "Identified By", field: text(identifiedBy), align: "left", sortable: true, default: false, width: 170 },
  { name: "identifiedDate", label: "Identified Date", field: text((r) => r.identifiedDate), align: "left", sortable: true, default: false, width: 130 },
  { name: "customerOwner.firstName", label: "Customer Owner", field: text((r) => personName(r.customerOwner)), align: "left", sortable: true, default: false, width: 170 },
  { name: "requirementEntered.person.firstName", label: "Entered By", field: text((r) => personName(r.requirementEntered)), align: "left", sortable: true, default: false, width: 160 },
  { name: "plannedStartDate", label: "Planned Start", field: text((r) => r.plannedStartDate), align: "left", sortable: true, default: false, width: 130 },
  { name: "actualStartDate", label: "Actual Start", field: text((r) => r.actualStartDate), align: "left", sortable: true, default: false, width: 130 },
  { name: "actualEndDate", label: "Actual End", field: text((r) => r.actualEndDate), align: "left", sortable: true, default: false, width: 130 },
  { name: "createdBy.person.firstName", label: "Created By", field: text((r) => personName(r.createdBy)), align: "left", sortable: true, default: false, width: 150 },
  { name: "createdOnUtc", label: "Created On", field: text((r) => r.createdOnUtc), align: "left", sortable: true, default: false, width: 170 },
  { name: "updatedBy.person.firstName", label: "Updated By", field: text((r) => personName(r.updatedBy)), align: "left", sortable: true, default: false, width: 150 },
  { name: "updatedOnUtc", label: "Updated On", field: text((r) => r.updatedOnUtc), align: "left", sortable: true, default: false, width: 170 },
  { name: "actions", label: "Actions", field: "actions", align: "left" }
];

// Column names carry dots, which a `#body-cell-x.y` shorthand would read as directive modifiers; the
// slot names are built here and bound dynamically.
const cellSlot = {
  project: "body-cell-project.name",
  module: "body-cell-projectModule.name",
  status: "body-cell-status.dropDownValue",
  priority: "body-cell-priority.dropDownValue",
  owner: "body-cell-requirementOwner.person.firstName",
  customerOwner: "body-cell-customerOwner.firstName",
  identifiedBy: "body-cell-employee.person.firstName",
  approval: "body-cell-approvalStatusDropDown.dropDownValue"
};

// The sort dialog reads a column's `field`; it is handed the server's names.
const sortDialogColumns = columns.filter((c) => c.name !== "actions").map((c) => ({ label: c.label, field: c.name }));

// ---- Rows ----
const rows = ref([]);
const loading = ref(false);
const totalRecords = ref(0);
const summary = ref(null);
const summaryLoading = ref(false);

// Everything a cell needs is worked out once per load, not once per render.
const toRow = (requirement) => ({
  ...requirement,
  isEditable: requirement.project?.currentUserManage === true,
  isNotes: requirement.project?.currentUserNotes === true,
  ownerName: personName(requirement.requirementOwner),
  shortDescriptionText: stripHtml(requirement.shortDescription),
  lastNoteText: stripHtml(requirement.lastNote),
  requirementTags: (requirement.requirementTags || []).map((tag) => ({
    text: tag.tags?.name,
    value: tag.tags?.id,
    color: tag.tags?.color,
    bgColor: tag.tags?.bgColor
  }))
});

const { multiSort, addSortLevel, removeSortLevel, applyMultiSort, selectedSortCount } = useMultiSort({
  lsSorts: sorts.value || null,
  saveDataTableState,
  onApplySort: () => reloadFromFirstPage()
});

const buildSorts = () =>
  Object.fromEntries(multiSort.value.filter((s) => s.column && s.direction).map((s) => [s.column, s.direction]));

// The filters as the API takes them (paging and sorting are added by the caller).
const buildFilterPayload = () => {
  const s = search.value;
  return {
    ...s,
    requirementNumber: requirementNumberOf(s.requirementNumber),
    fromDate: s.fromDate || null,
    toDate: s.toDate || null,
    plannedEndFromDate: s.plannedEndFromDate || null,
    plannedEndToDate: s.plannedEndToDate || null,
    updatedFromDate: s.updatedFromDate || null,
    updatedToDate: s.updatedToDate || null
  };
};

// Counts are a background read: a failure leaves the last strip in place and says nothing.
const loadSummary = async () => {
  summaryLoading.value = true;
  try {
    summary.value = await requirementService.getRequirementSummary(buildFilterPayload());
  } catch (error) {
    console.error("Failed to load the requirement summary", error);
  } finally {
    summaryLoading.value = false;
  }
};

const load = async () => {
  loading.value = true;
  const { page, rowsPerPage, sortBy, descending } = pagination.value;
  const sortMap = buildSorts();
  const payload = { ...buildFilterPayload(), page, pageSize: rowsPerPage, sortBy, descending, sorts: sortMap };
  saveDataTableState({ search: search.value, pagination: pagination.value, activeRowId: activeRowId.value, sorts: sortMap });

  loadSummary();
  try {
    const resp = await requirementService.getAllRequirement(payload);
    rows.value = (resp?.data || []).map(toRow);
    totalRecords.value = resp?.total ?? rows.value.length;
    pagination.value = { ...pagination.value, rowsNumber: totalRecords.value };
    selection.refreshEditable(rows.value);
  } catch (error) {
    // The interceptor has shown the server's message; the table keeps its last rows.
    console.error("Failed to load requirements", error);
  } finally {
    loading.value = false;
  }
};

// Filters and search always restart at page 1: a narrower list has no page 3.
const reloadFromFirstPage = debounce(() => {
  pagination.value = { ...pagination.value, page: 1 };
  load();
}, 300);

watch(search, () => reloadFromFirstPage(), { deep: true });

const onRequest = (next) => {
  pagination.value = { ...pagination.value, ...next };
  load();
};

// ---- Row decoration ----
// Whether a field of a row has a change log to open (the list API names the fields that do). An API
// that does not send the names yet is read as "may have one", as before.
const hasChangeLog = (row, column) => (Array.isArray(row.changeLogColumns) ? row.changeLogColumns.includes(column) : true);

const pinnedRowKeys = computed(() => rows.value.filter((r) => r.isPinned).map((r) => r.id));

// For each row, whether it starts a run of rows of the same project (and of the same module in it), in
// the order the table shows them: pinned rows first, then the rest. Only a run's first row shows the name.
const runStarts = computed(() => {
  const shown = [...rows.value.filter((r) => r.isPinned), ...rows.value.filter((r) => !r.isPinned)];
  const out = {};
  shown.forEach((row, index) => {
    const before = shown[index - 1];
    const project = !before || before.project?.id !== row.project?.id;
    out[row.id] = { project, module: project || before.projectModule?.id !== row.projectModule?.id };
  });
  return out;
});
const rowColours = computed(() =>
  Object.fromEntries(rows.value.filter((r) => r.requirementColor).map((r) => [r.id, r.requirementColor])));
const rowClass = (row) => (activeRowId.value === row.id ? "req-row--active" : "");

const setActiveRow = (id) => {
  activeRowId.value = id;
  saveDataTableState({ activeRowId: id });
};
watch(activeRowId, (id) => saveDataTableState({ activeRowId: id }));

// ---- Selection (persisted across pages) ----
const selectedRows = computed(() => rows.value.filter((r) => selection.isSelected(r.id)));
const onSelectedChange = (pageSelected) => selection.applyPageSelection(rows.value, pageSelected);

// ---- Header ----
const breadcrumbs = computed(() => [
  { label: "Home", icon: "o_home", to: user?.siteLandingPageLink || "/" },
  { label: "SDLC" },
  ...(search.value.requirementGroupIds?.length ? [{ label: "Requirement Groups", to: "/requirement-group" }] : []),
  { label: "Requirements" }
]);
const showBack = computed(() => !!cameFromProjectId || !!search.value.requirementGroupIds?.length);
const hasAnyFilter = computed(() => filterCount.value > 0 || !!search.value.searchText);

const openFilters = () => {
  filterOpen.value = true;
};

const clearEverything = () => {
  search.value.searchText = "";
  filters.clearAll();
};

// Creation is a page of its own, pre-set to the project / module the list is filtered to.
const onAdd = () => {
  if (isViewer) return;
  const projectId = search.value.projectIds?.[0];
  const moduleId = search.value.projectModuleIds?.[0];
  router.push({ name: "requirement-v2-create", query: { ...(projectId ? { projectId } : {}), ...(moduleId ? { moduleId } : {}) } });
};

const openManageDropdowns = async () => {
  await lookups.ensureManageDropdownTypes();
  showManageDropdowns.value = !showManageDropdowns.value;
};

// ---- Summary strip toggles ----
const toggleStatus = (id) => {
  const ids = search.value.statusIds || [];
  search.value.statusIds = ids.length === 1 && ids[0] === id ? [] : [id];
};

const sevenDaysAgo = () => {
  const d = new Date();
  d.setDate(d.getDate() - 7);
  return `${String(d.getMonth() + 1).padStart(2, "0")}/${String(d.getDate()).padStart(2, "0")}/${d.getFullYear()}`;
};

const toggleFlag = (key) => {
  const s = search.value;
  if (key === "hasTasks") s.hasTasks = s.hasTasks === false ? null : false;
  else if (key === "updatedFromDate") s.updatedFromDate = s.updatedFromDate ? null : sevenDaysAgo();
  else s[key] = s[key] === true ? null : true;
};

const clearQuick = () => {
  const s = search.value;
  s.statusIds = [];
  for (const key of [...TOGGLE_FILTER_KEYS, "notApproved", "dueSoon"]) s[key] = null;
  s.updatedFromDate = null;
};

// ---- Opening records (each on its own page) ----
// The row last opened stays marked, so coming back from the record lands the eye.
const markActive = (row) => setActiveRow(row.id);

const goToRecordPage = (row, tab) => {
  setActiveRow(row.id);
  router.push({ name: "requirement-v2-detail", params: { id: row.id }, query: tab ? { tab } : {} });
};

const goToEdit = (row) => {
  setActiveRow(row.id);
  router.push({ name: "requirement-v2-edit", params: { id: row.id } });
};

// A requirement opens from its number or title link and from View; a click on the rest of the row does nothing.

const copyLink = (row) => {
  const href = router.resolve({ name: "requirement-v2-detail", params: { id: row.id } }).href;
  copyRequirementLink(`${window.location.origin}${href}`);
};

const goToProjectCenter = (row) => {
  setActiveRow(row.id);
  router.push({ path: "/project-center", state: { projectId: row.project?.id } });
};

const openNotes = (row) => goToRecordPage(row, "notes");

// ---- Quick edits on the row ----
const busy = ref({ id: null, field: null });

// A status change may carry a note (optional), kept in the status change log. Cancelling the prompt
// leaves the status as it was.
const setStatus = async (row, statusId) => {
  const { ok, note } = await askChangeNote("Requirement Status Note", "Add a note for this status change (optional).");
  if (!ok) return;
  busy.value = { id: row.id, field: "status" };
  try {
    await onSubmitRequirementStatus(row.id, statusId, load, note);
  } finally {
    busy.value = { id: null, field: null };
  }
};

// A planned / actual date is saved on its own, with its note, and written to that date's change log.
const setDate = async (row, def, value) => {
  const { ok, note } = await askChangeNote(`${def.log} Note`, `Add a note for this ${def.log.toLowerCase()} change (optional).`);
  if (!ok) return;
  busy.value = { id: row.id, field: def.field };
  try {
    await onSubmitRequirementDate(row.id, value, def.field, note, load);
  } finally {
    busy.value = { id: null, field: null };
  }
};

const setPriority = async (row, priorityId) => {
  busy.value = { id: row.id, field: "priority" };
  try {
    await onSubmitRequirementPriority(row.id, priorityId, load);
  } finally {
    busy.value = { id: null, field: null };
  }
};

const saveTags =(row, value) => onSubmitRequirementTags(row.id, value, load, lookups.reloadRequirementTags);

const removeTag = async (row, tag) => {
  const ok = await confirm({
    title: "Remove tag",
    message: `Remove the tag "${tag.text}" from this requirement?`,
    confirmLabel: "Remove",
    type: "danger"
  });
  if (!ok) return;
  saveTags(row, row.requirementTags.filter((t) => t.value !== tag.value));
};

// The eight row colours of the standard; a colour is private to the reader's view of the list.
const SWATCHES = ["#ef5350", "#ec407a", "#ab47bc", "#5c6bc0", "#42a5f5", "#26a69a", "#9ccc65", "#ffa726"];
const sameColour = (a, b) => String(a || "").toLowerCase() === String(b || "").toLowerCase();
const setColour = (row, colour) => onSubmitRequirementColor(row.id, colour, true, row.requirementColor, load);

// ---- Convert into task(s) ----
const converting = ref(false);

const openTaskDialog = (componentProps) =>
  import("modules/project-tasks/components/addEdit.vue")
    .then((m) => $q.dialog({ component: m.default, componentProps }).onOk(() => load()));

const onConvertToTask = (row) => {
  setActiveRow(row.id);
  const taskNumbers = (row.projectTaskRelatedMappings || []).map((m) => m.projectTaskNumber).filter(Boolean);
  openTaskDialog({
    requirementId: row.id,
    projectIdAttr: row.project?.id,
    moduleIdAttr: row.projectModule?.id,
    name: row.title,
    isRequirementConverted: true,
    taskNumbers
  });
};

const escapeHtml = (value) =>
  String(value ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;", "'": "&#39;" }[c]));

// The selection may span pages the table cannot see; bulk work runs over the selected rows ON THIS PAGE.
const selectedOnThisPage = () => {
  const ids = new Set(selection.selectedIds.value);
  return rows.value.filter((r) => ids.has(r.id));
};

const convertSelectedToTasks = async () => {
  if (hasNonEditableSelection.value) {
    notify.warning("Some selected requirements are view-only for you.");
    return;
  }
  const selected = selectedOnThisPage();
  if (!selected.length) {
    notify.warning("The selected requirements are on another page. Open that page to convert them.");
    return;
  }
  if (selected.some((r) => !r.projectModuleId)) {
    notify.error("Every selected requirement needs a project module before it can be converted.");
    return;
  }

  const plural = selected.length === 1 ? "" : "s";
  let message = `<p>Convert ${selected.length} requirement${plural} on this page into task${plural}? ` +
    "Each task starts from the requirement's title; add the rest from the task list.</p>";
  const alreadyConverted = selected.filter((r) => r.projectTaskRelatedMappings?.length);
  if (alreadyConverted.length) {
    message += "<p><b>Already converted before:</b></p><ul>" +
      alreadyConverted.map((r) =>
        `<li>${escapeHtml(r.title)} → ${r.projectTaskRelatedMappings.map((m) => `#${m.projectTaskNumber}`).join(", ")}</li>`).join("") +
      "</ul>";
  }

  const ok = await confirm({ title: "Convert into tasks", message, html: true, confirmLabel: "Convert" });
  if (!ok) return;

  converting.value = true;
  try {
    await taskService.saveBulkTasks({
      isRequirementConverted: true,
      projectTaskModel: selected.map((r) => ({ flag: "Add", requirementId: r.id }))
    });
    notify.success(`${selected.length} task${plural} created.`, {
      caption: "Open the task list to add estimates, assignees and dates."
    });
    selection.clear();
    load();
  } catch (error) {
    console.error("Error converting requirements into tasks", error);
  } finally {
    converting.value = false;
  }
};

// ---- Bulk status / priority / tags / pin / delete ----
const runBulkChange = (field) => {
  if (hasNonEditableSelection.value) {
    notify.warning("Some selected requirements are view-only for you.");
    return;
  }
  import("modules/requirement-v2/components/_multiRequirementQuickActions.vue").then((m) => {
    $q.dialog({
      component: m.default,
      componentProps: { requirementIds: [...selection.selectedIds.value], selectedField: field }
    }).onOk(() => {
      selection.clear();
      load();
      if (field === "Tags") lookups.reloadRequirementTags();
    });
  });
};

const bulkPin = async (status) => {
  const ids = [...selection.selectedIds.value];
  if (!ids.length) return;
  await onSubmitRequirementBulkPinned(ids, status, load);
  selection.clear();
};

const bulkDelete = async () => {
  if (hasNonEditableSelection.value) {
    notify.warning("Some selected requirements are view-only for you.");
    return;
  }
  const selected = selectedOnThisPage();
  if (!selected.length) {
    notify.warning("The selected requirements are on another page. Open that page to delete them.");
    return;
  }
  const done = await onSubmitRequirementBulkDelete(selected, load);
  if (done) selection.clear();
};

// ---- Export ----
// One requirement as a workbook, from its row: the same endpoint and download checks as the old list.
const requirementExport = createRequirementExportAction({
  exportRequirement: (id) => legacyRequirementService.exportRequirement(id),
  download: downloadBlob,
  notifyError: notify.error,
  notifySuccess: notify.success,
  pending: reactive({})
});

const exporting = ref(false);

const exportColumns = columns.filter((c) => c.name !== "actions").map((c) => ({
  label: c.label,
  value: (row) => {
    const v = typeof c.field === "function" ? c.field(row) : row[c.field];
    return v === "—" ? "" : v;
  }
}));

const stamp = () => new Date().toISOString().slice(0, 10);

const exportRows = (list, name) => {
  if (!list.length) return;
  exportCsv({ filename: `${name}-${stamp()}.csv`, columns: exportColumns, rows: list });
  notify.success(`${list.length} row${list.length === 1 ? "" : "s"} exported.`);
};

const exportAll = async () => {
  exporting.value = true;
  try {
    const { sortBy, descending } = pagination.value;
    const all = [];
    const pageSize = 500;
    for (let page = 1; all.length < EXPORT_LIMIT; page += 1) {
      const resp = await requirementService.getAllRequirement({ ...buildFilterPayload(), page, pageSize, sortBy, descending, sorts: buildSorts() });
      const batch = (resp?.data || []).map(toRow);
      all.push(...batch);
      if (batch.length < pageSize || all.length >= (resp?.total ?? 0)) break;
    }
    exportRows(all.slice(0, EXPORT_LIMIT), "requirements-all");
  } catch (error) {
    console.error("Export failed", error);
  } finally {
    exporting.value = false;
  }
};

// ---- Mount ----
onMounted(() => {
  lookups.ensureRowLookups();
  if (hasLookupFilters.value) lookups.ensureFilterLookups();
  filters.init();
  load();
});
</script>

<style scoped>
/* ---- The look of the Projects list: a header bar in the theme colour, a line round every cell, and
   less air round the header card and the table's own bar. ---- */
.req-page :deep(.app-data-table thead tr th) {
  background: var(--q-primary);
  color: #fff;
  border-bottom-color: var(--q-primary);
  border-right-color: rgba(255, 255, 255, 0.25);
}
/* On the coloured bar the select-all box is drawn in white. */
.req-page :deep(.app-data-table thead .q-checkbox__inner) {
  color: #fff;
}
.req-page :deep(.app-data-table thead .q-checkbox__bg) {
  border-color: #fff;
}
.req-page :deep(.app-data-table thead .q-checkbox__svg) {
  color: var(--q-primary);
}
.req-page :deep(.app-data-table thead .app-th__resize:hover) {
  background: #fff;
  opacity: 0.6;
}
/* The rows read in plain dark text, as on the Projects list: what can be clicked turns blue under the
   pointer, not before. Status, priority, approval and tags keep their own colours. */
.req-page :deep(.app-data-table tbody td) {
  border-color: rgba(0, 0, 0, 0.12);
}
.req-page :deep(.app-data-table tbody tr:hover td) {
  background: #f5f5f5;
}
/* The body text as on the old requirement list: the app's standard grey at the regular weight and 13px,
   the title included; a pinned row on the same pale amber. */
.req-page :deep(.app-data-table tbody td) {
  color: var(--q-dark);
  font-size: 13px;
  font-weight: 400;
}
.req-page :deep(.rtc2__title) {
  color: #000;
  text-decoration: none;
  font-weight: 400;
}
.req-page :deep(.rtc2__number) {
  color: inherit;
  font-weight: 400;
  font-size: 13px;
}
.req-page :deep(.app-data-table tbody tr.app-data-table__row--pinned td) {
  background: #fff8e1;
}
/* The type of the old requirement list: column headings at 14px medium, everything in the rows at 13px
   regular. The cells' own components (due date, badges, names) give up their bolder or smaller type. */
.req-page :deep(.app-data-table thead tr th) {
  font-size: 14px;
  font-weight: 500;
}
.req-page :deep(.app-data-table tbody td .rdc),
.req-page :deep(.app-data-table tbody td .rdc2__value),
.req-page :deep(.app-data-table tbody td .app-avatar__name),
.req-page :deep(.app-data-table tbody td .app-option-badge),
.req-page :deep(.app-data-table tbody td .abs__badge),
.req-page :deep(.app-data-table tbody td .rtc__chip) {
  font-size: 13px;
  font-weight: 400;
}
.req-page :deep(.rtc2__title:hover),
.req-page :deep(.rtc2__title:focus-visible) {
  color: #0f6295;
}
.req-page :deep(.rtc2__pin) {
  color: var(--ink-500, #647a82);
}
/* The actions, as on the old requirement list: square outlined icons of one size, evenly spaced, in the
   body text colour (View and More in the theme colour), filling with the theme colour under the pointer. */
.req-act {
  width: 26px;
  height: 26px;
  min-height: 26px;
  padding: 0;
  margin-right: 6px;
  border: 1px solid var(--q-secondary);
  border-radius: 5px;
}
.req-act:last-child {
  margin-right: 0;
}
.req-act :deep(.q-icon) {
  font-size: 18px;
}
.req-act:hover,
.req-act:focus-visible {
  border-color: var(--q-primary);
  background: var(--q-primary);
  color: #fff !important;
}
/* The fill is the hover; Quasar's own grey wash over it is not needed. */
.req-act :deep(.q-focus-helper) {
  display: none;
}
/* One colour in the rows, as on the old list. Everything a cell's own component drew lighter or darker
   (the dash of an empty cell, a person's name, the due-date reading, a task chip) takes the row's text
   colour. Status, approval and tag badges are the exception: their colour is the information. */
.req-page :deep(.app-data-table tbody td .text-grey-6),
.req-page :deep(.app-data-table tbody td .rdc),
.req-page :deep(.app-data-table tbody td .rdc2__value),
.req-page :deep(.app-data-table tbody td .app-avatar__name),
.req-page :deep(.app-data-table tbody td .rtc2__desc),
.req-page :deep(.app-data-table tbody td .rtc2__context),
.req-page :deep(.app-data-table tbody td .req-task-chip) {
  color: var(--q-dark) !important;
}
/* The small buttons inside the cells (date picker, change log, tags, Project Center): the same colour,
   not dimmed, turning blue under the pointer like the rest of the row. The action column keeps its own. */
.req-page :deep(.app-data-table tbody td .q-btn:not(.req-act):not(.abs)) {
  color: var(--q-dark) !important;
  opacity: 1;
}
.req-page :deep(.app-data-table tbody td .q-btn:not(.req-act):not(.abs):hover),
.req-page :deep(.app-data-table tbody td .q-btn:not(.req-act):not(.abs):focus-visible) {
  color: #0f6295 !important;
}
.req-page :deep(.app-list-header) {
  margin-bottom: 8px;
}
.req-page :deep(.app-list-header__bar) {
  padding-top: 4px;
  padding-bottom: 4px;
}
.req-page :deep(.app-list-header__body) {
  padding-top: 6px;
  padding-bottom: 6px;
}
.req-page :deep(.app-data-table .q-table__top) {
  padding: 2px 10px;
}
/* A repeated project or module name is left out for the eye but kept for screen readers. */
.req-sr {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0 0 0 0);
  white-space: nowrap;
}
/* A value shown as plain text that opens a list of choices on a click. */
.req-plain-pick {
  display: inline-flex;
  align-items: center;
  cursor: pointer;
  border-radius: 4px;
}
.req-plain-pick:hover,
.req-plain-pick:focus-visible {
  color: #0f6295;
  outline: none;
}
.req-pick-menu {
  min-width: 180px;
}
.req-pick-menu__on {
  font-weight: 600;
  color: var(--q-primary);
}
.req-link {
  color: inherit;
  font-weight: 500;
  cursor: pointer;
  text-decoration: none;
}
.req-link:hover,
.req-link:focus-visible {
  color: #0f6295;
  text-decoration: underline;
  outline: none;
}
.req-ellipsis {
  display: block;
  max-width: 260px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
/* Small in-cell buttons sit beside text; they are dimmed until the row is hovered. */
.req-cell-btn {
  margin-left: 2px;
}
.req-page :deep(tr:hover) .req-cell-btn,
.req-cell-btn:focus-visible {
  opacity: 1;
}
/* The row whose record was last opened stays marked, so coming back from a panel lands the eye. */
.req-page :deep(tr.req-row--active td) {
  background: rgb(232, 255, 230);
}
.req-page :deep(.app-data-table tbody tr) {
  cursor: pointer;
}
.req-task-chip {
  margin: 0 4px 2px 0;
}
.req-menu {
  min-width: 220px;
}
.req-swatches {
  display: grid;
  grid-template-columns: repeat(4, 28px);
  gap: 6px;
}
.req-swatch {
  width: 28px;
  height: 28px;
  border-radius: 50%;
  border: 2px solid transparent;
  cursor: pointer;
  padding: 0;
}
.req-swatch--on {
  outline: 2px solid var(--q-primary);
  outline-offset: 2px;
}
</style>
