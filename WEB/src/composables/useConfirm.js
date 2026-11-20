import { Dialog } from "quasar";

// Standard confirmation dialog: a short noun-phrase title, a message that names the record and the
// consequence, and a VERB on the confirm button ("Delete", "Remove", "Convert"), never "OK/Yes".
// `type: "danger"` colours the confirm button negative for anything that removes or disables.
export function useConfirm () {
  const confirm = ({
    title = "Please confirm",
    message = "Are you sure you want to continue?",
    confirmLabel = "Confirm",
    cancelLabel = "Cancel",
    type = "primary",
    html = false
  } = {}) =>
    new Promise((resolve) => {
      let settled = false;
      const done = (value) => {
        if (!settled) {
          settled = true;
          resolve(value);
        }
      };
      Dialog.create({
        title,
        message,
        html,
        ok: {
          label: confirmLabel,
          unelevated: true,
          noCaps: true,
          color: type === "danger" ? "negative" : "primary"
        },
        cancel: { label: cancelLabel, flat: true, noCaps: true, color: "grey-8" },
        persistent: true
      })
        .onOk(() => done(true))
        .onCancel(() => done(false))
        .onDismiss(() => done(false));
    });

  return { confirm };
}
