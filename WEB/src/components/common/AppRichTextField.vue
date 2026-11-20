<template>
  <div class="app-field">
    <app-field-label :label="label" :required="required" :info="info" />
    <q-editor
      v-if="!readonly && !disable"
      v-model="model"
      :toolbar="editorToolbar"
      :fonts="fonts"
      :dense="$q.screen.lt.md"
      :placeholder="placeholder"
      :min-height="minHeight"
      :max-height="maxHeight"
      :class="['app-rich-text', { 'app-rich-text--error': error }]"
      @blur="$emit('blur', $event)"
    />
    <!-- Read-only renders the stored markup rather than a disabled editor: a greyed-out toolbar over
         content nobody can change is just noise. -->
    <!-- eslint-disable-next-line vue/no-v-html -->
    <div v-else-if="hasContent" class="app-rich-text__readonly RichTextEditor" v-html="modelValue" />
    <div v-else class="app-rich-text__readonly text-grey-6">{{ placeholder || "—" }}</div>
    <div v-if="error && errorMessage" class="text-negative text-caption q-mt-xs">{{ errorMessage }}</div>
    <div v-else-if="hint" class="text-caption text-grey-7 q-mt-xs">{{ hint }}</div>
  </div>
</template>

<script setup>
// Rich-text counterpart to AppTextField, built on q-editor with the app's shared toolbar
// (composables/form-inputs/useEditorSettings). The value is HTML, stored as the editor produces it.
import { computed } from "vue";
import { useQuasar } from "quasar";
import AppFieldLabel from "components/common/AppFieldLabel.vue";
import { getEditorConfig } from "src/composables/form-inputs/useEditorSettings.js";
import { hasRichTextContent } from "src/utils/richText";

const props = defineProps({
  modelValue: { type: String, default: "" },
  label: { type: String, default: "" },
  required: { type: Boolean, default: false },
  info: { type: String, default: "" },
  placeholder: { type: String, default: "" },
  hint: { type: String, default: "" },
  readonly: { type: Boolean, default: false },
  disable: { type: Boolean, default: false },
  error: { type: Boolean, default: false },
  errorMessage: { type: String, default: "" },
  // "full" is the app's complete toolbar; "compact" the short one used inside rows and composers.
  toolbar: { type: [String, Array], default: "full" },
  minHeight: { type: String, default: "8rem" },
  maxHeight: { type: String, default: "22rem" }
});

const emit = defineEmits(["update:modelValue", "blur"]);

const $q = useQuasar();
const { fonts, toolbar: fullToolbar, rowToolbar } = getEditorConfig($q);

const editorToolbar = computed(() => {
  if (Array.isArray(props.toolbar)) return props.toolbar;
  return props.toolbar === "compact" ? rowToolbar : fullToolbar;
});

const model = computed({
  get: () => props.modelValue || "",
  set: (val) => emit("update:modelValue", val)
});

const hasContent = computed(() => hasRichTextContent(props.modelValue));
</script>

<style scoped>
/* Match the outlined fields beside it so the editor does not read as a foreign control. */
.app-rich-text {
  border-color: rgba(0, 0, 0, 0.24);
  border-radius: 4px;
}
.app-rich-text :deep(.q-editor__toolbar) {
  border-bottom: 1px solid rgba(0, 0, 0, 0.12);
  background: #fafafa;
}
.app-rich-text:focus-within {
  border-color: var(--q-primary);
  box-shadow: 0 0 0 1px var(--q-primary);
}
.app-rich-text--error {
  border-color: var(--q-negative);
}
.app-rich-text--error:focus-within {
  border-color: var(--q-negative);
  box-shadow: 0 0 0 1px var(--q-negative);
}
.app-rich-text :deep(.q-editor__content) {
  font-size: 14px;
  color: var(--ink-900, #16262c) !important;
}
.app-rich-text__readonly {
  padding: 6px 0;
  font-size: 14px;
  color: var(--ink-900, #16262c);
  word-break: break-word;
}
.app-rich-text__readonly :deep(p) {
  margin: 0 0 0.5em;
}
.app-rich-text__readonly :deep(p:last-child) {
  margin-bottom: 0;
}
.app-rich-text__readonly :deep(img) {
  max-width: 100%;
  height: auto;
}
</style>
