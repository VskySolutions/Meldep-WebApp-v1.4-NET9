<template>
  <div class="rdoc q-pa-md">
    <app-section-card title="Document references" icon="o_link" :count="documents.length || null" info="Links to the documents that describe the requirement — a specification, a mock-up, a signed form — each with a name and a note." flush>
      <template #actions>
        <q-btn v-if="editable" flat dense no-caps color="primary" icon="o_edit" label="Manage references" @click="$emit('manage')" />
      </template>

      <q-list v-if="documents.length" separator>
        <q-item v-for="doc in documents" :key="doc.id" class="rdoc__row">
          <q-item-section avatar>
            <q-avatar size="36px" rounded color="blue-1" text-color="primary">
              <q-icon :name="iconFor(doc)" size="20px" />
            </q-avatar>
          </q-item-section>
          <q-item-section>
            <q-item-label class="rdoc__name">
              <a v-if="isUrl(doc.filePath)" :href="doc.filePath" target="_blank" rel="noopener" class="rdoc__link">{{ doc.fileName || fileNameFromPath(doc.filePath) || doc.filePath }}</a>
              <span v-else>{{ doc.fileName || "Untitled reference" }}</span>
            </q-item-label>
            <q-item-label v-if="doc.note" caption class="rdoc__note">{{ doc.note }}</q-item-label>
            <q-item-label caption class="rdoc__path ellipsis">{{ doc.filePath }}</q-item-label>
          </q-item-section>
          <q-item-section side>
            <div class="row no-wrap">
              <q-btn v-if="isUrl(doc.filePath)" flat round dense color="primary" icon="o_open_in_new" type="a" :href="doc.filePath" target="_blank" rel="noopener">
                <q-tooltip>Open</q-tooltip>
              </q-btn>
              <q-btn flat round dense color="grey-7" icon="o_content_copy" @click="copyPath(doc)">
                <q-tooltip>Copy path</q-tooltip>
              </q-btn>
            </div>
          </q-item-section>
        </q-item>
      </q-list>

      <app-empty-state v-else icon="o_link" title="No document references" :text="editable ? 'Link the specification, mock-ups or signed forms that describe this requirement.' : 'Nothing has been linked yet.'">
        <q-btn v-if="editable" unelevated no-caps color="primary" icon="o_add" label="Add references" @click="$emit('manage')" />
      </app-empty-state>
    </app-section-card>
  </div>
</template>

<script setup>
// The documents linked to a requirement, as a list the reader can open and copy from; editing happens
// in the Edit panel's Documents tab.
import AppSectionCard from "components/common/AppSectionCard.vue";
import AppEmptyState from "components/common/AppEmptyState.vue";
import { useNotify } from "composables/useNotify";

defineProps({
  // model.filePathDetails
  documents: { type: Array, default: () => [] },
  editable: { type: Boolean, default: false }
});

defineEmits(["manage"]);

const notify = useNotify();

const isUrl = (value) => /^https?:\/\//i.test(String(value || "").trim());

// Stored links point at the file service with the original name in a `file` query parameter.
const fileNameFromPath = (filePath) => {
  if (!filePath) return "";
  try {
    const name = new URL(filePath).searchParams.get("file");
    return name ? decodeURIComponent(name) : "";
  } catch {
    return "";
  }
};

const iconFor = (doc) => {
  const name = `${doc.fileName || ""} ${fileNameFromPath(doc.filePath)} ${doc.filePath || ""}`.toLowerCase();
  if (/\.pdf\b/.test(name)) return "o_picture_as_pdf";
  if (/\.(png|jpe?g|gif|svg|webp)\b/.test(name)) return "o_image";
  if (/\.(xlsx?|csv)\b/.test(name)) return "o_table_chart";
  if (/\.(docx?|rtf)\b/.test(name)) return "o_description";
  if (/\.(pptx?)\b/.test(name)) return "o_slideshow";
  if (/\.(zip|rar|7z)\b/.test(name)) return "o_folder_zip";
  return isUrl(doc.filePath) ? "o_link" : "o_insert_drive_file";
};

const copyPath = async (doc) => {
  try {
    await navigator.clipboard.writeText(doc.filePath || "");
    notify.success("Path copied.");
  } catch {
    notify.warning("Copy failed — please select and copy the path manually.");
  }
};
</script>

<style scoped>
.rdoc__row {
  padding-top: 10px;
  padding-bottom: 10px;
}
.rdoc__name {
  font-weight: 500;
}
.rdoc__link {
  color: var(--q-primary);
  text-decoration: none;
}
.rdoc__link:hover {
  text-decoration: underline;
}
.rdoc__note {
  white-space: pre-wrap;
}
.rdoc__path {
  font-size: 11px;
  max-width: 100%;
}
</style>
