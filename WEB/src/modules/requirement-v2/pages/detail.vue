<template>
  <q-page padding class="rdp">
    <template v-if="!id">
      <app-detail-header :items="crumbs" :back-to="{ name: 'requirement-v2' }" />
      <app-empty-state icon="o_search_off" title="No requirement selected" text="Open a requirement from the list to see its page.">
        <q-btn unelevated no-caps color="primary" label="Go to requirements" :to="{ name: 'requirement-v2' }" />
      </app-empty-state>
    </template>

    <!-- The record carries the page's crumb trail, Back and note in its own header: one header, not two. -->
    <requirement-record
      v-else
      ref="recordRef"
      :id="id"
      :initial-tab="initialTab"
      :initial-entity="initialEntity"
      :crumbs="crumbs"
      :back-to="{ name: 'requirement-v2' }"
      :page-note="PAGE_NOTE"
      @update:tab="onTabChange"
      @deleted="onDeleted"
    />
  </q-page>
</template>

<script setup>
// The requirement's all-in-one page (v2), opened from the v2 requirement list or from the "v2" icon on
// the current one. The id comes from the route; the open tab lives in the URL.
import { ref, computed } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useAuthStore } from "stores/auth";
import AppDetailHeader from "components/common/AppDetailHeader.vue";
import AppEmptyState from "components/common/AppEmptyState.vue";
import RequirementRecord from "modules/requirement-v2/components/RequirementRecord.vue";

const PAGE_NOTE =
  "The requirement's own page: what it is, its Requirement Log and notes, everything linked to it (tasks, issues, " +
  "test cases, Q&A, action items and time, in the Related tab as a summary or as working views), " +
  "its documents, data mappings and history.";

const route = useRoute();
const router = useRouter();
const user = useAuthStore().user;

const id = computed(() => route.params.id || route.query.id || history.state?.requirementId || "");

// The old Dashboard / Workbench / Workspace tabs live on in the Related tab; "All fields" (info) is part
// of Overview.
const LEGACY_TABS = { dashboard: "related", workbench: "related", workspace: "related", info: "overview" };
const initialTab = computed(() => {
  const tab = String(route.query.tab || "overview");
  return LEGACY_TABS[tab] || tab;
});
// ?related=tasks opens the Related tab on one kind of linked work.
const initialEntity = computed(() => String(route.query.related || "all"));
const recordRef = ref(null);

// The trail up to the list; the record header adds its own number as the last crumb. Back itself
// returns to wherever the reader came from (either requirement list), falling back to the v2 list.
const crumbs = computed(() => [
  { label: "Home", icon: "o_home", to: user?.siteLandingPageLink || "/" },
  { label: "SDLC" },
  { label: "Requirements", to: { name: "requirement-v2" } }
]);

const onTabChange = (tab) => {
  if (initialTab.value === tab) return;
  router.replace({ query: { ...route.query, tab: tab === "overview" ? undefined : tab } });
};

// A deleted requirement has no page: back to the list the reader came from, or the v2 list.
const onDeleted = () => {
  if (window.history.state?.back) router.back();
  else router.replace({ name: "requirement-v2" });
};
</script>
