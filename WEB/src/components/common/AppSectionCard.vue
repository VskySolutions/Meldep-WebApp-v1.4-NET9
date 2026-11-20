<template>
  <q-card flat bordered :class="['app-section-card', { 'app-section-card--flush': flush, 'app-section-card--dense': dense }]">
    <q-card-section v-if="title || $slots.actions" class="app-section-card__head row items-center no-wrap q-py-sm">
      <q-icon v-if="icon" :name="icon" color="primary" size="18px" class="q-mr-xs" />
      <div class="text-subtitle2 text-primary ellipsis">{{ title }}</div>
      <q-badge v-if="count != null" color="blue-1" text-color="primary" class="q-ml-sm">{{ count }}</q-badge>
      <app-info-tip v-if="info" :text="info" />
      <q-space />
      <div class="app-section-card__actions row items-center no-wrap">
        <slot name="actions" />
      </div>
    </q-card-section>
    <q-separator v-if="title || $slots.actions" />
    <q-card-section :class="['app-section-card__body', { 'q-pa-none': flush }]">
      <slot />
    </q-card-section>
  </q-card>
</template>

<script setup>
// The compact card every record view is built from: an icon-and-title head (with an optional count badge,
// an info tip and a right-aligned actions slot), a rule, and the body.
import AppInfoTip from "components/common/AppInfoTip.vue";

defineProps({
  title: { type: String, default: "" },
  icon: { type: String, default: "" },
  info: { type: String, default: "" },
  count: { type: [Number, String], default: null },
  // No body padding (for a table or a list that draws its own edges).
  flush: { type: Boolean, default: false },
  // Tighter head and body (forms made of several small cards).
  dense: { type: Boolean, default: false }
});
</script>

<style scoped>
.app-section-card {
  border-radius: 12px;
  box-shadow: none;
}
.app-section-card__head {
  min-height: 46px;
}
.app-section-card__actions {
  gap: 4px;
}
.app-section-card--dense .app-section-card__head {
  min-height: 40px;
  padding-top: 6px;
  padding-bottom: 6px;
}
.app-section-card--dense .app-section-card__body {
  padding: 12px;
}
</style>
