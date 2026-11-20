<template>
  <q-select
    ref="selectRef"
    :model-value="null"
    :options="options"
    option-value="id"
    option-label="title"
    :option-disable="isCurrent"
    use-input
    hide-selected
    hide-dropdown-icon
    outlined
    dense
    input-debounce="300"
    behavior="menu"
    menu-anchor="bottom right"
    menu-self="top right"
    :menu-offset="[0, 4]"
    popup-content-class="rqs__popup"
    placeholder="Find a requirement"
    aria-label="Find a requirement"
    class="rqs"
    @filter="onFilter"
    @filter-abort="cancelPending"
    @popup-show="hasPopup = true"
    @popup-hide="hasPopup = false"
    @update:model-value="open"
  >
    <template #prepend>
      <q-icon name="o_search" size="18px" />
    </template>

    <template #option="scope">
      <q-item v-bind="scope.itemProps" class="rqs__item">
        <q-item-section>
          <q-item-label class="rqs__title">
            <span class="rqs__number">#{{ scope.opt.requirementNumber }}</span> {{ scope.opt.title }}
          </q-item-label>
          <q-item-label caption class="rqs__place">
            {{ scope.opt.projectName || "—" }}<template v-if="scope.opt.projectModuleName"> › {{ scope.opt.projectModuleName }}</template>
          </q-item-label>
        </q-item-section>
        <q-item-section side>
          <span v-if="isCurrent(scope.opt)" class="rqs__here">Open now</span>
          <app-option-badge v-else-if="scope.opt.statusLabel" :option="statusBadge(scope.opt)" />
        </q-item-section>
      </q-item>
    </template>

    <template #after-options>
      <div v-if="more" class="rqs__more">More requirements match. Keep typing to narrow them down.</div>
    </template>

    <template #no-option>
      <div class="rqs__none">No requirement matches “{{ term }}”.</div>
    </template>

    <q-tooltip v-if="!hasPopup" :delay="600" max-width="260px">Type a requirement number, or a few words of its title, project or module</q-tooltip>
  </q-select>
</template>

<script setup>
// Jump to another requirement from the one that is open: type its number or a few words, pick a
// suggestion, and its page opens. The suggestions come from the v2 quick search (number, title, project
// or module; best match first; only what the reader may see).
import { ref } from "vue";
import { useRouter } from "vue-router";
import AppOptionBadge from "components/common/AppOptionBadge.vue";
import requirementService from "modules/requirement-v2/requirementV2.service";

const props = defineProps({
  // The requirement whose page this is: it can come up in the suggestions, marked, and is not a link.
  currentId: { type: String, default: "" }
});

const SHOWN = 8;

const router = useRouter();
const selectRef = ref(null);
const options = ref([]);
const term = ref("");
const more = ref(false);
const hasPopup = ref(false);

const isCurrent = (opt) => !!props.currentId && opt?.id === props.currentId;

const statusBadge = (opt) => ({
  label: opt.statusLabel,
  backgroundColor: opt.statusBgColor || "",
  textColor: opt.statusColor || ""
});

// One character is enough when it is a digit (requirement #7); words need two.
const longEnough = (text) => text.length >= 2 || /^#?\d$/.test(text);

// An API that has not been restarted since the quick search was added answers 404: the suggestions are
// then read from the list (title, or the exact number), so the box works either way.
let quickSearchUnsupported = false;

const fromList = async (text, signal) => {
  const digits = text.replace(/^#/, "");
  const byNumber = /^\d{1,9}$/.test(digits);
  const page = await requirementService.getAllRequirement({
    ...(byNumber ? { requirementNumber: Number(digits) } : { name: text }),
    page: 1,
    pageSize: SHOWN + 1,
    sortBy: "updatedOnUtc",
    descending: true
  }, { signal, silent: true });
  return (page?.data || []).map((row) => ({
    id: row.id,
    requirementNumber: row.requirementNumber,
    title: row.title,
    projectName: row.project?.name,
    projectModuleName: row.projectModule?.name,
    statusLabel: row.status?.dropDownValue
  }));
};

const fetchSuggestions = async (text, signal) => {
  if (!quickSearchUnsupported) {
    try {
      return await requirementService.searchRequirements(text, SHOWN + 1, { signal, silent: true });
    } catch (error) {
      if (error?.response?.status !== 404) throw error;
      quickSearchUnsupported = true;
    }
  }
  return fromList(text, signal);
};

// Only the latest search matters: an earlier one still on its way is dropped.
let pending = null;
const cancelPending = () => {
  pending?.abort();
  pending = null;
};

const onFilter = async (input, update, abort) => {
  cancelPending();
  const text = String(input || "").trim();
  if (!longEnough(text)) {
    options.value = [];
    abort();
    return;
  }

  const controller = new AbortController();
  pending = controller;
  try {
    const rows = await fetchSuggestions(text, controller.signal);
    if (controller.signal.aborted) return;
    update(
      () => {
        term.value = text;
        more.value = rows.length > SHOWN;
        options.value = rows.slice(0, SHOWN);
      },
      // The best match is ready under Enter.
      (select) => {
        if (options.value.length) {
          select.setOptionIndex(-1);
          select.moveOptionSelection(1, true);
        }
      }
    );
  } catch (error) {
    if (controller.signal.aborted) return;
    console.error("Requirement quick search failed", error);
    abort();
  } finally {
    if (pending === controller) pending = null;
  }
};

const open = (opt) => {
  if (!opt?.id || isCurrent(opt)) return;
  options.value = [];
  selectRef.value?.blur();
  router.push({ name: "requirement-v2-detail", params: { id: opt.id } });
};
</script>

<style scoped>
.rqs {
  width: 230px;
  max-width: 100%;
}
/* A search box among buttons: as tall as they are, lighter than a form field. */
.rqs.q-field--dense :deep(.q-field__control) {
  min-height: 36px;
  height: 36px;
  padding: 0 10px;
  border-radius: 8px;
  background: #fff;
}
.rqs.q-field--dense :deep(.q-field__marginal) {
  height: 36px;
  color: var(--ink-500, #647a82);
}
.rqs.q-field--dense :deep(.q-field__native) {
  min-height: 34px;
  padding: 0;
  font-size: 13px;
}
.rqs.q-field--dense :deep(.q-field__input) {
  min-height: 34px;
  padding: 0;
}
</style>

<style>
/* The suggestions are drawn in a menu teleported to <body>, out of a scoped style's reach; the rqs__
   prefix keeps these to this component. */
.rqs__popup {
  width: 440px;
  max-width: 95vw;
  border-radius: 10px;
}
.rqs__item {
  min-height: 48px;
  padding: 6px 12px;
}
.rqs__item + .rqs__item {
  border-top: 1px solid var(--line, #dde6e8);
}
.rqs__title {
  font-size: 13.5px;
  font-weight: 500;
  color: var(--ink-900, #16262c);
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
.rqs__number {
  margin-right: 2px;
  font-weight: 700;
  color: var(--q-primary);
  font-variant-numeric: tabular-nums;
}
.rqs__place {
  color: var(--ink-500, #647a82);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.rqs__here {
  font-size: 11.5px;
  font-weight: 600;
  color: var(--ink-500, #647a82);
  white-space: nowrap;
}
.rqs__more,
.rqs__none {
  padding: 8px 12px;
  font-size: 12px;
  color: var(--ink-500, #647a82);
}
.rqs__more {
  border-top: 1px solid var(--line, #dde6e8);
}
</style>
