<template>
  <!-- Status tiles: one per configured status, each a toggle on the status filter. -->
  <div class="rss">
    <div class="rss__tiles">
      <app-stat-tile
        label="All"
        :value="summary?.total ?? 0"
        icon="o_assignment"
        clickable
        dense
        :active="!filters.statusIds?.length && !anyFlag"
        :loading="loading && !summary"
        hint="Every requirement matching the search and filters"
        @click="$emit('clear-quick')"
      />
      <app-stat-tile
        v-for="tile in statusTiles"
        :key="tile.id || 'none'"
        :label="tile.label"
        :value="tile.count"
        :color="tile.color"
        :icon="tile.icon"
        clickable
        dense
        :active="isStatusActive(tile.id)"
        :loading="loading && !summary"
        :hint="`Show only ${tile.label.toLowerCase()} requirements`"
        @click="$emit('toggle-status', tile.id)"
      />
    </div>
  </div>

  <!-- Attention flags, in a row of their own after the tiles: counted toggles over the things that need a look. -->
  <div class="rss-flags" role="group" aria-label="Attention flags">
    <button
      v-for="flag in flags"
      :key="flag.key"
      type="button"
      :class="['rss__flag', `rss__flag--${flag.tone}`, { 'rss__flag--on': flag.active }]"
      :aria-pressed="String(flag.active)"
      @click="$emit('toggle-flag', flag.key)"
    >
      <q-icon :name="flag.icon" size="15px" />
      <span>{{ flag.label }}</span>
      <span class="rss__flag-count">{{ loading && !summary ? "…" : flag.count }}</span>
      <q-tooltip :delay="400" max-width="280px">{{ flag.hint }}</q-tooltip>
    </button>
  </div>
</template>

<script setup>
// The "at a glance" strip over the requirement list: a tile per status (.rss) and, after it, a chip per
// attention flag (.rss-flags), each showing how many requirements it would narrow the list to, and each
// a toggle on the matching filter. Counts are faceted (a tile's own filter is ignored in its count), so
// a tile never shows 0 just because it is the one that was clicked. The list puts both in the folding
// part of its header card.
import { computed } from "vue";
import AppStatTile from "components/common/AppStatTile.vue";

const props = defineProps({
  // The /requirement-v2/summary payload, or null before the first load.
  summary: { type: Object, default: null },
  loading: { type: Boolean, default: false },
  // The list's search model (statusIds and the flag toggles are read from it).
  filters: { type: Object, required: true },
  // Configured statuses [{ text, value, color, bgColor, data: sortOrder }] so a status with no
  // requirements still gets a tile.
  statuses: { type: Array, default: () => [] }
});

defineEmits(["toggle-status", "toggle-flag", "clear-quick"]);

// A word in the status name picks an icon; the colour is the site's own badge colour.
const iconFor = (label) => {
  const l = String(label || "").toLowerCase();
  if (l.includes("new")) return "o_fiber_new";
  if (l.includes("progress") || l.includes("open")) return "o_play_circle";
  if (l.includes("hold")) return "o_pause_circle";
  if (l.includes("close") || l.includes("done") || l.includes("complete")) return "o_check_circle";
  if (l.includes("cancel")) return "o_cancel";
  return "o_label";
};

const statusTiles = computed(() => {
  const counts = new Map((props.summary?.byStatus || []).map((s) => [s.id || null, s]));
  const tiles = props.statuses.map((s) => {
    const hit = counts.get(s.value);
    return {
      id: s.value,
      label: s.text,
      count: hit?.count ?? 0,
      color: s.bgColor || hit?.bgColor || "",
      sortOrder: Number(s.data ?? hit?.sortOrder ?? 0),
      icon: iconFor(s.text)
    };
  });
  // Requirements whose status is not in the configured list (retired values, or none at all).
  for (const [id, hit] of counts) {
    if (!tiles.some((t) => t.id === id)) {
      tiles.push({ id, label: hit.label || "No status", count: hit.count, color: hit.bgColor || "#9e9e9e", sortOrder: hit.sortOrder ?? 999, icon: iconFor(hit.label) });
    }
  }
  return tiles.sort((a, b) => a.sortOrder - b.sortOrder || a.label.localeCompare(b.label));
});

const isStatusActive = (id) => {
  const ids = props.filters.statusIds || [];
  if (id == null) return false;
  return ids.length === 1 && ids[0] === id;
};

const anyFlag = computed(() =>
  ["overdue", "dueSoon", "notApproved", "myRequirements", "pinnedOnly", "hasTasks", "updatedFromDate"].some((k) => props.filters[k] != null && props.filters[k] !== ""));

const flags = computed(() => {
  const s = props.summary || {};
  const f = props.filters;
  return [
    { key: "overdue", label: "Overdue", icon: "o_event_busy", tone: "negative", count: s.overdue ?? 0, active: f.overdue === true, hint: "Planned end date has passed and the requirement is not closed" },
    { key: "dueSoon", label: "Due this week", icon: "o_upcoming", tone: "warning", count: s.dueThisWeek ?? 0, active: f.dueSoon === true, hint: "Planned to end within the next seven days" },
    { key: "notApproved", label: "Not approved", icon: "o_pending_actions", tone: "info", count: s.notApproved ?? 0, active: f.notApproved === true, hint: "Approval state missing or anything other than Approved" },
    { key: "myRequirements", label: "Mine", icon: "o_person", tone: "primary", count: s.mine ?? 0, active: f.myRequirements === true, hint: "Owned, identified, entered or created by you" },
    { key: "pinnedOnly", label: "Pinned", icon: "o_push_pin", tone: "primary", count: s.pinned ?? 0, active: f.pinnedOnly === true, hint: "Requirements you pinned to the top of the list" },
    { key: "hasTasks", label: "Not converted", icon: "o_swap_horiz", tone: "neutral", count: s.withoutTasks ?? 0, active: f.hasTasks === false, hint: "No task has been created from the requirement yet" },
    { key: "updatedFromDate", label: "Updated 7d", icon: "o_update", tone: "neutral", count: s.updatedLast7Days ?? 0, active: !!f.updatedFromDate, hint: "Changed in the last seven days" }
  ];
});
</script>

<style scoped>
.rss__tiles {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.rss__tiles > * {
  flex: 1 1 140px;
  max-width: 220px;
}
.rss-flags {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 8px;
}
.rss__flag {
  --flag-colour: var(--ink-700, #3a4d54);
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 30px;
  padding: 0 10px 0 9px;
  border: 1px solid var(--line, #dde6e8);
  border-radius: 999px;
  background: #fff;
  color: var(--ink-700, #3a4d54);
  font: inherit;
  font-size: 12.5px;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.15s ease, border-color 0.15s ease, color 0.15s ease;
}
.rss__flag .q-icon {
  color: var(--flag-colour);
}
.rss__flag--negative { --flag-colour: #c10015; }
.rss__flag--warning { --flag-colour: #b9862f; }
.rss__flag--info { --flag-colour: #2b7f96; }
.rss__flag--primary { --flag-colour: var(--q-primary); }
.rss__flag:hover {
  border-color: var(--flag-colour);
  background: color-mix(in srgb, var(--flag-colour) 6%, #fff);
}
.rss__flag:focus-visible {
  outline: 2px solid var(--q-primary);
  outline-offset: 2px;
}
.rss__flag--on {
  border-color: var(--flag-colour);
  background: var(--flag-colour);
  color: #fff;
}
.rss__flag--on .q-icon {
  color: #fff;
}
.rss__flag-count {
  min-width: 20px;
  padding: 0 6px;
  border-radius: 999px;
  background: rgba(0, 0, 0, 0.06);
  font-size: 11px;
  font-weight: 600;
  text-align: center;
  font-variant-numeric: tabular-nums;
}
.rss__flag--on .rss__flag-count {
  background: rgba(255, 255, 255, 0.25);
}
</style>
