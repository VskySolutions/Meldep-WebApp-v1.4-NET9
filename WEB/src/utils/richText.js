// Helpers for the HTML that q-editor produces. Kept dependency-free and DOM-free so they can run inside a
// row-mapping loop without creating elements per cell (the old per-cell <textarea> trick cost a DOM node
// for every row on every render).

const ENTITIES = { "&nbsp;": " ", "&amp;": "&", "&lt;": "<", "&gt;": ">", "&quot;": "\"", "&#39;": "'" };

/** Plain text of a fragment of HTML — tags dropped, common entities decoded, whitespace collapsed. */
export function stripHtml (html) {
  if (!html) return "";
  return String(html)
    .replace(/<br\s*\/?>/gi, " ")
    .replace(/<[^>]*>/g, " ")
    .replace(/&(nbsp|amp|lt|gt|quot|#39);/g, (m) => ENTITIES[m] ?? m)
    .replace(/\s+/g, " ")
    .trim();
}

/** True when the fragment carries an image, which counts as content even with no text around it. */
export function hasImage (html) {
  return !!html && /<img\s+[^>]*src=/i.test(html);
}

/** An editor that was typed in and emptied still posts "<p><br></p>"; this is what "has content" means. */
export function hasRichTextContent (html) {
  return stripHtml(html).length > 0 || hasImage(html);
}

/** First `max` characters of the plain text, with an ellipsis when cut. */
export function excerpt (html, max = 140) {
  const text = stripHtml(html);
  return text.length > max ? `${text.slice(0, max - 1)}…` : text;
}
