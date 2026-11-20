import { Notify } from "quasar";

// Standard toast templates (VSky UI standard, feedback §1): bottom, multi-line, the standard icon per type.
// Same visual output as assets/utils.js notify* helpers, exposed as a composable so screens built on the
// App* layer read the same way as the rest of the standard.
const base = {
  multiLine: true,
  html: true,
  position: "bottom"
};

function create (type, icon, message, opts = {}) {
  return Notify.create({
    ...base,
    type,
    icon,
    message: typeof message === "string" ? message : message?.message,
    caption: opts.caption ?? message?.caption,
    timeout: opts.timeout ?? message?.timeout,
    actions: opts.actions ?? message?.actions,
    ...(typeof opts === "object" ? opts : {})
  });
}

export function useNotify () {
  const notifyPositive = (message, opts) => create("positive", "o_check_circle", message, opts);
  const notifyNegative = (message, opts) => create("negative", "o_error", message, opts);
  const notifyWarning = (message, opts) => create("warning", "o_warning", message, opts);
  const notifyInfo = (message, opts) => create("info", "o_info", message, opts);

  return {
    notifyPositive,
    notifyNegative,
    notifyWarning,
    notifyInfo,
    notifySuccess: notifyPositive,
    notifyError: notifyNegative,
    success: notifyPositive,
    error: notifyNegative,
    warning: notifyWarning,
    info: notifyInfo
  };
}
