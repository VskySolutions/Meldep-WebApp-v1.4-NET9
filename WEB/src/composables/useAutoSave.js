import { ref, computed, onBeforeUnmount } from "vue";

// Auto-save for a form that edits something which already exists: the page calls mark() whenever the
// model changes, the save runs once typing has stopped, overlapping changes queue one follow-up save,
// and the browser warns before a tab closes with changes still unsaved.
//
//   const autosave = useAutoSave({
//     debounceMs: 1200,
//     save: async () => {
//       if (!(await validate())) return "Fix the highlighted fields to save.";   // a string = blocked
//       await api.update(id, payload());                                        // throws = error
//     }
//   });
//   watch(model, () => autosave.mark(), { deep: true });
//   onBeforeRouteLeave(async () => (await autosave.flush()) === "saved" || confirm(...));
//
// States: idle (nothing to save) · pending (a save is due) · saving · saved · blocked (the page refused,
// e.g. validation) · error (the request failed). `message` carries the reason for blocked and error.

// The API answers a failed write with the exception text, stack trace included. One sentence of it is
// what an indicator can show: the database's own reason when there is one, else the innermost message.
export function shortApiError (error, fallback = "Could not save.") {
  const data = error?.response?.data;
  const raw = (typeof data === "string" ? data : data?.message) || error?.message || "";
  if (!raw) return fallback;
  const truncated = /String or binary data would be truncated[^']*'[^']*',\s*column '([^']+)'/i.exec(raw);
  if (truncated) return `The ${truncated[1].replace(/([a-z])([A-Z])/g, "$1 $2").toLowerCase()} is too long to save.`;
  const innermost = raw.split("--->").pop();
  const firstLine = innermost.split(/\r?\n/).map((l) => l.trim()).find((l) => l && !l.startsWith("at ")) || "";
  const sentence = firstLine.replace(/^[A-Za-z.]+Exception(\s*\([^)]*\))?:\s*/, "").trim();
  return sentence ? (sentence.length > 160 ? `${sentence.slice(0, 157)}…` : sentence) : fallback;
}

export function useAutoSave ({ save, debounceMs = 1200 } = {}) {
  const state = ref("idle");
  const message = ref("");
  const savedAt = ref(null);

  let timer = null;
  let inFlight = null;
  let rerun = false;

  const clearTimer = () => {
    if (timer) {
      clearTimeout(timer);
      timer = null;
    }
  };

  const run = () => {
    clearTimer();
    if (inFlight) {
      // A change landed while a save was running: save again once it is done.
      rerun = true;
      return inFlight;
    }
    state.value = "saving";
    message.value = "";
    inFlight = (async () => {
      try {
        const result = await save();
        if (typeof result === "string") {
          state.value = "blocked";
          message.value = result;
        } else {
          state.value = "saved";
          savedAt.value = new Date();
        }
      } catch (error) {
        state.value = "error";
        message.value = shortApiError(error);
        console.error("Auto-save failed", error);
      } finally {
        inFlight = null;
        if (rerun) {
          rerun = false;
          run();
        }
      }
    })();
    return inFlight;
  };

  // Something changed: save once the reader pauses.
  const mark = () => {
    state.value = "pending";
    clearTimer();
    timer = setTimeout(run, debounceMs);
  };

  // Save now if anything is due, wait for whatever is running, and report the resulting state.
  const flush = async () => {
    if (timer) await run();
    while (inFlight) await inFlight;
    return state.value;
  };

  const unsaved = computed(() => ["pending", "saving", "blocked", "error"].includes(state.value));

  const onBeforeUnload = (event) => {
    if (!unsaved.value) return;
    event.preventDefault();
    event.returnValue = "";
  };
  window.addEventListener("beforeunload", onBeforeUnload);

  onBeforeUnmount(() => {
    window.removeEventListener("beforeunload", onBeforeUnload);
    clearTimer();
  });

  // Forget what is pending (after the form is reset or reloaded from the server).
  const reset = () => {
    clearTimer();
    rerun = false;
    state.value = "idle";
    message.value = "";
  };

  return { state, message, savedAt, unsaved, mark, flush, run, reset };
}
