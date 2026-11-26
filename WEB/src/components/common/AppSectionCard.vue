<template>
  <q-card flat bordered :class="['app-section-card', { 'app-section-card--flush': flush, 'app-section-card--dense': dense, 'app-section-card--collapsible': collapsible }]">
    <q-card-section
      v-if="hasHead"
      :class="['app-section-card__head row items-center no-wrap q-py-sm', { 'app-section-card__head--toggle': collapsible }]"
      v-bind="headAttrs"
      @click="onHeadClick"
      @keydown.enter.self.prevent="toggle"
      @keydown.space.self.prevent="toggle"
    >
      <q-icon v-if="icon" :name="icon" color="primary" size="18px" class="q-mr-xs" />
      <div class="text-subtitle2 text-primary ellipsis">{{ title }}</div>
      <q-badge v-if="count != null" color="blue-1" text-color="primary" class="q-ml-sm">{{ count }}</q-badge>
      <span v-if="info" class="row items-center" @click.stop @keydown.stop>
        <app-info-tip :text="info" />
      </span>
      <q-space />
      <div class="app-section-card__actions row items-center no-wrap" @click.stop @keydown.stop>
        <slot name="actions" />
      </div>
      <q-icon
        v-if="collapsible"
        name="o_expand_more"
        size="20px"
        color="grey-7"
        :class="['app-section-card__chevron q-ml-xs', { 'app-section-card__chevron--open': expanded }]"
      />
    </q-card-section>
    <!-- A collapsed body stays mounted, so the fields in it keep their values and their validation. -->
    <q-slide-transition>
      <div v-show="expanded" :id="bodyId">
        <q-separator v-if="hasHead" />
        <q-card-section :class="['app-section-card__body', { 'q-pa-none': flush }]">
          <slot />
        </q-card-section>
      </div>
    </q-slide-transition>
  </q-card>
</template>

<script setup>
// The compact card every record view is built from: an icon-and-title head (with an optional count badge,
// an info tip and a right-aligned actions slot), a rule, and the body. A `collapsible` card is an
// accordion section: its head opens and closes the body.
import { computed, ref, useSlots } from "vue";
import { uid } from "quasar";
import AppInfoTip from "components/common/AppInfoTip.vue";

const props = defineProps({
  title: { type: String, default: "" },
  icon: { type: String, default: "" },
  info: { type: String, default: "" },
  count: { type: [Number, String], default: null },
  // No body padding (for a table or a list that draws its own edges).
  flush: { type: Boolean, default: false },
  // Tighter head and body (forms made of several small cards).
  dense: { type: Boolean, default: false },
  // The head toggles the body. Needs a title (or actions), or there is nothing to click.
  collapsible: { type: Boolean, default: false },
  // A collapsible card that starts closed.
  defaultCollapsed: { type: Boolean, default: false }
});

const slots = useSlots();
const hasHead = computed(() => Boolean(props.title || slots.actions));

const bodyId = `app-section-card-${uid()}`;
const open = ref(!(props.collapsible && props.defaultCollapsed));
// Without a head there is no way to open it again, so such a card is always open.
const expanded = computed(() => !props.collapsible || !hasHead.value || open.value);

const toggle = () => {
  if (props.collapsible) open.value = !open.value;
};
const onHeadClick = () => toggle();

const headAttrs = computed(() =>
  props.collapsible
    ? { role: "button", tabindex: 0, "aria-expanded": expanded.value ? "true" : "false", "aria-controls": bodyId }
    : {}
);

defineExpose({
  expand: () => { open.value = true; },
  collapse: () => { open.value = false; },
  toggle
});
</script>

<style scoped>
.app-section-card {
  border-radius: 12px;
  box-shadow: none;
}
.app-section-card__head {
  min-height: 46px;
}
.app-section-card__head--toggle {
  cursor: pointer;
  user-select: none;
  border-radius: 12px;
}
.app-section-card__head--toggle:hover {
  background: rgba(0, 0, 0, 0.03);
}
.app-section-card__head--toggle:focus-visible {
  outline: 2px solid var(--q-primary);
  outline-offset: -2px;
}
.app-section-card__chevron {
  transition: transform 0.2s ease;
}
.app-section-card__chevron--open {
  transform: rotate(180deg);
}
.app-section-card__actions {
  gap: 4px;
}
.app-section-card--dense .app-section-card__head {
  min-height: 40px;
  padding-top: 6px;
  padding-bottom: 6px;
}
.app-section-card--dense .app-section-card__body {
  padding: 12px;
}
</style>
