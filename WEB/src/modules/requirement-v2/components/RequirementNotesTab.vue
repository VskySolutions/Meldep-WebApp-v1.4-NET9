<template>
  <!-- In full screen the whole tab is moved to <body> and laid over the window; nothing in it is rebuilt,
       so a note being written stays as it is. -->
  <teleport to="body" :disabled="!fullscreen">
  <div :class="['rnt column no-wrap', { 'rnt--full': fullscreen }]" :aria-label="fullscreen ? 'Notes, full screen' : undefined">
    <!-- Over the whole window the page's header is out of sight: say whose notes these are. -->
    <div v-if="fullscreen" class="rnt__where q-px-md">
      <q-icon name="o_sticky_note_2" size="18px" color="primary" />
      <span class="rnt__where-title"><span v-if="model?.requirementNumber" class="rnt__where-no">#{{ model.requirementNumber }}</span> {{ model?.title || "Requirement" }}</span>
      <span class="rnt__where-tab">Notes</span>
    </div>

    <!-- Which notes: every kind, one kind, or the work & progress written on the tasks. -->
    <div class="rnt__filters q-px-md q-pt-md">
      <button type="button" :class="['rnt__chip', { 'rnt__chip--on': filter === 'all' }]" @click="filter = 'all'">
        All notes <span class="rnt__chip-count">{{ allCount }}</span>
        <q-tooltip>Every note on this requirement, all types together</q-tooltip>
      </button>
      <span class="rnt__sep" aria-hidden="true" />
      <button
        v-for="type in noteTypes"
        :key="type.value"
        type="button"
        :class="['rnt__chip', { 'rnt__chip--on': filter === type.value }]"
        @click="filter = type.value"
      >
        {{ type.text }} <span class="rnt__chip-count">{{ countByType[type.value] || 0 }}</span>
        <q-tooltip>Show only the {{ type.text }} notes</q-tooltip>
      </button>
      <span class="rnt__sep" aria-hidden="true" />
      <button type="button" :class="['rnt__chip', 'rnt__chip--progress', { 'rnt__chip--on': filter === 'progress' }]" @click="filter = 'progress'">
        <q-icon name="o_task_alt" size="14px" /> Work &amp; progress
        <q-tooltip>Notes written on this requirement's tasks and activities. They are added from the task, not here.</q-tooltip>
      </button>
      <q-space />
      <!-- Searches every note of the requirement, not only the ones scrolled to so far. -->
      <q-input
        v-if="filter !== 'progress'"
        v-model="search"
        dense
        outlined
        clearable
        debounce="250"
        placeholder="Search notes"
        aria-label="Search notes"
        class="rnt__search"
      >
        <template #prepend><q-icon name="o_search" size="18px" /></template>
      </q-input>
      <q-btn flat round dense color="grey-8" icon="o_refresh" :loading="loading" aria-label="Refresh" @click="load()">
        <q-tooltip>Refresh</q-tooltip>
      </q-btn>
      <q-btn flat round dense :color="fullscreen ? 'primary' : 'grey-8'" :icon="fullscreen ? 'o_fullscreen_exit' : 'o_fullscreen'" :aria-label="fullscreen ? 'Exit full screen' : 'Full screen'" class="rnt__full-btn" @click="toggleFullscreen">
        <q-tooltip>{{ fullscreen ? "Exit full screen (Esc)" : "Full screen" }}</q-tooltip>
      </q-btn>
    </div>

    <!-- The notes themselves, read like a chat: oldest at the top, the newest right above the composer,
         the latest page first and the earlier ones as the reader scrolls up. -->
    <div ref="threadRef" class="rnt__thread col scroll q-pa-md" @scroll.passive="onScroll">
      <team-task-updates v-if="filter === 'progress'" :id="id" />

      <template v-else>
        <div v-if="loading && !notes.length" class="row flex-center q-pa-xl">
          <q-spinner color="primary" size="32px" />
        </div>

        <app-empty-state v-else-if="!notes.length && searching" icon="o_search_off" :title="`No note matches “${needle}”`" :text="filter === 'all' ? '' : `Among ${filterLabel.toLowerCase()}. Other kinds of note may match.`" />

        <app-empty-state
          v-else-if="!notes.length"
          icon="o_sticky_note_2"
          :title="filter === 'all' ? 'No notes yet' : `No ${filterLabel.toLowerCase()} yet`"
          :text="canNotes ? 'Write the first one below. Type @ to bring somebody in.' : ''"
        />

        <template v-else>
          <div v-if="searching" class="rnt__found" aria-live="polite">{{ foundLabel }}</div>
          <div class="rnt__earlier" aria-live="polite">
            <q-spinner v-if="loadingMore" size="18px" color="primary" />
            <button v-else-if="hasMore" type="button" class="rnt__earlier-btn" @click="loadOlder">Load earlier notes</button>
          </div>

          <div class="rnt__list">
            <template v-for="row in rows" :key="row.key">
              <div v-if="row.kind === 'day'" class="rnt__day"><span>{{ row.label }}</span></div>

              <article v-else :class="['rnt__note', { 'rnt__note--mine': row.note.mine, 'rnt__note--editing': editingId === row.note.id }]">
                <app-avatar v-if="!row.note.mine" :name="row.note.who" :size="30" class="rnt__avatar" />
                <div class="rnt__card">
                  <div class="rnt__meta">
                    <span class="rnt__who">{{ row.note.mine ? "You" : row.note.who }}</span>
                    <q-badge v-if="row.note.typeLabel" color="blue-1" text-color="primary" class="rnt__type">{{ row.note.typeLabel }}</q-badge>
                    <span class="rnt__when">{{ row.note.when }}</span>
                    <q-space />
                    <template v-if="row.note.mine && canNotes && editingId !== row.note.id">
                      <q-btn flat round dense size="sm" color="primary" icon="o_edit" class="rnt__action" @click="startEdit(row.note)">
                        <q-tooltip>Edit</q-tooltip>
                      </q-btn>
                      <q-btn flat round dense size="sm" color="negative" icon="o_delete" class="rnt__action" @click="remove(row.note)">
                        <q-tooltip>Delete</q-tooltip>
                      </q-btn>
                    </template>
                  </div>

                  <template v-if="editingId === row.note.id">
                    <q-editor
                      ref="editEditorRef"
                      v-model="editingValue"
                      :toolbar="toolbar"
                      :fonts="fonts"
                      :dense="$q.screen.lt.md"
                      min-height="5rem"
                      class="rnt__editor"
                      @keyup="editMentions.onKeyUp"
                      @keydown="editMentions.onKeyDown"
                    />
                    <mention-list :mentions="editMentions" />
                    <div class="row items-center q-gutter-sm q-mt-sm">
                      <q-select
                        v-model="editingTypeId"
                        dense
                        outlined
                        options-dense
                        emit-value
                        map-options
                        option-label="text"
                        option-value="value"
                        :options="noteTypes"
                        class="rnt__type-select"
                        aria-label="Note type"
                      />
                      <q-space />
                      <q-btn flat dense no-caps color="grey-8" label="Cancel" @mousedown.prevent @click="cancelEdit" />
                      <q-btn unelevated dense no-caps color="primary" label="Save" :loading="savingEdit" :disable="savingEdit || !hasRichTextContent(editingValue)" @click="saveEdit(row.note)" />
                    </div>
                  </template>
                  <!-- eslint-disable-next-line vue/no-v-html -->
                  <div v-else class="rnt__body RichTextEditor" v-html="searching ? marked(row.note) : row.note.html" />
                </div>
              </article>
            </template>
          </div>
        </template>
      </template>
    </div>

    <!-- Composer, at the foot for those who may write: one line until it is opened, so the notes have
         the room. -->
    <div v-if="canNotes && filter !== 'progress' && !composing" class="rnt__composer rnt__composer--closed q-px-md">
      <div class="rnt__compose-box rnt__compose-box--closed">
        <button type="button" :class="['rnt__open-btn', { 'rnt__open-btn--draft': hasRichTextContent(draft) }]" @click="openComposer">
          <q-icon name="o_edit_note" size="20px" color="primary" />
          <span>{{ hasRichTextContent(draft) ? "Continue your note…" : "Write a note…" }}</span>
        </button>
      </div>
    </div>
    <div v-else-if="canNotes && filter !== 'progress'" class="rnt__composer q-pa-md">
      <div class="rnt__compose-box">
        <q-editor
          ref="draftEditorRef"
          v-model="draft"
          :toolbar="toolbar"
          :fonts="fonts"
          :dense="$q.screen.lt.md"
          placeholder="Write a note…  @ to mention someone"
          min-height="4rem"
          max-height="14rem"
          class="rnt__editor"
          @keyup="draftMentions.onKeyUp"
          @keydown="draftMentions.onKeyDown"
        />
        <mention-list :mentions="draftMentions" />
        <div class="rnt__compose-bar">
          <div class="rnt__types" role="radiogroup" aria-label="Note type">
            <span class="rnt__types-label">
              Type:
              <q-tooltip>The type this note is saved under. Picking one also shows the notes of that type above.</q-tooltip>
            </span>
            <button
              v-for="type in noteTypes"
              :key="type.value"
              type="button"
              role="radio"
              :aria-checked="String(draftTypeId === type.value)"
              :class="['rnt__type-chip', { 'rnt__type-chip--on': draftTypeId === type.value }]"
              @click="pickDraftType(type.value)"
            >
              {{ type.text }}
              <q-tooltip>Save this note as {{ type.text }}</q-tooltip>
            </button>
          </div>
          <q-space />
          <q-btn flat round dense color="grey-8" icon="o_expand_more" aria-label="Hide the editor" @click="composing = false">
            <q-tooltip>Hide the editor (what you wrote is kept)</q-tooltip>
          </q-btn>
          <q-btn
            round unelevated color="primary" icon="o_send" aria-label="Add note"
            :loading="sending"
            :disable="sending || !hasRichTextContent(draft) || !draftTypeId"
            @click="send"
          >
            <q-tooltip>Add note</q-tooltip>
          </q-btn>
        </div>
      </div>
    </div>
  </div>
  </teleport>
</template>

<script setup>
// The notes of a requirement — client meeting updates, weekly status, next steps, roadblocks — as one
// chat-like thread: the newest notes right above the composer, the earlier pages as the reader scrolls
// up, one kind at a time when filtered, the work & progress notes from its tasks one chip away, and
// @mentions for those who may write. The thread can be searched (every note, not only those on screen)
// and opened over the whole window.
import { ref, computed, watch, nextTick, h, defineComponent, onBeforeUnmount, onDeactivated } from "vue";
import { useQuasar } from "quasar";
import { useAuthStore } from "stores/auth";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useMentions } from "composables/useMentions";
import { useChatThread, pageFromList } from "composables/useChatThread";
import AppEmptyState from "components/common/AppEmptyState.vue";
import AppAvatar from "components/common/AppAvatar.vue";
import TeamTaskUpdates from "modules/requirement-v2/components/_teamTaskUpdatesView.vue";
import { getEditorConfig } from "src/composables/form-inputs/useEditorSettings.js";
import { hasRichTextContent, stripHtml } from "src/utils/richText";
import { personName, dayLabel } from "modules/requirement-v2/utils/options";
import requirementService from "modules/requirement-v2/requirementV2.service";
import commonService from "services/common.service";
import employeesService from "src/modules/employee/employee.service";

const props = defineProps({
  id: { type: String, required: true },
  // The requirement (project id / name and title go on every note, as the API expects).
  model: { type: Object, default: null },
  canNotes: { type: Boolean, default: false },
  // Open on one kind ("progress", or a note type id).
  initialFilter: { type: String, default: "all" }
});

const emit = defineEmits(["changed"]);

const $q = useQuasar();
const notify = useNotify();
const { confirm } = useConfirm();
const { fonts, toolbar } = getEditorConfig($q);
const user = useAuthStore().user;

const PAGE_SIZE = 10;

// ---- Mention suggestion list: shared by the composer and the inline editor ----
const MentionList = defineComponent({
  props: { mentions: { type: Object, required: true } },
  setup (p) {
    return () => p.mentions.open.value
      ? h("div", { class: "rnt__mentions" }, p.mentions.matches.value.map((person, index) =>
        h("button", {
          type: "button",
          class: ["rnt__mention", { "rnt__mention--active": index === p.mentions.activeIndex.value }],
          onMousedown: (e) => e.preventDefault(),
          onClick: () => p.mentions.pick(person)
        }, person.text)))
      : null;
  }
});

// ---- Data ----
const noteTypes = ref([]);
const people = ref([]);
const filter = ref(props.initialFilter || "all");
const threadRef = ref(null);

const noteTypeFilter = computed(() => (filter.value === "all" || filter.value === "progress" ? null : filter.value));

// The first line of what the server said, for a toast; the fallback when it said nothing readable.
const errorText = (error, fallback) => {
  const data = error?.response?.data;
  const text = typeof data === "string" ? data : data?.message || data?.title || error?.message;
  return text ? String(text).split("\n")[0].slice(0, 200) : fallback;
};

const toNote = (note) => {
  const who = personName(note.user) || "—";
  const typeLabel = note.noteType?.dropDownValue || "";
  return {
    id: note.id,
    html: note.note,
    who,
    when: note.createdOnUtc,
    day: dayLabel(note.createdOnUtc),
    typeId: note.noteTypeId || note.noteType?.id || null,
    typeLabel,
    mine: !!user?.username && note.user?.userName === user.username,
    // What a search reads: the words of the note, who wrote it, its kind and its date.
    text: `${stripHtml(note.note)} ${who} ${typeLabel} ${note.createdOnUtc || ""}`.toLowerCase()
  };
};

// ---- Search ----
// Every word typed must be in the note (its text, its author, its kind or its date).
const search = ref("");
const needle = computed(() => (search.value || "").trim());
const searchWords = computed(() => needle.value.toLowerCase().split(/\s+/).filter(Boolean));
const searching = computed(() => searchWords.value.length > 0);
const matches = (note) => searchWords.value.every((word) => note.text.includes(word));

// One page at a time, newest first, from the thread endpoint. Two cases read every note of the requirement
// once instead and page that list here, so the screen reads the same either way: a search (which has to
// look at notes the reader has not scrolled to), and an API without the thread endpoint yet (it answers 404).
let threadUnsupported = false;
let allNotes = null;

const readAllNotes = async () => {
  if (!allNotes) allNotes = ((await commonService.getAllNoteByTypeAndRecord(props.id, "Requirement", true)) || []).map(toNote);
  return allNotes;
};

const fetchNotesPage = async ({ beforeId, take }) => {
  if (!searching.value && !threadUnsupported) {
    try {
      const page = await requirementService.getNoteThread(props.id, "Requirement", { noteTypeId: noteTypeFilter.value, beforeId, take }, { silent: true });
      return { ...page, data: (page?.data || []).map(toNote) };
    } catch (error) {
      if (error?.response?.status !== 404) throw error;
      threadUnsupported = true;
    }
  }
  const everything = await readAllNotes();
  // While searching, the chips count what matches, kind by kind.
  const pool = searching.value ? everything.filter(matches) : everything;
  const counts = {};
  for (const note of pool) counts[note.typeId] = (counts[note.typeId] || 0) + 1;
  const list = noteTypeFilter.value ? pool.filter((n) => n.typeId === noteTypeFilter.value) : pool;
  return {
    ...pageFromList(list, { beforeId, take }),
    countsByType: Object.entries(counts).map(([noteTypeId, count]) => ({ noteTypeId, count })),
    searchedIn: everything.length
  };
};

const thread = useChatThread({ fetchPage: fetchNotesPage, pageSize: PAGE_SIZE, scroller: threadRef });
const { items: notes, page, hasMore, total, loading, loadingMore, loadOlder, onScroll } = thread;

// `keep` reuses the notes already read for searching (another word, another kind of note); anything that
// may have changed them (a write, Refresh, another requirement) reads them again.
const load = async ({ keep = false } = {}) => {
  if (!keep) {
    allNotes = null;
    markedCache.clear();
  }
  if (!props.id || filter.value === "progress") return;
  try {
    await thread.load();
  } catch (error) {
    console.error("Error loading requirement notes", error);
    notify.error(errorText(error, "The notes could not be loaded."));
  }
};

const foundLabel = computed(() => {
  const found = total.value;
  const among = page.value?.searchedIn ?? found;
  return `${found} of ${among} note${among === 1 ? "" : "s"} match${found === 1 ? "es" : ""} “${needle.value}”`;
});

// The words searched for, marked inside the note's text (never inside its tags). Kept per note and
// search, so typing in the composer does not mark every note again.
const markedCache = new Map();
const escapeRegExp = (text) => text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
const marked = (note) => {
  const words = searchWords.value;
  if (!words.length || !note.html) return note.html;
  const key = `${note.id}|${words.join(" ")}`;
  if (markedCache.has(key)) return markedCache.get(key);
  const doc = new DOMParser().parseFromString(`<div>${note.html}</div>`, "text/html");
  const root = doc.body.firstElementChild;
  const pattern = new RegExp(words.map(escapeRegExp).join("|"), "gi");
  const walker = doc.createTreeWalker(root, NodeFilter.SHOW_TEXT);
  const texts = [];
  while (walker.nextNode()) texts.push(walker.currentNode);
  for (const node of texts) {
    const value = node.nodeValue;
    const hits = [...value.matchAll(pattern)];
    if (!hits.length) continue;
    const pieces = doc.createDocumentFragment();
    let from = 0;
    for (const hit of hits) {
      if (hit.index > from) pieces.appendChild(doc.createTextNode(value.slice(from, hit.index)));
      const mark = doc.createElement("mark");
      mark.className = "rnt__hit";
      mark.textContent = hit[0];
      pieces.appendChild(mark);
      from = hit.index + hit[0].length;
    }
    if (from < value.length) pieces.appendChild(doc.createTextNode(value.slice(from)));
    node.parentNode.replaceChild(pieces, node);
  }
  const html = root.innerHTML;
  if (markedCache.size > 400) markedCache.clear();
  markedCache.set(key, html);
  return html;
};

const loadLookups = async () => {
  const [types, employees] = await Promise.allSettled([
    commonService.getDropDown("Note Type"),
    employeesService.getAllActiveEmployeesListForDropdown(user?.siteId)
  ]);
  if (types.status === "fulfilled") {
    noteTypes.value = (types.value || [])
      .map((t) => ({ text: t.dropdownValue, value: t.id, order: Number(t.sortOrder ?? 0) }))
      .sort((a, b) => a.order - b.order);
    if (!draftTypeId.value) {
      const preferred = noteTypes.value.find((t) => t.text === "Client Meeting Updates") || noteTypes.value[0];
      draftTypeId.value = preferred?.value || null;
    }
  } else console.error(types.reason);
  if (employees.status === "fulfilled") {
    people.value = (employees.value || []).map((e) => ({
      text: [e.person?.firstName, e.person?.middleName, e.person?.lastName].filter(Boolean).join(" "),
      value: e.id
    }));
  } else console.error(employees.reason);
};

// The chips count every note of the requirement, whatever kind is on show.
const countByType = computed(() => Object.fromEntries((page.value?.countsByType || []).map((c) => [c.noteTypeId, c.count])));
const allCount = computed(() => Object.values(countByType.value).reduce((sum, n) => sum + n, 0));

// Notes under the day they were written on, as a chat shows them.
const rows = computed(() => {
  const out = [];
  let lastDay = null;
  for (const note of notes.value) {
    if (note.day && note.day !== lastDay) {
      out.push({ kind: "day", key: `day-${note.id}`, label: note.day });
      lastDay = note.day;
    }
    out.push({ kind: "note", key: note.id, note });
  }
  return out;
});

const filterLabel = computed(() => noteTypes.value.find((t) => t.value === filter.value)?.text || "notes");

// ---- Composer ----
const draft = ref("");
const draftTypeId = ref(null);
const sending = ref(false);
const draftEditorRef = ref(null);
// The editor is closed until asked for; what was written stays in `draft` while it is closed.
const composing = ref(false);
const openComposer = async () => {
  composing.value = true;
  await nextTick();
  draftEditorRef.value?.focus?.();
  thread.scrollToBottom();
};
// Choosing the kind of note to write also shows the notes of that kind, so the new one is read among them.
const pickDraftType = (typeId) => {
  draftTypeId.value = typeId;
  filter.value = typeId;
};
const draftMentions = useMentions(() => draftEditorRef.value?.getContentEl?.(), people);

const notePayload = (id, html, typeId, taggedIds) => ({
  id,
  taggedPersonId: taggedIds.join(","),
  subModuleId: props.id,
  note: html,
  type: "Requirement",
  moduleId: props.model?.project?.id || props.model?.projectId || "",
  module: props.model?.project?.name || "",
  sub_Module: props.model?.title || "",
  noteTypeId: typeId
});

const send = async () => {
  if (sending.value || !hasRichTextContent(draft.value) || !draftTypeId.value) return;
  sending.value = true;
  try {
    await commonService.saveNote(notePayload(null, draft.value, draftTypeId.value, draftMentions.extractIds()));
    notify.success("Note added.");
    draft.value = "";
    composing.value = false;
    // Reading the newest page again lands the reader on the note just written, right above the composer.
    await load();
    emit("changed");
  } catch (error) {
    console.error("Error saving the note", error);
  } finally {
    sending.value = false;
  }
};

// ---- Inline edit of the reader's own notes ----
const editingId = ref(null);
const editingValue = ref("");
const editingTypeId = ref(null);
const savingEdit = ref(false);
const editEditorRef = ref(null);
const editMentions = useMentions(() => {
  const el = Array.isArray(editEditorRef.value) ? editEditorRef.value[0] : editEditorRef.value;
  return el?.getContentEl?.();
}, people);

const startEdit = (note) => {
  editingId.value = note.id;
  editingValue.value = note.html;
  editingTypeId.value = note.typeId;
};

const cancelEdit = () => {
  editingId.value = null;
  editingValue.value = "";
};

const saveEdit = async (note) => {
  if (savingEdit.value || !hasRichTextContent(editingValue.value)) return;
  savingEdit.value = true;
  try {
    await commonService.saveNote(notePayload(note.id, editingValue.value, editingTypeId.value, editMentions.extractIds()));
    notify.success("Note updated.");
    cancelEdit();
    await load();
    emit("changed");
  } catch (error) {
    console.error("Error updating the note", error);
  } finally {
    savingEdit.value = false;
  }
};

const remove = async (note) => {
  const ok = await confirm({ title: "Delete note", message: "Delete this note? This cannot be undone.", confirmLabel: "Delete", type: "danger" });
  if (!ok) return;
  try {
    await commonService.deleteNote(note.id);
    notify.success("Note deleted.");
    await load();
    emit("changed");
  } catch (error) {
    console.error("Error deleting the note", error);
  }
};

// ---- Full screen ----
// The tab laid over the whole window (see the teleport in the template), for reading a long thread.
const fullscreen = ref(false);
const BODY_CLASS = "rnt-fullscreen-open";

// A taller pane may have room for more than the page read so far.
const fillPane = async () => {
  for (let i = 0; i < 3 && hasMore.value; i += 1) {
    const el = threadRef.value;
    if (!el || el.scrollHeight > el.clientHeight + 1) return;
    if (!(await loadOlder())) return;
    await thread.scrollToBottom();
  }
};

// Escape leaves full screen, unless it is closing something opened on top of it (a menu, a dialog, the
// mention list).
const onFullscreenKey = (event) => {
  if (event.key !== "Escape" || event.defaultPrevented) return;
  if (document.querySelector(".q-menu, .q-dialog") || draftMentions.open.value || editMentions.open.value) return;
  setFullscreen(false);
};

const setFullscreen = async (on) => {
  if (fullscreen.value === on) return;
  // A chat is read from its foot: the reader's place is how far above the newest note they stand, and
  // moving the tab in the page would otherwise send the thread back to its top.
  const before = threadRef.value;
  const fromFoot = before ? before.scrollHeight - before.scrollTop - before.clientHeight : 0;
  fullscreen.value = on;
  document.body.classList.toggle(BODY_CLASS, on);
  if (on) window.addEventListener("keydown", onFullscreenKey);
  else window.removeEventListener("keydown", onFullscreenKey);
  await nextTick();
  const after = threadRef.value;
  if (after) after.scrollTop = Math.max(0, after.scrollHeight - after.clientHeight - fromFoot);
  if (on) fillPane();
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

watch(() => props.id, () => {
  leaveFullscreen();
  search.value = "";
  load();
}, { immediate: true });
// Another kind of note, or another search, is its own thread: its newest page, scrolled to the foot.
watch(filter, (value) => {
  // Reading one kind of note, the next note written is of that kind unless another is chosen.
  if (noteTypes.value.some((type) => type.value === value)) draftTypeId.value = value;
  load({ keep: true });
});
watch(needle, () => load({ keep: true }));
loadLookups();

defineExpose({ reload: () => load(), focusComposer: openComposer });
</script>

<style scoped>
.rnt {
  height: 100%;
  min-height: 0;
}
/* Full screen: over the whole window, above the page's header and drawers and below the dialogs, menus
   and tooltips Quasar opens (those sit at 6000 and up). */
.rnt--full {
  position: fixed;
  inset: 0;
  z-index: 5000;
  background: #fff;
}
/* A full-width line of text is hard to read: the thread and the composer keep a reading measure. */
.rnt--full .rnt__list,
.rnt--full .rnt__earlier,
.rnt--full .rnt__compose-box {
  width: 100%;
  max-width: 1100px;
  margin-left: auto;
  margin-right: auto;
}
.rnt__where {
  display: flex;
  align-items: center;
  gap: 8px;
  min-height: 40px;
  border-bottom: 1px solid var(--line, #dde6e8);
  background: #fff;
  font-size: 14px;
  color: var(--ink-900, #16262c);
}
.rnt__where-title {
  min-width: 0;
  font-weight: 600;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.rnt__where-no {
  color: var(--q-primary);
  font-variant-numeric: tabular-nums;
}
.rnt__where-tab {
  flex: 0 0 auto;
  padding: 1px 8px;
  border-radius: 999px;
  background: var(--brand-100, #e3f1f8);
  color: var(--q-primary);
  font-size: 11.5px;
  font-weight: 600;
}
.rnt__search {
  width: 220px;
  max-width: 100%;
}
.rnt__search.q-field--dense :deep(.q-field__control) {
  height: 32px;
  min-height: 32px;
  border-radius: 8px;
  background: #fff;
}
.rnt__search.q-field--dense :deep(.q-field__marginal) {
  height: 32px;
}
.rnt__search.q-field--dense :deep(.q-field__native) {
  font-size: 13px;
}
/* How many notes the search found: it stays at the top of the thread, which is read from its foot. */
.rnt__found {
  position: sticky;
  top: 0;
  z-index: 1;
  width: fit-content;
  margin: 0 auto 6px;
  padding: 2px 12px;
  border: 1px solid var(--line, #dde6e8);
  border-radius: 999px;
  background: #fff;
  font-size: 11.5px;
  color: var(--ink-700, #3a4d54);
}
/* What the search found, inside a note. */
.rnt__body :deep(.rnt__hit) {
  padding: 0 1px;
  border-radius: 2px;
  background: #fff2a8;
  color: inherit;
}
.rnt__filters {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--line, #dde6e8);
  background: #fff;
}
.rnt__chip {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  min-height: 28px;
  padding: 0 10px;
  border: 1px solid var(--line, #dde6e8);
  border-radius: 999px;
  background: #fff;
  color: var(--ink-700, #3a4d54);
  font: inherit;
  font-size: 12px;
  font-weight: 500;
  cursor: pointer;
}
.rnt__chip:hover {
  border-color: var(--q-primary);
}
.rnt__chip--on {
  background: var(--q-primary);
  border-color: var(--q-primary);
  color: #fff;
}
.rnt__chip-count {
  min-width: 18px;
  padding: 0 5px;
  border-radius: 999px;
  background: rgba(0, 0, 0, 0.06);
  font-size: 10.5px;
  font-weight: 600;
  text-align: center;
}
.rnt__chip--on .rnt__chip-count {
  background: rgba(255, 255, 255, 0.25);
}
.rnt__thread {
  min-height: 0;
  overflow: auto;
  background: #f7f9fb;
}
/* The page before loads itself as the reader nears the top; the button is for those who would rather ask. */
.rnt__earlier {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 26px;
  margin-bottom: 6px;
}
.rnt__earlier-btn {
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
.rnt__earlier-btn:hover {
  border-color: var(--q-primary);
}
.rnt__list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.rnt__day {
  display: flex;
  justify-content: center;
  margin: 4px 0 0;
}
.rnt__day span {
  padding: 2px 10px;
  border-radius: 999px;
  background: #e7ebf0;
  color: var(--ink-500, #647a82);
  font-size: 10.5px;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}
/* Others on the left behind their avatar, the reader's own on the right, as a chat lays them out. */
.rnt__note {
  display: flex;
  gap: 10px;
  align-items: flex-start;
  padding-right: 12%;
}
.rnt__note--mine {
  flex-direction: row-reverse;
  padding-right: 0;
  padding-left: 12%;
}
@media (max-width: 599px) {
  .rnt__note {
    padding-right: 4%;
  }
  .rnt__note--mine {
    padding-left: 4%;
  }
}
.rnt__avatar {
  margin-top: 4px;
}
.rnt__card {
  flex: 1 1 auto;
  min-width: 0;
  border: 1px solid var(--line, #dde6e8);
  border-radius: 12px 12px 12px 3px;
  background: #fff;
  padding: 8px 12px;
}
.rnt__note--mine .rnt__card {
  background: #e3f1fd;
  border-color: #cfe4f7;
  border-radius: 12px 12px 3px 12px;
}
.rnt__note--editing .rnt__card {
  border-color: var(--q-primary);
}
.rnt__meta {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 4px 10px;
  font-size: 11.5px;
  color: var(--ink-500, #647a82);
  margin-bottom: 4px;
  min-height: 24px;
}
.rnt__who {
  font-weight: 600;
  color: var(--ink-700, #3a4d54);
}
.rnt__type {
  font-size: 10.5px;
}
.rnt__when {
  font-variant-numeric: tabular-nums;
}
.rnt__action {
  opacity: 0;
  transition: opacity 0.15s ease;
}
.rnt__card:hover .rnt__action,
.rnt__action:focus-visible {
  opacity: 1;
}
.rnt__body {
  font-size: 13.5px;
  color: var(--ink-900, #16262c);
  word-break: break-word;
}
.rnt__body :deep(p) {
  margin: 0 0 6px;
}
.rnt__body :deep(p:last-child) {
  margin-bottom: 0;
}
.rnt__body :deep(img) {
  max-width: 100%;
  height: auto;
}
.rnt__body :deep(.tagged-user),
.rnt__editor :deep(.tagged-user) {
  color: var(--q-primary);
  font-weight: 600;
  background: rgba(27, 117, 171, 0.1);
  padding: 1px 4px;
  border-radius: 4px;
}
.rnt__composer {
  flex: 0 0 auto;
  border-top: 1px solid var(--line, #dde6e8);
  background: #fff;
}
.rnt__compose-box {
  position: relative;
  border: 1px solid var(--line, #dde6e8);
  border-radius: 12px;
  padding: 6px 8px 8px;
  transition: border-color 0.15s ease, box-shadow 0.15s ease;
}
.rnt__compose-box--closed {
  border: none;
  padding: 0;
}
.rnt__compose-box:focus-within {
  border-color: var(--q-primary);
  box-shadow: 0 0 0 3px rgba(27, 117, 171, 0.12);
}
/* The composer closed: a single line, so the thread has the room. */
.rnt__compose-box--closed:focus-within {
  box-shadow: none;
}
.rnt__composer--closed {
  padding-top: 8px;
  padding-bottom: 8px;
}
.rnt__open-btn {
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
.rnt__open-btn:hover,
.rnt__open-btn:focus-visible {
  border-color: var(--q-primary);
  outline: none;
}
.rnt__open-btn--draft {
  color: var(--ink-900, #16262c);
}
.rnt__editor {
  border: none;
}
.rnt__editor :deep(.q-editor__toolbar) {
  border-bottom: 1px solid rgba(0, 0, 0, 0.08);
}
.rnt__editor :deep(.q-editor__content) {
  font-size: 13.5px;
}
.rnt__compose-bar {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 6px;
}
.rnt__types {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 4px;
}
.rnt__types-label {
  margin-right: 4px;
  color: var(--ink-500, #647a82);
  font-size: 12px;
  font-weight: 600;
}
/* Between the views (All notes, Work & progress) and the note types. */
.rnt__sep {
  flex: 0 0 1px;
  align-self: stretch;
  min-height: 20px;
  margin: 2px 4px;
  background: var(--line, #dde6e8);
}
.rnt__type-chip {
  min-height: 26px;
  padding: 0 10px;
  border: 1px solid var(--line, #dde6e8);
  border-radius: 999px;
  background: #fff;
  color: var(--ink-700, #3a4d54);
  font: inherit;
  font-size: 11.5px;
  cursor: pointer;
}
.rnt__type-chip--on {
  background: #123645;
  border-color: #123645;
  color: #fff;
}
.rnt__type-select {
  min-width: 200px;
}
</style>

<style>
/* While the notes cover the window the page behind them does not scroll. */
body.rnt-fullscreen-open {
  overflow: hidden;
}
/* The suggestion list is rendered by a small inline component, so its elements carry no scoped
   attribute; these rules are global on purpose and namespaced by the rnt__ prefix. */
.rnt__mentions {
  position: absolute;
  left: 8px;
  bottom: calc(100% - 4px);
  z-index: 10;
  min-width: 240px;
  max-height: 220px;
  overflow: auto;
  padding: 4px;
  border: 1px solid var(--line, #dde6e8);
  border-radius: 10px;
  background: #fff;
  box-shadow: 0 12px 32px rgba(18, 54, 69, 0.16);
}
.rnt__card .rnt__mentions {
  position: relative;
  bottom: auto;
  left: auto;
  margin-top: 4px;
  box-shadow: none;
}
.rnt__mention {
  display: block;
  width: 100%;
  padding: 6px 10px;
  border: none;
  border-radius: 6px;
  background: transparent;
  text-align: left;
  font: inherit;
  font-size: 13px;
  cursor: pointer;
}
.rnt__mention:hover,
.rnt__mention--active {
  background: var(--brand-050, #f3f8fb);
  color: var(--q-primary);
}
</style>
