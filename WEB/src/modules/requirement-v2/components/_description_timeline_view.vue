<template>
  <div class="rtl column no-wrap">
    <!-- The log, oldest at the top and the newest entry at the foot, scrolling on its own above the
         composer; the pages before arrive as the reader scrolls up, their place on the page kept. -->
    <div ref="threadRef" class="rtl__thread col scroll q-pa-md" @scroll.passive="onScroll">
      <div v-if="loading && !entries.length" class="row flex-center q-pa-xl">
        <q-spinner color="primary" size="32px" />
      </div>

      <app-empty-state
        v-else-if="!entries.length"
        icon="o_history_edu"
        title="No Description Log yet"
        :text="readonly ? '' : 'Describe the requirement below to start the Description Log.'"
      />

      <template v-else>
        <div class="rtl__earlier" aria-live="polite">
          <q-spinner v-if="loadingMore" size="18px" color="primary" />
          <button v-else-if="hasMore" type="button" class="rtl__earlier-btn" @click="loadOlder">Load earlier entries</button>
        </div>

        <div class="rtl__entries">
          <template v-for="row in rows" :key="row.key">
            <div v-if="row.kind === 'day'" class="rtl__day"><span>{{ row.label }}</span></div>

            <div v-else class="rtl__entry">
              <div class="rtl__rail"><span class="rtl__dot" /></div>

              <!-- Editing one of the reader's own entries, in place. -->
              <div v-if="editingId === row.entry.id" class="rtl__card rtl__card--editing">
                <q-editor
                  v-model="editingValue"
                  :toolbar="toolbar"
                  :fonts="fonts"
                  :dense="$q.screen.lt.md"
                  min-height="5rem"
                  class="rtl__editor"
                  @blur="onEditorBlur"
                />
                <div class="row justify-end q-gutter-sm q-mt-sm">
                  <q-btn flat dense no-caps color="grey-8" label="Cancel" @mousedown.prevent @click="cancelEdit" />
                  <q-btn
                    unelevated dense no-caps color="primary" label="Save"
                    :loading="savingEdit"
                    :disable="savingEdit || sending || !hasRichTextContent(editingValue)"
                    @click="save(row.entry)"
                  />
                </div>
              </div>

              <div v-else class="rtl__card">
                <div v-if="showMeta || row.entry.isRequirementDescription || (!readonly && row.entry.mine)" class="rtl__meta">
                  <template v-if="showMeta">
                    <span class="rtl__who">{{ row.entry.who }}</span>
                    <span class="rtl__when">{{ row.entry.when }}</span>
                  </template>
                  <q-badge v-if="row.entry.isRequirementDescription" color="blue-1" text-color="primary" class="rtl__badge">Original description</q-badge>
                  <q-space />
                  <template v-if="!readonly && row.entry.mine">
                    <q-btn flat round dense size="sm" color="primary" icon="o_edit" class="rtl__action" @click="startEdit(row.entry)">
                      <q-tooltip>Edit</q-tooltip>
                    </q-btn>
                    <q-btn
                      v-if="!row.entry.isRequirementDescription"
                      flat round dense size="sm" color="negative" icon="o_delete" class="rtl__action"
                      @click="remove(row.entry)"
                    >
                      <q-tooltip>Delete</q-tooltip>
                    </q-btn>
                  </template>
                </div>
                <!-- eslint-disable-next-line vue/no-v-html -->
                <div class="rtl__body RichTextEditor" v-html="row.entry.description || '—'" />
              </div>
            </div>
          </template>
        </div>
      </template>
    </div>

    <!-- Composer: at the foot, for those who may write; one line until it is opened, so the log has
         the room. -->
    <div v-if="!readonly && !composing" class="rtl__composer rtl__composer--closed q-px-md">
      <div class="rtl__compose-box rtl__compose-box--closed">
        <button type="button" :class="['rtl__open-btn', { 'rtl__open-btn--draft': hasRichTextContent(draft) }]" @click="openComposer">
          <q-icon name="o_edit_note" size="20px" color="primary" />
          <span>{{ hasRichTextContent(draft) ? "Continue your entry…" : "Add to the Description Log…" }}</span>
        </button>
      </div>
    </div>
    <div v-else-if="!readonly" class="rtl__composer q-pa-md">
      <div class="rtl__compose-box">
        <q-editor
          ref="draftEditorRef"
          v-model="draft"
          :toolbar="toolbar"
          :fonts="fonts"
          :dense="$q.screen.lt.md"
          placeholder="Add to the Description Log…"
          min-height="4rem"
          max-height="14rem"
          class="rtl__editor"
        />
        <div class="row items-center q-mt-xs">
          <div class="text-caption text-grey-7">
            {{ hasMainDescription ? "Each entry is kept with its author and time." : "The first entry becomes the requirement's description." }}
          </div>
          <q-space />
          <q-btn flat round dense color="grey-8" icon="o_expand_more" aria-label="Hide the editor" class="q-mr-xs" @click="composing = false">
            <q-tooltip>Hide the editor (what you wrote is kept)</q-tooltip>
          </q-btn>
          <q-btn
            round unelevated color="primary" icon="o_send" aria-label="Add entry"
            :loading="sending"
            :disable="sending || savingEdit || !hasRichTextContent(draft)"
            @click="save()"
          >
            <q-tooltip>Add to the Description Log</q-tooltip>
          </q-btn>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
// The requirement log as a chat: the description and every change-log entry since, oldest at the top and
// the newest right above the composer, the latest ten read first and the earlier pages as the reader
// scrolls up. Editors see their own entries editable in place; the first entry written to a requirement
// with no description becomes that description, later ones are change-log entries.
import { ref, computed, watch, nextTick } from "vue";
import { useQuasar } from "quasar";
import { useAuthStore } from "stores/auth";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useChatThread, pageFromList } from "composables/useChatThread";
import AppEmptyState from "components/common/AppEmptyState.vue";
import { getEditorConfig } from "src/composables/form-inputs/useEditorSettings.js";
import { hasRichTextContent } from "src/utils/richText";
import { personName, dayLabel } from "modules/requirement-v2/utils/options";
import requirementService from "modules/requirement-v2/requirementV2.service";

const props = defineProps({
  id: { type: String, default: "" },
  // Read the log without writing to it: no composer, no edit or delete.
  readonly: { type: Boolean, default: false },
  // Author and time on each entry.
  showMeta: { type: Boolean, default: true }
});

// `changed` tells the host the log changed (counts and activity refresh); `loaded` carries the entry count.
const emit = defineEmits(["changed", "loaded"]);

const $q = useQuasar();
const notify = useNotify();
const { confirm } = useConfirm();
const { fonts, toolbar } = getEditorConfig($q);
const user = useAuthStore().user;

const PAGE_SIZE = 10;
const threadRef = ref(null);

// The first line of what the server said, for a toast; the fallback when it said nothing readable.
const errorText = (error, fallback) => {
  const data = error?.response?.data;
  const text = typeof data === "string" ? data : data?.message || data?.title || error?.message;
  return text ? String(text).split("\n")[0].slice(0, 200) : fallback;
};

const toEntry = (item) => ({
  id: item.id,
  description: item.description,
  who: (item.createdByName || "").trim() || personName(item.createdBy) || "—",
  when: item.createdOnUtc || "",
  day: dayLabel(item.createdOnUtc),
  mine: !!user?.username && (item.createdByUserName || item.createdBy?.userName) === user.username,
  isRequirementDescription: !!item.isRequirementDescription
});

// ---- Reading: one page at a time, newest first ----
// An API without the thread endpoint yet answers 404; the whole log is then read once per load and paged here.
let threadUnsupported = false;
let fallback = null;

const readWholeLog = async () => {
  const resp = await requirementService.getAllRequirementDescriptionsById(props.id, true);
  const list = [];
  let hasDescription = false;
  for (const requirement of resp?.requirementList || []) {
    for (const log of requirement.requirementChangeLog || []) list.push(toEntry(log));
    if (hasRichTextContent(requirement.description)) {
      hasDescription = true;
      list.push(toEntry({ ...requirement, isRequirementDescription: true }));
    }
  }
  return { list, hasDescription };
};

const fetchLogPage = async ({ beforeId, take }) => {
  if (!threadUnsupported) {
    try {
      const page = await requirementService.getRequirementLogThread(props.id, { beforeId, take }, { silent: true });
      return { ...page, data: page.data.map(toEntry) };
    } catch (error) {
      if (error?.response?.status !== 404) throw error;
      threadUnsupported = true;
    }
  }
  if (!fallback || !beforeId) fallback = await readWholeLog();
  return { ...pageFromList(fallback.list, { beforeId, take }), hasDescription: fallback.hasDescription };
};

const thread = useChatThread({ fetchPage: fetchLogPage, pageSize: PAGE_SIZE, scroller: threadRef });
const { items: entries, page, hasMore, total, loading, loadingMore, loadOlder, onScroll } = thread;

const hasMainDescription = computed(() => !!page.value?.hasDescription);

// Entries under the day they were written on, as a chat shows them.
const rows = computed(() => {
  const out = [];
  let lastDay = null;
  for (const entry of entries.value) {
    if (entry.day && entry.day !== lastDay) {
      out.push({ kind: "day", key: `day-${entry.id}`, label: entry.day });
      lastDay = entry.day;
    }
    out.push({ kind: "entry", key: entry.id, entry });
  }
  return out;
});

const load = async () => {
  if (!props.id) return;
  try {
    await thread.load();
    emit("loaded", total.value);
  } catch (error) {
    console.error("Error while loading the requirement log", error);
    notify.error(errorText(error, "The Description Log could not be loaded."));
  }
};

// ---- Inline edit ----
const sending = ref(false);
const savingEdit = ref(false);
const draft = ref("");
// The editor is closed until asked for; what was written stays in `draft` while it is closed.
const composing = ref(false);
const draftEditorRef = ref(null);
const openComposer = async () => {
  composing.value = true;
  await nextTick();
  draftEditorRef.value?.focus?.();
  thread.scrollToBottom();
};
const editingId = ref(null);
const editingValue = ref("");
let editingOriginal = "";
let cancelling = false;

const startEdit = (entry) => {
  editingId.value = entry.id;
  editingValue.value = entry.description;
  editingOriginal = entry.description;
  cancelling = false;
};

const cancelEdit = () => {
  cancelling = true;
  editingId.value = null;
  editingValue.value = "";
  setTimeout(() => { cancelling = false; }, 0);
};

// Leaving the editor with nothing changed simply closes it; a toolbar click is not leaving.
const onEditorBlur = (event) => {
  if (cancelling) return;
  if (event?.relatedTarget?.closest?.(".q-editor__toolbar")) return;
  if ((editingValue.value || "").trim() === (editingOriginal || "").trim()) editingId.value = null;
};

// ---- Save (new entry, or an edit of one) ----
const save = async (entry = null) => {
  if (sending.value || savingEdit.value) return;
  const isEditing = !!entry;
  const value = (isEditing ? editingValue.value : draft.value) || "";
  if (!hasRichTextContent(value)) {
    if (isEditing) editingId.value = null;
    return;
  }

  if (isEditing) savingEdit.value = true;
  else sending.value = true;

  try {
    if (entry?.isRequirementDescription) {
      await requirementService.updateRequirementDescription({ id: entry.id, description: value });
    } else if (isEditing) {
      await requirementService.saveResponseLogDescription({
        id: entry.id,
        requirementId: props.id,
        employeeId: user?.employeeId,
        description: value
      });
    } else if (!hasMainDescription.value) {
      // No description yet: the first entry IS the description.
      await requirementService.updateRequirementDescription({ id: props.id, description: value });
    } else {
      await requirementService.saveResponseLogDescription({
        id: null,
        requirementId: props.id,
        employeeId: user?.employeeId,
        description: value
      });
    }

    notify.success(isEditing ? "Description Log entry updated." : "Description Log entry added.");
    emit("changed");
    if (isEditing) {
      editingId.value = null;
      editingValue.value = "";
    } else {
      draft.value = "";
      composing.value = false;
    }
    // Reading the newest page again lands the reader on the entry just written, right above the composer.
    await load();
  } catch (error) {
    console.error("Error saving the log entry", error);
  } finally {
    sending.value = false;
    savingEdit.value = false;
  }
};

// ---- Delete ----
const remove = async (entry) => {
  const ok = await confirm({
    title: "Delete Description Log entry",
    message: "Delete this Description Log entry? This cannot be undone.",
    confirmLabel: "Delete",
    type: "danger"
  });
  if (!ok) return;
  try {
    await requirementService.deleteRequirementChangeLog(entry.id);
    notify.success("Description Log entry deleted.");
    emit("changed");
    await load();
  } catch (error) {
    console.error("Error deleting the log entry", error);
  }
};

watch(() => props.id, () => load(), { immediate: true });

// A taller pane (full screen) may have room for more than the page read so far.
const fillPane = async () => {
  for (let i = 0; i < 3 && hasMore.value; i += 1) {
    const el = threadRef.value;
    if (!el || el.scrollHeight > el.clientHeight + 1) return;
    if (!(await loadOlder())) return;
    await thread.scrollToBottom();
  }
};

defineExpose({ reload: load, scroller: () => threadRef.value, fillPane });
</script>

<style scoped>
.rtl {
  height: 100%;
  min-height: 0;
}
.rtl__thread {
  min-height: 0;
  overflow: auto;
  background: #f7f9fb;
}
/* The page before loads itself as the reader nears the top; the button is for those who would rather ask. */
.rtl__earlier {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 26px;
  margin-bottom: 6px;
}
.rtl__earlier-btn {
  min-height: 24px;
  padding: 0 12px;
  border: 1px solid var(--line, #dde6e8);
  border-radius: 999px;
  background: #fff;
  color: var(--q-primary);
  font: inherit;
  font-size: 11.5px;
  font-weight: 500;
  cursor: pointer;
}
.rtl__earlier-btn:hover {
  border-color: var(--q-primary);
}
.rtl__entries {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.rtl__day {
  display: flex;
  justify-content: center;
  margin: 4px 0 0;
}
.rtl__day span {
  padding: 2px 10px;
  border-radius: 999px;
  background: #e7ebf0;
  color: var(--ink-500, #647a82);
  font-size: 10.5px;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}
.rtl__entry {
  display: flex;
  align-items: stretch;
  gap: 10px;
}
.rtl__rail {
  position: relative;
  width: 14px;
  flex: 0 0 14px;
}
.rtl__rail::before {
  content: "";
  position: absolute;
  top: 14px;
  bottom: -14px;
  left: 6px;
  width: 2px;
  background: var(--line, #dde6e8);
}
.rtl__entry:last-child .rtl__rail::before {
  display: none;
}
.rtl__dot {
  position: absolute;
  top: 10px;
  left: 2px;
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: #fff;
  border: 2px solid var(--q-primary);
}
.rtl__card {
  flex: 1 1 auto;
  min-width: 0;
  border: 1px solid var(--line, #dde6e8);
  border-radius: 10px;
  background: #fff;
  padding: 8px 12px;
}
.rtl__card--editing {
  border-color: var(--q-primary);
}
.rtl__meta {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 4px 10px;
  font-size: 11.5px;
  color: var(--ink-500, #647a82);
  margin-bottom: 4px;
  min-height: 24px;
}
.rtl__who {
  font-weight: 600;
  color: var(--ink-700, #3a4d54);
}
.rtl__badge {
  font-size: 10.5px;
}
/* The row's own actions show on hover only, so the log reads as a log rather than a toolbar. */
.rtl__action {
  opacity: 0;
  transition: opacity 0.15s ease;
}
.rtl__card:hover .rtl__action,
.rtl__action:focus-visible {
  opacity: 1;
}
.rtl__body {
  font-size: 13.5px;
  color: var(--ink-900, #16262c);
  word-break: break-word;
}
.rtl__body :deep(p) {
  margin: 0 0 6px;
}
.rtl__body :deep(p:last-child) {
  margin-bottom: 0;
}
.rtl__body :deep(img) {
  max-width: 100%;
  height: auto;
}
.rtl__composer {
  flex: 0 0 auto;
  border-top: 1px solid var(--line, #dde6e8);
  background: #fff;
}
.rtl__compose-box {
  border: 1px solid var(--line, #dde6e8);
  border-radius: 12px;
  padding: 6px 8px 8px;
  transition: border-color 0.15s ease, box-shadow 0.15s ease;
}
.rtl__compose-box--closed {
  border: none;
  padding: 0;
}
.rtl__compose-box:focus-within {
  border-color: var(--q-primary);
  box-shadow: 0 0 0 3px rgba(27, 117, 171, 0.12);
}
/* The composer closed: a single line, so the thread has the room. */
.rtl__compose-box--closed:focus-within {
  box-shadow: none;
}
.rtl__composer--closed {
  padding-top: 8px;
  padding-bottom: 8px;
}
.rtl__open-btn {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  min-height: 36px;
  padding: 0 12px;
  border: 1px solid var(--line, #dde6e8);
  border-radius: 12px;
  background: #fff;
  color: var(--ink-500, #647a82);
  font: inherit;
  font-size: 13.5px;
  text-align: left;
  cursor: text;
}
.rtl__open-btn:hover,
.rtl__open-btn:focus-visible {
  border-color: var(--q-primary);
  outline: none;
}
.rtl__open-btn--draft {
  color: var(--ink-900, #16262c);
}
.rtl__editor {
  border: none;
}
.rtl__editor :deep(.q-editor__toolbar) {
  border-bottom: 1px solid rgba(0, 0, 0, 0.08);
}
.rtl__editor :deep(.q-editor__content) {
  font-size: 13.5px;
}
</style>
