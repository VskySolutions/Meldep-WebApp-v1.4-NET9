<template>
  <app-section-card title="People" icon="o_groups">
    <div class="rpc">
      <div v-for="person in people" :key="person.role" class="rpc__row">
        <app-avatar :name="person.name" :size="30" :empty="person.empty" />
        <div class="rpc__text">
          <div class="rpc__role">{{ person.role }}</div>
          <div :class="['rpc__name', { 'rpc__name--empty': !person.name }]">{{ person.name || person.empty }}</div>
        </div>
      </div>
    </div>
  </app-section-card>
</template>

<script setup>
// Who is who on the requirement: the owner on each side, who raised it, who entered it, who approved it.
import { computed } from "vue";
import AppSectionCard from "components/common/AppSectionCard.vue";
import AppAvatar from "components/common/AppAvatar.vue";
import { personName } from "modules/requirement-v2/utils/options";

const props = defineProps({
  model: { type: Object, default: null }
});

const people = computed(() => {
  const m = props.model || {};
  const identifier = m.userType?.dropDownValue;
  const identified = identifier === "Customer" ? personName(m.customer) : personName(m.employee);
  const list = [
    { role: "Requirement owner", name: personName(m.requirementOwner), empty: "No owner yet" },
    { role: "Customer owner", name: personName(m.customerOwner), empty: "No customer owner" },
    { role: identifier ? `Identified by (${identifier.toLowerCase()})` : "Identified by", name: identified, empty: "Not recorded" },
    { role: "Entered by", name: personName(m.requirementEntered), empty: "Not recorded" }
  ];
  if (personName(m.approvedBy)) list.push({ role: "Approved by", name: personName(m.approvedBy), empty: "" });
  if (personName(m.confirmedBy)) list.push({ role: "Confirmed by", name: personName(m.confirmedBy), empty: "" });
  return list;
});
</script>

<style scoped>
.rpc {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.rpc__row {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 0;
}
.rpc__text {
  min-width: 0;
}
.rpc__role {
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: var(--ink-500, #647a82);
}
.rpc__name {
  font-size: 13.5px;
  color: var(--ink-900, #16262c);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.rpc__name--empty {
  color: var(--ink-300, #a6b8bd);
  font-style: italic;
}
</style>
