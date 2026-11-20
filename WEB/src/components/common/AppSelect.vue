<template>
  <div class="app-field">
    <app-field-label :label="label" :required="required" :info="info" />
    <q-select
      v-model="model"
      :options="visibleOptions"
      :loading="loading"
      :clearable="clearable"
      :multiple="multiple"
      :option-value="optionValue"
      :option-label="optionLabel"
      :option-disable="optionDisable"
      :emit-value="emitValue"
      :map-options="mapOptions"
      :dense="dense"
      :use-chips="chips"
      :readonly="readonly"
      :disable="disable"
      :autocomplete="autocomplete"
      :aria-label="ariaLabel"
      :use-input="searchable"
      :input-debounce="inputDebounce"
      :fill-input="typeahead"
      :hide-selected="typeahead"
      :rules="rules"
      :hint="hint"
      :placeholder="placeholder"
      outlined
      hide-bottom-space
      :error="error"
      :error-message="errorMessage"
      class="app-select"
      @input-value="search = $event || ''"
      @popup-show="search = ''"
      @popup-hide="search = ''"
      @blur="$emit('blur', $event)"
    >
      <!-- Multi-select renders each selection as the brand-tint chip, removable while editable. -->
      <template v-if="chips" #selected-item="scope">
        <q-chip
          :removable="!readonly && !disable"
          dense
          :tabindex="scope.tabindex"
          color="blue-1"
          text-color="primary"
          class="app-select__chip"
          @remove="scope.removeAtIndex(scope.index)"
        >
          <span class="ellipsis">{{ optionText(scope.opt) }}</span>
        </q-chip>
      </template>

      <!-- "Select all" for a multi-select that asks for it: all the options showing (the search's matches,
           while searching); again to clear them. -->
      <template v-if="multiple && selectAll && selectableOptions.length" #before-options>
        <q-item clickable dense class="app-select__all" @click="toggleAll">
          <q-item-section side>
            <q-checkbox :model-value="allState" dense tabindex="-1" class="app-select__all-box" />
          </q-item-section>
          <q-item-section>
            <q-item-label>{{ search.trim() ? "Select all matching" : "Select all" }} ({{ selectableOptions.length }})</q-item-label>
          </q-item-section>
        </q-item>
        <q-separator />
      </template>

      <!-- Multi-select options carry a checkbox so the state of each one is visible while picking. -->
      <template v-if="multiple || hasHeaders || $slots.option" #option="scope">
        <q-item-label v-if="scope.opt.header" header class="app-select__group">{{ optionText(scope.opt) }}</q-item-label>
        <slot v-else name="option" v-bind="scope">
          <q-item v-bind="scope.itemProps">
            <q-item-section v-if="multiple" side>
              <q-checkbox :model-value="scope.selected" dense @update:model-value="scope.toggleOption(scope.opt)" />
            </q-item-section>
            <q-item-section>
              <q-item-label class="app-select__option">{{ optionText(scope.opt) }}</q-item-label>
            </q-item-section>
          </q-item>
        </slot>
      </template>

      <template v-if="loading" #append>
        <q-spinner size="20px" color="primary" />
      </template>
      <template #no-option>
        <q-item>
          <q-item-section class="text-grey-6">{{ noOptionText }}</q-item-section>
        </q-item>
      </template>
      <template v-if="$slots.after" #after>
        <slot name="after" />
      </template>
    </q-select>
  </div>
</template>

<script setup>
// Standard select: label above, outlined, dense, 40px tall; type-to-search switches on by itself over
// ten options and filters the options the component already holds (hand it the full list).
import { ref, computed, toRef } from "vue";
import AppFieldLabel from "components/common/AppFieldLabel.vue";
import { useFieldLabel } from "composables/useFieldLabel";

const props = defineProps({
  modelValue: { type: [String, Number, Array, Object, Boolean], default: null },
  options: { type: Array, default: () => [] },
  label: { type: String, default: "" },
  required: { type: Boolean, default: false },
  // Why the list offers what it offers (scoped to a project, maintained under Manage Dropdowns).
  info: { type: String, default: "" },
  // Accessible name for a select with no visible label of its own.
  ariaLabel: { type: String, default: "" },
  loading: { type: Boolean, default: false },
  clearable: { type: Boolean, default: true },
  multiple: { type: Boolean, default: false },
  // A "Select all" row at the top of a multi-select's list.
  selectAll: { type: Boolean, default: false },
  optionValue: { type: String, default: "value" },
  // Meldep's dropdown composables produce { text, value }; pass option-label="text" for those lists.
  optionLabel: { type: String, default: "label" },
  optionDisable: { type: [String, Function], default: undefined },
  emitValue: { type: Boolean, default: true },
  mapOptions: { type: Boolean, default: true },
  error: { type: Boolean, default: false },
  errorMessage: { type: String, default: "" },
  rules: { type: Array, default: () => [] },
  hint: { type: String, default: "" },
  placeholder: { type: String, default: undefined },
  dense: { type: Boolean, default: true },
  useChips: { type: Boolean, default: undefined },
  readonly: { type: Boolean, default: false },
  disable: { type: Boolean, default: false },
  autocomplete: { type: String, default: "off" },
  useInput: { type: Boolean, default: undefined },
  // 0 because the lists filtered this way are in memory (Quasar's own default is 500ms).
  inputDebounce: { type: [Number, String], default: 0 },
  noOptionText: { type: String, default: "No options" }
});

const emit = defineEmits(["update:modelValue", "blur"]);

const model = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});

// Multi-select shows selections as chips by default; single-select stays inline text.
const chips = computed(() => (props.useChips === undefined ? props.multiple : props.useChips));

// Past this many options a list is quicker to type than to scroll, so search switches on by itself.
const SEARCH_THRESHOLD = 10;
const searchable = computed(() =>
  (props.useInput === undefined ? props.options.length > SEARCH_THRESHOLD : props.useInput));

// A searchable single-select reads as an autocomplete: the selection sits in the search input itself.
const typeahead = computed(() => searchable.value && !props.multiple);

const search = ref("");
const optionText = (opt) =>
  String((opt !== null && typeof opt === "object" ? opt[props.optionLabel] : opt) ?? "");
const optionKey = (opt) => (opt !== null && typeof opt === "object" ? opt[props.optionValue] : opt);

const selectedKeys = computed(() => {
  const val = props.modelValue;
  if (val === null || val === undefined) return [];
  return (Array.isArray(val) ? val : [val]).map(optionKey);
});

// Whatever is selected stays in the list however the search narrows it.
const isSelectedOption = (opt) => {
  const key = optionKey(opt);
  return key !== undefined && selectedKeys.value.includes(key);
};

const hasHeaders = computed(() => props.options.some((o) => o && o.header === true));

const visibleOptions = computed(() => {
  const needle = searchable.value ? search.value.trim().toLowerCase() : "";
  if (needle === "") return props.options;
  const matches = (opt) => optionText(opt).toLowerCase().includes(needle) || isSelectedOption(opt);
  if (!hasHeaders.value) return props.options.filter(matches);

  // A heading is not itself a match; it stays only while something under it does.
  const visible = [];
  let pending = null;
  for (const opt of props.options) {
    if (opt && opt.header === true) {
      pending = opt;
    } else if (matches(opt)) {
      if (pending) visible.push(pending);
      pending = null;
      visible.push(opt);
    }
  }
  return visible;
});

// ---- Select all ----
const isDisabledOption = (opt) => {
  if (typeof props.optionDisable === "function") return props.optionDisable(opt) === true;
  if (typeof props.optionDisable === "string") return opt !== null && typeof opt === "object" && opt[props.optionDisable] === true;
  return opt !== null && typeof opt === "object" && opt.disable === true;
};
// What "Select all" covers: the options showing, without group headings and disabled ones.
const selectableOptions = computed(() => visibleOptions.value.filter((opt) => !(opt && opt.header === true) && !isDisabledOption(opt)));
// true: all of them selected; null: some (the box shows a dash); false: none.
const allState = computed(() => {
  const selected = selectableOptions.value.filter(isSelectedOption).length;
  if (selected === 0) return false;
  return selected === selectableOptions.value.length ? true : null;
});
const toggleAll = () => {
  const current = Array.isArray(props.modelValue) ? props.modelValue : [];
  const keys = new Set(selectableOptions.value.map(optionKey));
  if (allState.value === true) {
    model.value = current.filter((item) => !keys.has(optionKey(item)));
    return;
  }
  const missing = selectableOptions.value.filter((opt) => !isSelectedOption(opt));
  model.value = [...current, ...missing.map((opt) => (props.emitValue ? optionKey(opt) : opt))];
};

const { text: labelText } = useFieldLabel(toRef(props, "label"), toRef(props, "required"));
const ariaLabel = computed(() => labelText.value || props.ariaLabel);
</script>

<style scoped>
/* 40px controls (matches the dense text fields); !important + higher specificity beats the legacy global
   `.q-field__control { min-height: auto !important }` in app.scss. Chips still grow the control. */
.app-select :deep(.q-field__control) {
  min-height: 40px !important;
}
.app-select__chip {
  margin: 2px 4px 2px 0;
  max-width: 100%;
}
.app-select__option {
  white-space: normal;
  word-break: break-word;
}
.app-select__all {
  font-weight: 600;
  color: var(--q-primary);
}
/* The row is the button; the box only shows the state. */
.app-select__all-box {
  pointer-events: none;
}
.app-select__group {
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--q-primary);
  padding: 10px 16px 2px;
  min-height: 0;
}
</style>
