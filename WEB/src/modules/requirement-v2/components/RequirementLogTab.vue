<template>
  <!-- The log reads like a chat for everyone: oldest at the top, the newest entry at the foot (right above
       the composer for those who may write), earlier entries arriving as the reader scrolls up. -->
  <!-- In full screen the whole tab is moved to <body> and laid over the window; nothing in it is rebuilt,
       so an entry being written stays as it is. -->
  <teleport to="body" :disabled="!fullscreen">
  <div :class="['rlt2 column no-wrap', { 'rlt2--full': fullscreen }]" :aria-label="fullscreen ? 'Description Log, full screen' : undefined">
    <!-- Over the whole window the page's header is out of sight: say whose log this is. -->
    <div v-if="fullscreen" class="rlt2__where q-px-md">
      <q-icon name="o_history_edu" size="18px" color="primary" />
      <span class="rlt2__where-title"><span v-if="model?.requirementNumber" class="rlt2__where-no">#{{ model.requirementNumber }}</span> {{ model?.title || "Requirement" }}</span>
      <span class="rlt2__where-tab">Description Log</span>
    </div>

    <div class="rlt2__bar q-px-md q-pt-sm">
      <div class="text-caption text-grey-7">{{ total }} entr{{ total === 1 ? "y" : "ies" }}</div>
      <q-space />
      <q-toggle v-if="!editable" v-model="showMeta" dense label="Author and time" class="text-caption" />
      <q-btn flat round dense :color="fullscreen ? 'primary' : 'grey-8'" :icon="fullscreen ? 'o_fullscreen_exit' : 'o_fullscreen'" :aria-label="fullscreen ? 'Exit full screen' : 'Full screen'" @click="toggleFullscreen">
        <q-tooltip>{{ fullscreen ? "Exit full screen (Esc)" : "Full screen" }}</q-tooltip>
      </q-btn>
    </div>
    <requirement-log-timeline
      ref="timelineRef"
      :id="id"
      :readonly="!editable"
      :show-meta="showMeta"
      class="col"
      @changed="$emit('changed')"
      @loaded="total = $event"
    />
  </div>
  </teleport>
</template>

<script setup>
// The requirement log tab: the original description and every entry added since, as one thread.
import { ref, watch, nextTick, onDeactivated, onBeforeUnmount } from "vue";
import RequirementLogTimeline from "modules/requirement-v2/components/_description_timeline_view.vue";

const props = defineProps({
  id: { type: String, required: true },
  // The requirement itself, for the title shown in full screen.
  model: { type: Object, default: null },
  editable: { type: Boolean, default: false }
});

defineEmits(["changed"]);

const total = ref(0);
const showMeta = ref(true);
const timelineRef = ref(null);

// ---- Full screen ----
// The tab laid over the whole window (see the teleport in the template), for reading a long log.
const fullscreen = ref(false);
const BODY_CLASS = "rlt2-fullscreen-open";

// Escape leaves full screen, unless it is closing something opened on top of it (a menu, a dialog).
const onFullscreenKey = (event) => {
  if (event.key !== "Escape" || event.defaultPrevented) return;
  if (document.querySelector(".q-menu, .q-dialog")) return;
  setFullscreen(false);
};

const setFullscreen = async (on) => {
  if (fullscreen.value === on) return;
  // A chat is read from its foot: the reader's place is how far above the newest entry they stand, and
  // moving the tab in the page would otherwise send the log back to its top.
  const before = timelineRef.value?.scroller();
  const fromFoot = before ? before.scrollHeight - before.scrollTop - before.clientHeight : 0;
  fullscreen.value = on;
  document.body.classList.toggle(BODY_CLASS, on);
  if (on) window.addEventListener("keydown", onFullscreenKey);
  else window.removeEventListener("keydown", onFullscreenKey);
  await nextTick();
  const after = timelineRef.value?.scroller();
  if (after) after.scrollTop = Math.max(0, after.scrollHeight - after.clientHeight - fromFoot);
  if (on) timelineRef.value?.fillPane();
};
const toggleFullscreen = () => setFullscreen(!fullscreen.value);

// Full screen ends with the tab: another tab, another requirement, another page.
const leaveFullscreen = () => {
  if (!fullscreen.value) return;
  fullscreen.value = false;
  document.body.classList.remove(BODY_CLASS);
  window.removeEventListener("keydown", onFullscreenKey);
};
onDeactivated(leaveFullscreen);
onBeforeUnmount(leaveFullscreen);
watch(() => props.id, leaveFullscreen);
</script>

<style scoped>
.rlt2 {
  height: 100%;
  min-height: 0;
}
.rlt2__bar {
  display: flex;
  align-items: center;
  gap: 8px;
  flex: 0 0 auto;
  padding-bottom: 4px;
  border-bottom: 1px solid var(--line, #dde6e8);
  background: #fff;
}
.rlt2--full {
  position: fixed;
  inset: 0;
  z-index: 5000;
  background: #fff;
}
/* A full-width line of text is hard to read: the log and the composer keep a reading measure. */
.rlt2--full :deep(.rtl__entries),
.rlt2--full :deep(.rtl__earlier),
.rlt2--full :deep(.rtl__compose-box) {
  width: 100%;
  max-width: 1100px;
  margin-left: auto;
  margin-right: auto;
}
.rlt2__where {
  display: flex;
  align-items: center;
  gap: 8px;
  flex: 0 0 auto;
  min-height: 40px;
  border-bottom: 1px solid var(--line, #dde6e8);
  background: #fff;
  font-size: 14px;
  color: var(--ink-900, #16262c);
}
.rlt2__where-title {
  min-width: 0;
  font-weight: 600;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.rlt2__where-no {
  color: var(--q-primary);
  font-variant-numeric: tabular-nums;
}
.rlt2__where-tab {
  flex: 0 0 auto;
  padding: 1px 8px;
  border-radius: 999px;
  background: var(--brand-100, #e3f1f8);
  color: var(--q-primary);
  font-size: 11.5px;
  font-weight: 600;
}
</style>

<style>
/* The page behind must not scroll while the log covers it. */
body.rlt2-fullscreen-open {
  overflow: hidden;
}
</style>
