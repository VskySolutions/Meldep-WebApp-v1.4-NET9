<template>
  <div class="rtc2">
    <div class="rtc2__top">
      <q-icon v-if="row.isPinned" name="o_push_pin" size="14px" class="rtc2__pin">
        <q-tooltip>Pinned to the top, for you</q-tooltip>
      </q-icon>
      <!-- Real links: middle-click and "open in new tab" work, and the page records which row was opened.
           The number and the title are the only parts of the cell that open the requirement. -->
      <router-link :to="{ name: 'requirement-v2-detail', params: { id: row.id } }" class="rtc2__number" @click.stop="$emit('open', row)">#{{ row.requirementNumber }}</router-link>
      <router-link :to="{ name: 'requirement-v2-detail', params: { id: row.id } }" class="rtc2__title" @click.stop="$emit('open', row)">
        {{ row.title }}
      </router-link>
    </div>
    <div v-if="row.shortDescriptionText && showDescription" class="rtc2__desc">
      {{ row.shortDescriptionText }}
      <q-tooltip max-width="420px" :delay="500">{{ row.shortDescriptionText }}</q-tooltip>
    </div>
    <div v-if="showContext" class="rtc2__context">
      <span class="ellipsis">{{ row.project?.name }}</span>
      <template v-if="row.projectModule?.name">
        <q-icon name="o_chevron_right" size="12px" />
        <span class="ellipsis">{{ row.projectModule.name }}</span>
      </template>
    </div>
  </div>
</template>

<script setup>
// The primary cell of the requirement list: the number and the title (links to the requirement's page)
// and the short description under it — what a reader scans the list by. The project › module line is
// for readings where those columns are hidden.
defineProps({
  row: { type: Object, required: true },
  showDescription: { type: Boolean, default: true },
  showContext: { type: Boolean, default: false }
});

// Fired alongside the link's own navigation, so the list can mark the row as the one last opened.
defineEmits(["open"]);
</script>

<style scoped>
.rtc2 {
  min-width: 0;
  max-width: 420px;
}
.rtc2__top {
  display: flex;
  align-items: baseline;
  gap: 6px;
  min-width: 0;
}
.rtc2__pin {
  color: var(--q-primary);
  transform: rotate(-35deg);
  align-self: center;
}
.rtc2__number {
  flex: 0 0 auto;
  font-size: 11.5px;
  font-weight: 600;
  color: var(--ink-500, #647a82);
  font-variant-numeric: tabular-nums;
  cursor: pointer;
  text-decoration: none;
}
.rtc2__number:hover,
.rtc2__number:focus-visible {
  color: var(--q-primary);
  text-decoration: underline;
  outline: none;
}
.rtc2__title {
  color: var(--q-primary);
  font-weight: 500;
  cursor: pointer;
  text-decoration: none;
  white-space: normal;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  line-height: 1.3;
}
.rtc2__title:hover,
.rtc2__title:focus-visible {
  text-decoration: underline;
  outline: none;
}
.rtc2__desc {
  margin-top: 2px;
  font-size: 12px;
  color: var(--ink-500, #647a82);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.rtc2__context {
  display: flex;
  align-items: center;
  gap: 2px;
  margin-top: 2px;
  font-size: 11.5px;
  color: var(--ink-500, #647a82);
  min-width: 0;
}
</style>
