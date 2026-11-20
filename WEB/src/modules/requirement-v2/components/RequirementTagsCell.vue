<template>
  <div class="rtc">
    <q-chip
      v-for="tag in visibleTags"
      :key="tag.value"
      dense
      :removable="editable"
      :style="{ backgroundColor: tag.bgColor || '#e0e0e0', color: tag.color || '#191919' }"
      class="rtc__chip"
      @remove="$emit('remove', tag)"
    >
      <span class="ellipsis">{{ tag.text }}</span>
      <q-tooltip v-if="(tag.text || '').length > 18" :delay="300">{{ tag.text }}</q-tooltip>
    </q-chip>

    <q-btn
      v-if="tags.length > LIMIT"
      flat dense no-caps size="sm" color="primary"
      :label="expanded ? 'Show less' : `+${tags.length - LIMIT} more`"
      @click.stop="expanded = !expanded"
    />

    <span v-if="!tags.length && !editable" class="text-grey-6">—</span>

    <!-- The editor opens from its own button, so a chip's remove cross stays a remove and nothing else. -->
    <q-btn v-if="editable" flat round dense size="sm" color="grey-7" icon="o_label" class="rtc__edit" aria-label="Edit tags">
      <q-tooltip>Edit tags</q-tooltip>
      <q-popup-edit
        v-slot="scope"
        :model-value="tags"
        anchor="bottom left"
        self="top left"
        class="rtc__popup"
        @before-show="$emit('before-edit')"
        @save="(value) => $emit('save', value)"
      >
        <div class="row items-center no-wrap q-mb-sm">
          <div class="text-subtitle2 text-primary">Tags</div>
          <q-space />
          <q-btn v-close-popup flat round dense size="sm" color="grey-8" icon="o_close" aria-label="Close" />
        </div>
        <tag-editor
          v-model="scope.value"
          :row-id="rowId"
          :available-tags="availableTags"
          @filter="(...args) => $emit('filter', ...args)"
        />
        <div class="row justify-end q-gutter-sm q-mt-md">
          <q-btn flat dense no-caps color="grey-8" label="Cancel" @click.stop="scope.cancel" />
          <q-btn unelevated dense no-caps color="primary" label="Set" @click.stop="scope.set" />
        </div>
      </q-popup-edit>
    </q-btn>
  </div>
</template>

<script setup>
// A row's tags: coloured chips (the first five, with a "+n more" toggle), each removable, and an editor
// over the site's tag catalogue for rows the reader may manage.
import { ref, computed } from "vue";
import TagEditor from "src/modules/project-tasks/components/_taskTagEditor.vue";

const props = defineProps({
  // [{ text, value, color, bgColor }]
  tags: { type: Array, default: () => [] },
  rowId: { type: String, default: "" },
  editable: { type: Boolean, default: false },
  availableTags: { type: Array, default: () => [] }
});

defineEmits(["remove", "save", "filter", "before-edit"]);

const LIMIT = 5;
const expanded = ref(false);

const visibleTags = computed(() => (expanded.value ? props.tags : props.tags.slice(0, LIMIT)));
</script>

<style scoped>
.rtc {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 4px;
  max-width: 360px;
}
.rtc__chip {
  margin: 0;
  max-width: 100%;
  font-size: 12px;
}
.rtc__edit {
  opacity: 0.55;
}
.rtc:hover .rtc__edit,
.rtc__edit:focus-visible {
  opacity: 1;
}
</style>

<style>
/* The popup is portaled to <body>; its width has to be set outside the scoped block. */
.rtc__popup {
  width: 340px;
  max-width: 92vw;
}
</style>
