<template>
  <div :class="['app-autosave', `app-autosave--${state}`]" role="status" aria-live="polite">
    <q-spinner v-if="state === 'saving'" size="16px" color="primary" />
    <q-icon v-else :name="icon" size="18px" />
    <span class="app-autosave__text">{{ text }}</span>
    <q-btn v-if="showSaveNow" flat dense no-caps size="sm" color="primary" label="Save now" class="app-autosave__now" @click="$emit('save-now')" />
    <q-tooltip v-if="message && state !== 'error' && state !== 'blocked'" :delay="400">{{ message }}</q-tooltip>
  </div>
</template>

<script setup>
// The one place an auto-saving form says where it stands: idle, due, saving, saved (and when), blocked
// by validation, or failed with the server's sentence. No toasts: this line is the feedback.
import { computed, ref, onMounted, onBeforeUnmount } from "vue";

const props = defineProps({
  // idle | pending | saving | saved | blocked | error
  state: { type: String, default: "idle" },
  message: { type: String, default: "" },
  savedAt: { type: [Date, String, Number], default: null },
  idleText: { type: String, default: "Changes save as you edit" }
});

defineEmits(["save-now"]);

// "Saved just now" ages into "Saved at 10:42" without the page having to re-render on its own.
const now = ref(Date.now());
let tick = null;
onMounted(() => { tick = setInterval(() => { now.value = Date.now(); }, 15000); });
onBeforeUnmount(() => clearInterval(tick));

const savedLabel = computed(() => {
  if (!props.savedAt) return "Saved";
  const at = new Date(props.savedAt).getTime();
  const seconds = Math.round((now.value - at) / 1000);
  if (seconds < 60) return "Saved just now";
  if (seconds < 3600) return `Saved ${Math.round(seconds / 60)} min ago`;
  return `Saved at ${new Date(at).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}`;
});

const text = computed(() => {
  switch (props.state) {
    case "pending": return "Unsaved changes — saving shortly";
    case "saving": return "Saving…";
    case "saved": return savedLabel.value;
    case "blocked": return props.message || "Changes can't be saved yet";
    case "error": return props.message ? `Couldn't save — ${props.message}` : "Couldn't save — will retry as you edit";
    default: return props.idleText;
  }
});

const icon = computed(() => {
  switch (props.state) {
    case "pending": return "o_cloud_queue";
    case "saved": return "o_cloud_done";
    case "blocked": return "o_error_outline";
    case "error": return "o_cloud_off";
    default: return "o_cloud_done";
  }
});

const showSaveNow = computed(() => props.state === "pending" || props.state === "error");
</script>

<style scoped>
.app-autosave {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 28px;
  padding: 0 10px;
  border-radius: 999px;
  font-size: 12.5px;
  color: var(--ink-500, #647a82);
  background: #f3f6f8;
  max-width: 100%;
}
.app-autosave__text {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.app-autosave--pending {
  color: var(--ink-700, #3a4d54);
  background: #eef3f5;
}
.app-autosave--saving {
  color: var(--q-primary);
  background: var(--brand-050, #f3f8fb);
}
.app-autosave--saved {
  color: #1b5e34;
  background: #e8f5ec;
}
.app-autosave--blocked {
  color: #8a5300;
  background: #fff4e5;
}
.app-autosave--error {
  color: #8a1c12;
  background: #fdecea;
}
.app-autosave__now {
  margin-left: 2px;
}
</style>
