import { ref, computed, nextTick } from "vue";

// A thread read the way a chat is: the newest page first, scrolled to its foot, older pages pulled in as
// the reader scrolls up, with their place on the page kept while the older entries arrive above them.
//
//   fetchPage({ beforeId, take }) -> Promise<{ data: [newest first], hasMore, total, ...anything else }>
//   scroller: a ref to the element that scrolls the thread
//
// `items` are kept oldest to newest, the order they render in. Every item needs an `id`: the id of the
// oldest item shown is the cursor the next page is read from, so entries written meanwhile cannot shift
// the pages the way page numbers would.
export function useChatThread ({ fetchPage, pageSize = 10, scroller = null }) {
  const items = ref([]);
  const page = ref({});           // the last page as the API returned it (counts, flags, …)
  const hasMore = ref(false);
  const total = ref(0);
  const loading = ref(false);     // the first page
  const loadingMore = ref(false); // an older page
  let generation = 0;             // answers to a superseded load are dropped

  const scrollEl = () => scroller?.value || null;

  // The element the reader actually scrolls: the thread when it has a scrollbar of its own, else the page.
  const scrollingParent = () => {
    const el = scrollEl();
    if (el && el.scrollHeight > el.clientHeight + 1) return el;
    return document.scrollingElement || document.documentElement;
  };

  const oldestFirst = (data) => [...(data || [])].reverse();

  const scrollToBottom = async () => {
    await nextTick();
    const el = scrollEl();
    if (el) el.scrollTop = el.scrollHeight;
  };

  const loadOlder = async () => {
    if (loading.value || loadingMore.value || !hasMore.value || !items.value.length) return false;
    const gen = generation;
    loadingMore.value = true;
    const el = scrollingParent();
    const heightBefore = el.scrollHeight;
    const topBefore = el.scrollTop;
    try {
      const result = await fetchPage({ beforeId: items.value[0].id, take: pageSize });
      if (gen !== generation) return false;
      const older = oldestFirst(result?.data);
      hasMore.value = !!result?.hasMore && older.length > 0;
      if (result?.total != null) total.value = result.total;
      page.value = result || page.value;
      if (!older.length) return false;
      items.value = [...older, ...items.value];
      await nextTick();
      // What the reader was looking at now sits lower by exactly the height of what arrived above it.
      el.scrollTop = topBefore + (el.scrollHeight - heightBefore);
      return true;
    } finally {
      if (gen === generation) loadingMore.value = false;
    }
  };

  // A thread shorter than its pane leaves nothing to scroll: fill the pane before the reader has to ask.
  const fillViewport = async (gen) => {
    for (let i = 0; i < 3 && hasMore.value && gen === generation; i++) {
      const el = scrollEl();
      if (!el || el.scrollHeight > el.clientHeight + 1) return;
      if (!(await loadOlder())) return;
      await scrollToBottom();
    }
  };

  const load = async () => {
    const gen = ++generation;
    loading.value = true;
    try {
      const result = await fetchPage({ beforeId: null, take: pageSize });
      if (gen !== generation) return;
      items.value = oldestFirst(result?.data);
      hasMore.value = !!result?.hasMore;
      total.value = result?.total ?? items.value.length;
      page.value = result || {};
    } finally {
      if (gen === generation) loading.value = false;
    }
    if (gen !== generation) return;
    await scrollToBottom();
    await fillViewport(gen);
  };

  // Close to the top of the thread: time for the page before.
  const onScroll = (event) => {
    const el = event?.target || scrollEl();
    if (el && el.scrollTop <= 64) loadOlder();
  };

  const isEmpty = computed(() => !loading.value && !items.value.length);

  return { items, page, hasMore, total, loading, loadingMore, isEmpty, load, loadOlder, onScroll, scrollToBottom };
}

// Pages an already complete, newest-first list the way the thread endpoints do, for an API that has no
// thread endpoint yet: the screen still reads like a chat, it just has everything in hand already.
export function pageFromList (newestFirst, { beforeId, take }) {
  const list = newestFirst || [];
  const start = beforeId ? list.findIndex((item) => item.id === beforeId) + 1 : 0;
  return { data: list.slice(start, start + take), hasMore: start + take < list.length, total: list.length };
}
