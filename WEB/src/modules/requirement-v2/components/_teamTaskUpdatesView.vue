<template>
  <div class="ttu">
    <div v-if="loading && !taskGroups.length" class="row flex-center q-pa-xl">
      <q-spinner color="primary" size="32px" />
    </div>

    <app-empty-state
      v-else-if="!taskGroups.length"
      icon="o_task_alt"
      title="No work and progress updates yet"
      text="Notes written on this requirement's tasks and activities will show here."
    />

    <q-card v-for="task in taskGroups" :key="task.taskId" flat bordered class="ttu__task q-mb-md">
      <q-card-section class="row items-center no-wrap q-py-sm">
        <q-icon name="o_task" color="primary" size="18px" class="q-mr-sm" />
        <div class="text-subtitle2 text-primary col ellipsis">{{ task.taskName }}</div>
        <q-badge color="blue-1" text-color="primary">{{ task.taskNotes.length + task.activityNoteCount }} updates</q-badge>
      </q-card-section>
      <q-separator />

      <q-card-section v-if="task.taskNotes.length" class="q-pt-sm">
        <div class="section-subhead q-mt-none">Task updates</div>
        <requirement-log-list :entries="task.taskNotes" />
      </q-card-section>

      <q-card-section v-if="task.activities.length" class="q-pt-sm">
        <div class="section-subhead q-mt-none">Activity updates</div>
        <div v-for="activity in task.activities" :key="activity.activityId" class="q-mb-md">
          <div class="text-caption text-weight-medium text-grey-8 q-mb-xs">{{ activity.activityName }}</div>
          <requirement-log-list :entries="activity.notes" />
        </div>
      </q-card-section>
    </q-card>
  </div>
</template>

<script setup>
// Work and progress updates: every note written on the requirement's tasks and their activities, grouped
// by task. Used by the notes panel here and by the shared Add Note dialog.
import { ref, computed, watch } from "vue";
import AppEmptyState from "components/common/AppEmptyState.vue";
import RequirementLogList from "modules/requirement-v2/components/RequirementLogList.vue";
import { personName } from "modules/requirement-v2/utils/options";
import requirementService from "modules/requirement-v2/requirementV2.service";

const props = defineProps({
  id: { type: String, default: "" }
});

const loading = ref(false);
const notes = ref([]);

const toEntry = (note) => ({
  id: note.id,
  html: note.note,
  who: personName(note.user) || "",
  when: note.createdOnUtc || ""
});

const taskGroups = computed(() => {
  const tasks = new Map();

  for (const note of notes.value) {
    const taskId = note.taskId || note.recordId;
    if (!tasks.has(taskId)) {
      tasks.set(taskId, {
        taskId,
        taskName: note.taskName || note.recordName,
        taskNotes: [],
        activities: new Map(),
        activityNoteCount: 0
      });
    }
    const task = tasks.get(taskId);

    if (note.noteType === "Task") {
      task.taskNotes.push(toEntry(note));
    } else if (note.noteType === "Activity") {
      const activityId = note.activityId || note.recordId;
      if (!task.activities.has(activityId)) {
        task.activities.set(activityId, { activityId, activityName: note.activityName || note.recordName, notes: [] });
      }
      task.activities.get(activityId).notes.push(toEntry(note));
      task.activityNoteCount += 1;
    }
  }

  return [...tasks.values()].map((task) => ({ ...task, activities: [...task.activities.values()] }));
});

const load = async () => {
  if (!props.id) return;
  loading.value = true;
  try {
    notes.value = (await requirementService.getRequirementWorkProgressNotes(props.id)) || [];
  } catch (error) {
    console.error("Error loading work and progress updates", error);
  } finally {
    loading.value = false;
  }
};

watch(() => props.id, () => load(), { immediate: true });
</script>

<style scoped>
.ttu__task {
  border-radius: 10px;
  box-shadow: none;
}
</style>
