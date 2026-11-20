<template>
  <q-card flat bordered class="app-detail-header q-mb-md">
    <div class="app-detail-header__bar q-px-md q-py-sm">
      <div class="app-detail-header__title">
        <app-breadcrumbs v-if="items.length" :items="items" no-margin />
        <app-info-tip v-if="$slots.note" size="18px" color="primary" max-width="460px" anchor="bottom left" self="top left">
          <slot name="note" />
        </app-info-tip>
      </div>

      <!-- Status pills first, then the buttons: things to read before things to press. -->
      <div class="app-detail-header__actions">
        <slot name="actions" />
        <q-btn v-if="showBack" outline no-caps color="primary" icon="o_arrow_back" label="Back" @click="goBack" />
      </div>
    </div>
  </q-card>
</template>

<script setup>
// The carded header of a record page: crumbs on the left, the record's status pills and actions on the
// right, and Back. Every q-btn in the actions row is normalised to 36px and every badge/chip becomes a
// 24px pill, so "things to press" and "things to read" are visibly different kinds.
import { useRouter } from "vue-router";
import AppBreadcrumbs from "components/common/AppBreadcrumbs.vue";
import AppInfoTip from "components/common/AppInfoTip.vue";

const props = defineProps({
  // [{ label, icon?, to? }] — the last crumb is the record's own name.
  items: { type: Array, default: () => [] },
  // Where Back goes when there is no history to return to.
  backTo: { type: [String, Object], default: null },
  showBack: { type: Boolean, default: true }
});

const router = useRouter();

const goBack = () => {
  if (window.history.state?.back) router.back();
  else if (props.backTo) router.push(props.backTo);
  else router.back();
};
</script>

<style scoped>
.app-detail-header {
  border-radius: 12px;
  box-shadow: none;
}
.app-detail-header__bar {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px 12px;
}
.app-detail-header__title {
  display: flex;
  align-items: center;
  flex: 1 1 auto;
  min-width: 0;
}
.app-detail-header__actions {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  flex-wrap: wrap;
  gap: 8px;
  margin-left: auto;
  --dh-action-height: 36px;
  --dh-status-height: 24px;
}
.app-detail-header__actions :deep(.q-btn:not(.q-btn--round)) {
  min-height: var(--dh-action-height);
}
.app-detail-header__actions :deep(.q-badge:not(.q-badge--floating)),
.app-detail-header__actions :deep(.q-chip) {
  height: var(--dh-status-height);
  border-radius: 999px;
  padding: 0 10px;
  font-size: 11.5px;
  font-weight: 600;
}
@media (max-width: 599px) {
  .app-detail-header__actions {
    width: 100%;
    justify-content: flex-start;
  }
}
</style>
