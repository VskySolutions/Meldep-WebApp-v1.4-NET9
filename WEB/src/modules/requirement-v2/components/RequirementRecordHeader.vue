<template>
  <q-card flat bordered class="rrh" :style="model?.requirementColor ? { '--rrh-colour': model.requirementColor } : {}">
    <div class="rrh__main">
      <div class="rrh__identity">
        <!-- Where the record lives (the page's crumb trail), then what it is. -->
        <div class="rrh__eyebrow">
          <template v-if="crumbs.length">
            <app-breadcrumbs :items="crumbs" no-margin class="rrh__crumbs" />
            <q-icon name="o_chevron_right" size="1.2em" color="grey-6" />
          </template>
          <span class="rrh__number">#{{ model?.requirementNumber }}</span>
          <q-icon v-if="model?.isPinned" name="o_push_pin" size="14px" class="rrh__pin">
            <q-tooltip>Pinned to the top of your list</q-tooltip>
          </q-icon>
          <span v-if="model?.requirementType?.dropDownValue" class="rrh__type">
            {{ model.requirementType.dropDownValue }}
            <q-tooltip>Requirement type</q-tooltip>
          </span>
          <span class="rrh__crumb">
            <a class="rrh__link" role="button" tabindex="0" @click="$emit('open-project')" @keyup.enter="$emit('open-project')">
              {{ model?.project?.name || "—" }}
              <q-tooltip>Open the project</q-tooltip>
            </a>
            <template v-if="model?.projectModule?.name">
              <q-icon name="o_chevron_right" size="14px" />
              <a class="rrh__link" role="button" tabindex="0" @click="$emit('open-module')" @keyup.enter="$emit('open-module')">
                {{ model.projectModule.name }}
                <q-tooltip>Open the project module</q-tooltip>
              </a>
            </template>
          </span>
        </div>

        <div class="rrh__title-row">
          <h1 class="rrh__title">{{ model?.title || "Requirement" }}</h1>
          <app-info-tip v-if="pageNote" :text="pageNote" size="16px" color="grey-6" max-width="460px" anchor="bottom left" self="top left" class="rrh__tip" />
        </div>

        <div v-if="shortDescription" class="rrh__summary">{{ shortDescription }}</div>

        <div class="rrh__pills">
          <app-badge-select
            :model-value="model?.status?.id ?? null"
            :options="statusOptions"
            :fallback="statusBadge"
            :readonly="!editable"
            :saving="busy === 'status'"
            header="Set status"
            @update:model-value="$emit('status', $event)"
          />
          <app-badge-select
            :model-value="model?.priority?.id ?? null"
            :options="priorityOptions"
            :fallback="priorityBadge || { label: 'No priority' }"
            :readonly="!editable"
            :saving="busy === 'priority'"
            header="Set priority"
            @update:model-value="$emit('priority', $event)"
          />
          <app-option-badge v-if="approvalBadge" :option="approvalBadge" class="rrh__approval">
            <q-tooltip>Approval status</q-tooltip>
          </app-option-badge>
          <span v-if="due.label" :class="['rrh__due', `rrh__due--${due.state}`]">
            <q-icon :name="due.state === 'overdue' ? 'o_error' : 'o_schedule'" size="14px" />{{ due.label }}
            <q-tooltip>Planned end {{ model?.plannedEndDate || "—" }}</q-tooltip>
          </span>
          <requirement-tags-cell
            :tags="tags"
            :row-id="model?.id"
            :editable="editable"
            :available-tags="availableTags"
            class="rrh__tags"
            @before-edit="$emit('tags-before-edit')"
            @filter="(...args) => $emit('tags-filter', ...args)"
            @save="$emit('tags-save', $event)"
            @remove="$emit('tags-remove', $event)"
          />
        </div>
      </div>

      <!-- Actions: a jump to another requirement, Edit as an icon, one named button that holds every other
           action, and Back. -->
      <div class="rrh__actions">
        <requirement-quick-search :current-id="model?.id || ''" class="rrh__search" />
        <q-btn v-if="editable" unelevated color="primary" icon="o_edit" aria-label="Edit the requirement" class="rrh__edit" @click="$emit('edit')">
          <q-tooltip>Edit the requirement</q-tooltip>
        </q-btn>
        <q-btn unelevated no-caps label="More" icon-right="o_expand_more" aria-label="More actions" class="rrh__more">
          <q-tooltip :delay="500">Description Log, note, pin, colour and the other actions</q-tooltip>
          <q-menu anchor="bottom right" self="top right" :offset="[0, 6]">
            <q-list dense class="rrh__menu">
              <template v-if="editable || canNotes">
                <q-item v-if="editable" v-close-popup clickable @click="$emit('log')">
                  <q-item-section avatar><q-icon name="o_history_edu" size="18px" /></q-item-section>
                  <q-item-section>Add to the Description Log</q-item-section>
                </q-item>
                <q-item v-if="canNotes" v-close-popup clickable @click="$emit('note')">
                  <q-item-section avatar><q-icon name="o_sticky_note_2" size="18px" /></q-item-section>
                  <q-item-section>Add a note</q-item-section>
                </q-item>
                <q-separator />
              </template>

              <q-item v-close-popup clickable @click="$emit('pin')">
                <q-item-section avatar><q-icon name="o_push_pin" size="18px" :color="model?.isPinned ? 'primary' : undefined" /></q-item-section>
                <q-item-section>{{ model?.isPinned ? "Unpin from the top of your list" : "Pin to the top of your list" }}</q-item-section>
              </q-item>
              <div v-if="editable" class="rrh__colour">
                <div class="rrh__colour-label"><q-icon name="o_palette" size="18px" />Colour, for you only</div>
                <div class="rrh__swatches">
                  <button
                    v-for="swatch in SWATCHES"
                    :key="swatch"
                    v-close-popup
                    type="button"
                    class="rrh__swatch"
                    :class="{ 'rrh__swatch--on': sameColour(model?.requirementColor, swatch) }"
                    :style="{ backgroundColor: swatch }"
                    :aria-label="`Colour ${swatch}`"
                    @click="$emit('colour', swatch)"
                  />
                </div>
              </div>
              <q-separator />

              <q-item v-close-popup clickable @click="$emit('copy-link')">
                <q-item-section avatar><q-icon name="o_link" size="18px" /></q-item-section>
                <q-item-section>Copy link</q-item-section>
              </q-item>
              <template v-if="editable">
                <q-item v-close-popup clickable @click="$emit('convert')">
                  <q-item-section avatar><q-icon name="o_swap_horiz" size="18px" /></q-item-section>
                  <q-item-section>Convert into task</q-item-section>
                </q-item>
                <q-item v-if="isDataIntegration" v-close-popup clickable @click="$emit('data-mapping')">
                  <q-item-section avatar><q-icon name="o_account_tree" size="18px" /></q-item-section>
                  <q-item-section>Edit data mapping</q-item-section>
                </q-item>
              </template>
              <q-separator />

              <q-item v-close-popup clickable :to="backTo || { name: 'requirement-v2' }">
                <q-item-section avatar><q-icon name="o_list" size="18px" /></q-item-section>
                <q-item-section>All requirements</q-item-section>
              </q-item>

              <template v-if="editable">
                <q-separator />
                <q-item v-close-popup clickable class="text-negative" @click="$emit('delete')">
                  <q-item-section avatar><q-icon name="o_delete" size="18px" color="negative" /></q-item-section>
                  <q-item-section>Delete requirement</q-item-section>
                </q-item>
              </template>
            </q-list>
          </q-menu>
        </q-btn>
        <q-btn outline no-caps color="primary" icon="o_arrow_back" label="Back" class="rrh__back" @click="goBack">
          <q-tooltip>Back to requirements</q-tooltip>
        </q-btn>
      </div>
    </div>
  </q-card>
</template>

<script setup>
// The one header of a requirement's page: Back and the crumb trail, the number, type and place; the
// title with the page's note on an info tip; the short description; the status and priority as editable
// badges, the approval state, the due standing and the tags; and the actions. It replaces the separate
// page header, so everything the page says about itself is said once. The related-work figures sit on
// the record's tab bar (RequirementRecord), not here.
import { computed } from "vue";
import { useRouter } from "vue-router";
import AppBreadcrumbs from "components/common/AppBreadcrumbs.vue";
import AppInfoTip from "components/common/AppInfoTip.vue";
import AppBadgeSelect from "components/common/AppBadgeSelect.vue";
import AppOptionBadge from "components/common/AppOptionBadge.vue";
import RequirementTagsCell from "modules/requirement-v2/components/RequirementTagsCell.vue";
import RequirementQuickSearch from "modules/requirement-v2/components/RequirementQuickSearch.vue";
import { dropDownBadge, dueInfo } from "modules/requirement-v2/utils/options";
import { stripHtml } from "src/utils/richText";

const props = defineProps({
  model: { type: Object, default: null },
  editable: { type: Boolean, default: false },
  canNotes: { type: Boolean, default: false },
  statusOptions: { type: Array, default: () => [] },
  priorityOptions: { type: Array, default: () => [] },
  tags: { type: Array, default: () => [] },
  availableTags: { type: Array, default: () => [] },
  // "status" | "priority" | "" — the write in flight.
  busy: { type: String, default: "" },
  // The page's crumb trail up to (not including) this record: [{ label, icon?, to? }].
  crumbs: { type: Array, default: () => [] },
  // Where Back goes when there is no history to return to.
  backTo: { type: [String, Object], default: null },
  // What this page is for, on the info tip beside the title.
  pageNote: { type: String, default: "" }
});

defineEmits([
  "status", "priority", "pin", "colour", "tags-before-edit", "tags-filter", "tags-save", "tags-remove",
  "edit", "log", "note", "convert", "data-mapping", "delete", "copy-link", "open-project", "open-module"
]);

const router = useRouter();

const goBack = () => {
  if (window.history.state?.back) router.back();
  else if (props.backTo) router.push(props.backTo);
  else router.back();
};

const SWATCHES = ["#ef5350", "#ec407a", "#ab47bc", "#5c6bc0", "#42a5f5", "#26a69a", "#9ccc65", "#ffa726"];
const sameColour = (a, b) => String(a || "").toLowerCase() === String(b || "").toLowerCase();

const statusBadge = computed(() => dropDownBadge(props.model?.status) || { label: "No status" });
const priorityBadge = computed(() => dropDownBadge(props.model?.priority));
const approvalBadge = computed(() => dropDownBadge(props.model?.approvalStatusDropDown));
const due = computed(() => dueInfo(props.model?.plannedEndDate, props.model?.status?.dropDownValue));
const shortDescription = computed(() => stripHtml(props.model?.shortDescription));
const isDataIntegration = computed(() => props.model?.requirementType?.dropDownValue === "Data Integration");
</script>

<style scoped>
.rrh {
  --rrh-colour: transparent;
  position: relative;
  border-radius: 12px;
  box-shadow: none;
  overflow: hidden;
}
.rrh::before {
  content: "";
  position: absolute;
  left: 0;
  top: 0;
  bottom: 0;
  width: 5px;
  background: var(--rrh-colour);
}
.rrh__main {
  display: flex;
  align-items: flex-start;
  gap: 8px 12px;
  flex-wrap: wrap;
  padding: 10px 14px 12px;
}
.rrh__identity {
  flex: 1 1 420px;
  min-width: 0;
}
.rrh__eyebrow {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 4px 8px;
  font-size: 12px;
  color: var(--ink-500, #647a82);
}
.rrh__crumbs {
  font-size: 12px;
}
.rrh__crumbs :deep(.q-breadcrumbs__el) {
  font-size: 12px;
}
.rrh__number {
  font-weight: 700;
  color: var(--q-primary);
  font-variant-numeric: tabular-nums;
}
.rrh__pin {
  color: var(--q-primary);
  transform: rotate(-35deg);
}
.rrh__type {
  padding: 1px 8px;
  border-radius: 999px;
  background: #eceff1;
  color: #455a64;
  font-size: 11px;
  font-weight: 600;
  cursor: default;
}
.rrh__crumb {
  display: inline-flex;
  align-items: center;
  gap: 2px;
  min-width: 0;
}
.rrh__link {
  color: var(--ink-700, #3a4d54);
  cursor: pointer;
  text-decoration: none;
}
.rrh__link:hover,
.rrh__link:focus-visible {
  color: var(--q-primary);
  text-decoration: underline;
  outline: none;
}
.rrh__title-row {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-top: 2px;
}
.rrh__title {
  margin: 0;
  font-size: 19px;
  font-weight: 600;
  line-height: 1.3;
  color: var(--q-primary);
  overflow-wrap: anywhere;
}
.rrh__tip {
  flex: 0 0 auto;
  cursor: help;
}
.rrh__summary {
  margin-top: 2px;
  font-size: 13px;
  color: var(--ink-700, #3a4d54);
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
.rrh__pills {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px 8px;
  margin-top: 8px;
}
.rrh__approval {
  cursor: default;
}
.rrh__due {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 3px 9px;
  border-radius: 999px;
  font-size: 11.5px;
  font-weight: 600;
  cursor: default;
}
.rrh__due--overdue {
  background: #fdecea;
  color: #8a1c12;
}
.rrh__due--due-soon {
  background: #fff4e5;
  color: #8a5300;
}
.rrh__due--scheduled {
  background: #eef3f5;
  color: var(--ink-700, #3a4d54);
}
.rrh__tags {
  max-width: 100%;
}
.rrh__actions {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  margin-left: auto;
}
/* Edit is the main move: filled, and a square of the search box's height now that it carries no label. */
.rrh__edit {
  width: 36px;
  min-width: 36px;
  height: 36px;
  min-height: 36px;
  padding: 0;
  border-radius: 8px;
}
/* The one door to every other action: named, a shade bolder than the controls beside it and lifted off
   the card, so it reads as a button to press rather than a decoration. */
.rrh__more {
  height: 36px;
  min-height: 36px;
  padding: 0 8px 0 14px;
  border: 1px solid var(--q-primary);
  border-radius: 8px;
  background: var(--brand-100, #e3f1f8);
  color: var(--q-primary);
  font-weight: 600;
  box-shadow: var(--shadow, 0 2px 10px rgba(18, 54, 69, 0.07));
}
.rrh__more:hover,
.rrh__more:focus-visible {
  background: var(--q-primary);
  color: #fff;
}
.rrh__more :deep(.q-icon) {
  margin-left: 2px;
}
.rrh__back {
  height: 36px;
  min-height: 36px;
  padding: 0 12px;
  border-radius: 8px;
}
.rrh__menu {
  min-width: 270px;
}
.rrh__colour {
  padding: 6px 16px 10px;
}
.rrh__colour-label {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-bottom: 8px;
  font-size: 13px;
  color: var(--ink-700, #3a4d54);
}
.rrh__swatches {
  display: grid;
  grid-template-columns: repeat(8, 22px);
  gap: 7px;
  padding-left: 34px;
}
.rrh__swatch {
  width: 22px;
  height: 22px;
  border-radius: 50%;
  border: 2px solid transparent;
  cursor: pointer;
  padding: 0;
}
.rrh__swatch--on {
  outline: 2px solid var(--q-primary);
  outline-offset: 2px;
}
@media (max-width: 599px) {
  .rrh__title {
    font-size: 17px;
  }
  .rrh__actions {
    width: 100%;
    margin-left: 0;
  }
  /* On a phone the search takes a line of its own above the buttons. */
  .rrh__actions .rrh__search {
    width: 100%;
  }
}
</style>
