<template>
  <q-card flat bordered class="app-list-header q-mb-md">
    <!-- Crumbs on the left, the tools for the list on the right. -->
    <div class="app-list-header__bar q-px-md q-py-sm">
      <div class="app-list-header__title">
        <app-breadcrumbs v-if="breadcrumbs.length" :items="breadcrumbs" no-margin class="app-list-header__crumbs" />
        <!-- What the page is for, on a tooltip beside the title rather than a paragraph under the bar. -->
        <app-info-tip v-if="$slots.note" size="18px" color="primary" max-width="460px" anchor="bottom left" self="top left">
          <slot name="note" />
        </app-info-tip>
      </div>

      <div class="app-list-header__tools">
        <q-input
          v-if="showSearch"
          ref="searchRef"
          :model-value="search"
          dense
          outlined
          clearable
          debounce="300"
          :placeholder="searchPlaceholder"
          class="app-list-header__search"
          @update:model-value="$emit('update:search', $event || '')"
        >
          <template #prepend><q-icon name="o_search" /></template>
        </q-input>

        <q-btn v-if="showFilters" outline no-caps color="primary" icon="o_filter_list" label="Filters" @click="$emit('filters')">
          <q-badge v-if="filterCount" floating color="primary">{{ filterCount }}</q-badge>
        </q-btn>

        <!-- Extra page-specific actions, shown before Add. -->
        <slot name="actions" />

        <q-btn v-if="showAdd" unelevated no-caps color="primary" :icon="addIcon" :label="addLabel" :disable="addDisable" @click="$emit('add')" />

        <q-btn v-if="showBack" flat no-caps color="primary" icon="o_arrow_back" label="Back" @click="$emit('back')" />

        <!-- The fold arrow, last on the bar (as on the Analytics registers). -->
        <q-btn
          v-if="collapsible"
          flat
          round
          dense
          color="grey-8"
          :icon="collapsed ? 'o_expand_more' : 'o_expand_less'"
          :aria-label="collapsed ? expandHint : collapseHint"
          :aria-expanded="String(!collapsed)"
          @click="$emit('update:collapsed', !collapsed)"
        >
          <q-tooltip>{{ collapsed ? expandHint : collapseHint }}</q-tooltip>
        </q-btn>
      </div>
    </div>

    <!-- The part of the header that folds away (a summary, counted shortcuts): the list moves up into its place. -->
    <q-slide-transition v-if="collapsible && $slots.body">
      <div v-show="!collapsed">
        <q-separator />
        <div class="app-list-header__body q-px-md">
          <slot name="body" />
        </div>
      </div>
    </q-slide-transition>
  </q-card>
</template>

<script setup>
// The carded header of every list page: breadcrumbs · search · Filters-with-count · Add · Back. A page
// with a summary above its list can make the header `collapsible`: the `body` slot then sits inside the
// card under the bar and folds away on the arrow, so the list moves up.
import { ref } from "vue";
import AppBreadcrumbs from "components/common/AppBreadcrumbs.vue";
import AppInfoTip from "components/common/AppInfoTip.vue";

const searchRef = ref(null);

// Lets a page put the cursor in the search box.
defineExpose({ focusSearch: () => searchRef.value?.focus() });

defineProps({
  breadcrumbs: { type: Array, default: () => [] },
  search: { type: String, default: "" },
  searchPlaceholder: { type: String, default: "Search" },
  showSearch: { type: Boolean, default: false },
  showFilters: { type: Boolean, default: false },
  filterCount: { type: Number, default: 0 },
  showAdd: { type: Boolean, default: false },
  addLabel: { type: String, default: "Add" },
  addIcon: { type: String, default: "o_add" },
  addDisable: { type: Boolean, default: false },
  showBack: { type: Boolean, default: false },
  // Adds the fold arrow and the `body` slot. `collapsed` is the page's to keep (v-model:collapsed).
  collapsible: { type: Boolean, default: false },
  collapsed: { type: Boolean, default: false },
  // What the arrow says it will do, in each state.
  collapseHint: { type: String, default: "Hide the summary to give the list the whole screen" },
  expandHint: { type: String, default: "Show the summary" }
});

defineEmits(["update:search", "filters", "add", "back", "update:collapsed"]);
</script>

<style scoped>
.app-list-header {
  border-radius: 12px;
  box-shadow: none;
}
/* gap rather than q-gutter: the utility's negative margins fight the card padding once the row wraps. */
.app-list-header__bar {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px 12px;
}
.app-list-header__title {
  display: flex;
  align-items: center;
  flex: 1 1 auto;
  min-width: 0;
}
.app-list-header__crumbs {
  min-width: 0;
}
.app-list-header__tools {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  flex-wrap: wrap;
  gap: 8px;
  margin-left: auto;
  min-width: 0;
}
.app-list-header__search {
  width: 280px;
  max-width: 100%;
}
.app-list-header__search :deep(.q-field__control) {
  min-height: 40px !important;
}
.app-list-header__body {
  padding-top: 10px;
  padding-bottom: 12px;
}

/* On a phone the crumbs and the tools each get a line, and the search box takes what the buttons leave. */
@media (max-width: 599px) {
  .app-list-header__tools {
    width: 100%;
    justify-content: flex-start;
  }
  .app-list-header__search {
    flex: 1 1 160px;
    width: auto;
  }
}
</style>
