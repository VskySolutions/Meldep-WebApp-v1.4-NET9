import { ref } from "vue";

// "@" mentions inside a Quasar q-editor: typing "@" followed by letters offers matching people; picking
// one inserts a tagged span (which the API reads back as taggedPersonId); Backspace first shortens a
// mention to its first name, then removes it. Everything is scoped to the editor element handed in,
// so several editors on one screen never see each other's caret.
//
//   const mentions = useMentions(() => editorRef.value?.getContentEl?.(), people);
//   <q-editor ref="editorRef" @keyup="mentions.onKeyUp" @keydown="mentions.onKeyDown" />
//   <q-list v-if="mentions.open.value"> … @click="mentions.pick(person)" </q-list>
//   mentions.extractIds()  → ["employee-id", …]
//
// `people` is a ref of [{ text: "Full Name", value: "employee-id" }].
export function useMentions (getEditorEl, people) {
  const open = ref(false);
  const matches = ref([]);
  const activeIndex = ref(0);
  let mentionRange = null;

  const insideEditor = (node) => {
    const el = getEditorEl?.();
    return !!el && !!node && el.contains(node);
  };

  const close = () => {
    open.value = false;
    matches.value = [];
    activeIndex.value = 0;
    mentionRange = null;
  };

  const onKeyUp = (event) => {
    if (event && ["ArrowUp", "ArrowDown", "Enter", "Escape"].includes(event.key) && open.value) return;
    const sel = window.getSelection();
    if (!sel || sel.rangeCount === 0) return close();

    const range = sel.getRangeAt(0);
    const node = range.startContainer;
    if (!node || node.nodeType !== Node.TEXT_NODE || !insideEditor(node)) return close();

    const textBeforeCursor = node.textContent.slice(0, range.startOffset);
    const match = textBeforeCursor.match(/@([\w\s]*)$/);
    if (!match) return close();

    mentionRange = range.cloneRange();
    mentionRange.setStart(node, textBeforeCursor.lastIndexOf("@"));

    const query = match[1].toLowerCase().trim();
    matches.value = (people.value || []).filter((u) => u.text.toLowerCase().includes(query)).slice(0, 8);
    activeIndex.value = 0;
    open.value = matches.value.length > 0;
  };

  const pick = (user) => {
    if (!mentionRange) return close();

    const sel = window.getSelection();
    sel.removeAllRanges();
    sel.addRange(mentionRange);
    mentionRange.deleteContents();

    const parts = user.text.split(" ");
    const displayName = `@${parts[0]}${parts.length > 1 ? ` ${parts[parts.length - 1]}` : ""}`;

    const span = document.createElement("span");
    span.className = "tagged-user";
    span.textContent = displayName;
    span.setAttribute("data-id", user.value);
    span.setAttribute("contenteditable", "false");

    const space = document.createTextNode(" ");
    mentionRange.insertNode(space);
    mentionRange.insertNode(span);
    mentionRange.setStartAfter(space);
    mentionRange.collapse(true);
    sel.removeAllRanges();
    sel.addRange(mentionRange);

    close();
  };

  const onKeyDown = (event) => {
    if (open.value) {
      if (event.key === "ArrowDown") {
        event.preventDefault();
        activeIndex.value = (activeIndex.value + 1) % matches.value.length;
        return;
      }
      if (event.key === "ArrowUp") {
        event.preventDefault();
        activeIndex.value = (activeIndex.value - 1 + matches.value.length) % matches.value.length;
        return;
      }
      if (event.key === "Enter" || event.key === "Tab") {
        event.preventDefault();
        pick(matches.value[activeIndex.value]);
        return;
      }
      if (event.key === "Escape") {
        event.preventDefault();
        close();
        return;
      }
    }

    if (event.key !== "Backspace") return;

    const sel = window.getSelection();
    if (!sel || !sel.rangeCount) return;
    const range = sel.getRangeAt(0);
    const node = range.startContainer;
    if (!insideEditor(node)) return;
    const offset = range.startOffset;

    let prevNode = null;
    if (node.nodeType === Node.TEXT_NODE) {
      if (offset !== 0) return;
      prevNode = node.previousSibling;
    } else if (node.nodeType === Node.ELEMENT_NODE) {
      prevNode = node.childNodes[offset - 1];
    }

    // Skip the spacer and empty text the mention was followed by.
    while (prevNode && (
      (prevNode.nodeType === Node.TEXT_NODE && (prevNode.textContent.trim() === "" || prevNode.textContent === " ")) ||
      prevNode.nodeName === "BR")) {
      prevNode = prevNode.previousSibling;
    }

    if (!prevNode || !prevNode.classList?.contains("tagged-user")) return;
    event.preventDefault();

    const text = prevNode.textContent.replace("@", "").trim();
    const parts = text.split(" ");

    // First Backspace shortens "@First Last" to "@First"; the second removes the mention.
    if (!prevNode.dataset.short && parts.length > 1) {
      prevNode.textContent = `@${parts[0]}`;
      prevNode.dataset.short = "true";
      return;
    }

    const parent = prevNode.parentNode;
    if (prevNode.nextSibling && prevNode.nextSibling.nodeType === Node.TEXT_NODE && prevNode.nextSibling.textContent === " ") {
      parent.removeChild(prevNode.nextSibling);
    }
    const newRange = document.createRange();
    newRange.setStartBefore(prevNode);
    parent.removeChild(prevNode);
    newRange.collapse(true);
    sel.removeAllRanges();
    sel.addRange(newRange);
  };

  // The ids of everyone mentioned in the editor's current content.
  const extractIds = () => {
    const el = getEditorEl?.();
    if (!el) return [];
    return Array.from(el.querySelectorAll(".tagged-user")).map((span) => span.getAttribute("data-id")).filter(Boolean);
  };

  return { open, matches, activeIndex, onKeyUp, onKeyDown, pick, close, extractIds };
}
