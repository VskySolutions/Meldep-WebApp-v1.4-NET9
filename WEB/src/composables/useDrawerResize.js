import { ref, onBeforeUnmount } from "vue";
import { LocalStorage } from "quasar";

// Drag-to-resize width for a right-side drawer (its left edge is the handle), persisted in LocalStorage.
export function useDrawerResize ({ storageKey, getDefault, getMin, getMax }) {
  const clamp = (w) => Math.min(getMax(), Math.max(getMin(), w));

  const stored = Number(LocalStorage.getItem(storageKey));
  const width = ref(clamp(stored > 0 ? stored : getDefault()));

  let startX = 0;
  let startWidth = 0;

  const onMove = (e) => {
    // A right-side drawer grows as the pointer moves left.
    width.value = clamp(startWidth + (startX - e.clientX));
  };

  const stopResize = () => {
    document.removeEventListener("mousemove", onMove);
    document.removeEventListener("mouseup", stopResize);
    document.body.style.userSelect = "";
    LocalStorage.set(storageKey, width.value);
  };

  const startResize = (e) => {
    startX = e.clientX;
    startWidth = width.value;
    document.body.style.userSelect = "none";
    document.addEventListener("mousemove", onMove);
    document.addEventListener("mouseup", stopResize);
  };

  const resetWidth = () => {
    width.value = getDefault();
    LocalStorage.set(storageKey, width.value);
  };

  onBeforeUnmount(stopResize);

  return { width, startResize, resetWidth };
}

export const viewportWidth = () => (typeof window !== "undefined" ? window.innerWidth : 1200);
