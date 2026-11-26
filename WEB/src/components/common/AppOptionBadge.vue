<template>
  <!-- A value from a managed dropdown, rendered as its badge: the site's label, in the site's colours,
       with an optional description on the tooltip. -->
  <q-badge :style="style" class="app-option-badge">
    <q-icon v-if="option.icon" :name="option.icon" size="13px" class="q-mr-xs" />
    {{ option.label }}
    <!-- Trailing affordance, for the one place a badge is also a CONTROL (AppBadgeSelect). -->
    <slot />
    <q-tooltip v-if="option.description" max-width="320px" :delay="300">{{ option.description }}</q-tooltip>
  </q-badge>
</template>

<script setup>
// THE badge for a status-like value — a status, a priority, an approval state.
import { computed } from "vue";

const props = defineProps({
  // { label, description?, backgroundColor?, textColor?, icon? }. Every field but `label` is optional.
  option: { type: Object, required: true }
});

// Neutral grey where the list carries no colour: a value nobody has coloured yet still reads as a badge.
const style = computed(() => ({
  backgroundColor: props.option.backgroundColor || "#9e9e9e",
  color: props.option.textColor || "#ffffff"
}));
</script>

<style scoped>
.app-option-badge {
  font-weight: 500;
  font-size: 12px;
  padding: 3px 8px;
  white-space: nowrap;
  /* Always as wide as its label: inside a dropdown option (a column flex box) a badge would otherwise
     stretch and colour the whole row. */
  width: fit-content;
  max-width: 100%;
}
</style>
