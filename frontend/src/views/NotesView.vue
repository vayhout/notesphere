<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useRouter } from "vue-router";
import { useAuthStore } from "../stores/auth";
import { useNotesStore } from "../stores/notes";
import { useToasts } from "../composables/useToasts";

import MarkdownPreview from "../components/MarkdownPreview.vue";
import DrawingModal from "../components/DrawingModal.vue";
import ConfirmModal from "../components/ConfirmModal.vue";
import ImageInsertModal from "../components/ImageInsertModal.vue";

import { uploadImage } from "../services/uploads";

type StatusFilter = "all" | "active" | "archived" | "trash";
type PinnedFilter = "all" | "pinned" | "unpinned";

const router = useRouter();
const auth = useAuthStore();
const notes = useNotesStore();
const toast = useToasts();

// Query UI
const status = ref<StatusFilter>("active");
const pinnedFilter = ref<PinnedFilter>("all");
const search = ref("");
const sortBy = ref<"updatedAt" | "createdAt" | "title">("updatedAt");
const sortDir = ref<"asc" | "desc">("desc");
const tag = ref("");

// Editor state
const selected = computed(() => notes.selectedNote);

const title = ref("");
const editorContent = ref("");
const tagsInput = ref("");
const isPinned = ref(false);
const isArchived = ref(false);

const contentRef = ref<HTMLTextAreaElement | null>(null);
const previewMode = ref(false);

const dirty = ref(false);
function markDirty() {
  dirty.value = true;
}

function repairBrokenMarkdownImages(text: string) {
  return (text || "")
    // Fix: ![alt]\n(url)  -> ![alt](url)
    .replace(/!\[([^\]]*)\]\s*\n+\s*\(([^)]+)\)/g, "![$1]($2)")
    // Fix: ![]\n(url) -> ![image](url)
    .replace(/!\[\]\(([^)]+)\)/g, "![image]($1)")
    // remove whitespace inside URL parentheses
    .replace(/!\[([^\]]*)\]\(\s*([^)]+?)\s*\)/g, (_m, a, u) => {
      const cleanUrl = String(u).replace(/\s+/g, "");
      const alt = String(a || "image");
      return `![${alt}](${cleanUrl})`;
    });
}

function loadFromSelected() {
  const n = selected.value;
  if (!n) {
    title.value = "";
    editorContent.value = "";
    tagsInput.value = "";
    isPinned.value = false;
    isArchived.value = false;
    dirty.value = false;
    return;
  }

  title.value = n.title;
  editorContent.value = repairBrokenMarkdownImages(n.content);
  tagsInput.value = (n.tags ?? []).join(", ");
  isPinned.value = n.isPinned;
  isArchived.value = n.isArchived;
  dirty.value = false;
}

watch(selected, loadFromSelected);

// caret insertion (cursor-position insertion)
function insertIntoContent(snippet: string) {
  const el = contentRef.value;
  const current = editorContent.value ?? "";

  if (!el) {
    editorContent.value = current + snippet;
    markDirty();
    return;
  }

  const start = el.selectionStart ?? current.length;
  const end = el.selectionEnd ?? current.length;

  const before = current.slice(0, start);
  const after = current.slice(end);

  editorContent.value = before + snippet + after;

  requestAnimationFrame(() => {
    el.focus();
    const pos = start + snippet.length;
    el.setSelectionRange(pos, pos);
  });

  markDirty();
}

function insertImageSmart(oneLine: string) {
  const content = editorContent.value ?? "";
  const hasAnyImage =
    /!\[[^\]]*\]\([^)]+\)/.test(content) || /<img\b[^>]*\bsrc=/.test(content);

  // Requirement: first image goes to top, next images go to bottom.
  if (!hasAnyImage) {
    editorContent.value = `${oneLine}\n\n${content.trimStart()}`;
    markDirty();
    return;
  }

  editorContent.value = `${content.trimEnd()}\n\n${oneLine}\n`;
  markDirty();
}

// Extract images so user can drag & reposition (move freely)
type ImageItem = { alt: string; url: string; snippet: string };
const images = computed<ImageItem[]>(() => {
  const src = editorContent.value ?? "";
  const out: ImageItem[] = [];

  // Markdown images: ![alt](url)
  const mdRe = /!\[([^\]]*)\]\(([^)]+)\)/g;
  let m: RegExpExecArray | null;
  while ((m = mdRe.exec(src))) {
    const alt = (m[1] ?? "").trim() || "image";
    const url = (m[2] ?? "").trim();
    out.push({ alt, url, snippet: `![${alt}](${url})` });
  }

  // HTML images: <img src="..." ...>
  const htmlRe = /<img\b[^>]*\bsrc=("|')([^"']+)(\1)[^>]*>/gi;
  while ((m = htmlRe.exec(src))) {
    const url = (m[2] ?? "").trim();
    const tag = (m[0] ?? "").trim();

    // Try to extract alt for nicer labels
    const altMatch = tag.match(/\balt=("|')([^"']*)(\1)/i);
    const alt = (altMatch?.[2] ?? "image").trim() || "image";
    out.push({ alt, url, snippet: tag });
  }

  return out;
});

function removeFirstImageOccurrence(url: string) {
  // Remove first occurrence of either Markdown or HTML image that matches url.
  const src = editorContent.value ?? "";
  const escaped = url.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");

  const mdRe = new RegExp(String.raw`!\[[^\]]*\]\(\s*${escaped}\s*\)`, "m");
  const htmlRe = new RegExp(String.raw`<img\\b[^>]*\\bsrc=("|')\s*${escaped}\s*\\1[^>]*>`, "i");

  let next = src;
  if (mdRe.test(next)) next = next.replace(mdRe, "");
  else if (htmlRe.test(next)) next = next.replace(htmlRe, "");

  editorContent.value = next.replace(/\n{3,}/g, "\n\n");
  markDirty();
}

function onDragStartImage(e: DragEvent, img: ImageItem) {
  if (!e.dataTransfer) return;
  e.dataTransfer.setData("application/x-notesphere-image-url", img.url);
  e.dataTransfer.setData("text/plain", img.snippet);
  e.dataTransfer.effectAllowed = "move";
}

function getCaretIndexFromDrop(el: HTMLTextAreaElement, e: DragEvent) {
  // Best-effort caret positioning
  el.focus();
  // If browser supports caretPositionFromPoint / caretRangeFromPoint, use it.
  // For textarea, we can't set caret by coordinates reliably across browsers,
  // so we fallback to current selectionStart.
  return el.selectionStart ?? (editorContent.value?.length ?? 0);
}

function onDropOnEditor(e: DragEvent) {
  // 1) If user dropped an existing image from sidebar -> move it
  const movedUrl = e.dataTransfer?.getData("application/x-notesphere-image-url");
  const md = e.dataTransfer?.getData("text/plain");

  if (movedUrl && md) {
    const el = contentRef.value;
    if (!el) return;

    // Remove original occurrence, then insert at caret
    removeFirstImageOccurrence(movedUrl);

    const pos = getCaretIndexFromDrop(el, e);
    const current = editorContent.value ?? "";
    const before = current.slice(0, pos);
    const after = current.slice(pos);

    // Ensure clean spacing around image so paragraphs behave naturally
    const snippet = `\n\n${md.trim()}\n\n`;
    editorContent.value = before + snippet + after;
    markDirty();
    return;
  }

  // 2) Else treat as file drop (upload)
  onDropFileUpload(e);
}

// Image insert modal (resize before insert)
const imageModalOpen = ref(false);
const pendingImageUrl = ref("");
const pendingImageAlt = ref("");

function openImageInsert(url: string, alt = "") {
  pendingImageUrl.value = (url ?? "").toString();
  pendingImageAlt.value = alt;
  imageModalOpen.value = true;
}

function onImageModalInsert(payload: { alt: string; width: number }) {
  const apiBase = import.meta.env.VITE_API_BASE_URL;

  // URL from backend is usually "/uploads/..."
  let url = (pendingImageUrl.value ?? "");
  url = url.replace(/\s+/g, "");

  const altRaw = (payload?.alt ?? pendingImageAlt.value ?? "image");
  const alt = String(altRaw).trim().replace(/\r?\n/g, " ") || "image";

  const fullUrl = url.startsWith("http") ? url : `${apiBase}${url}`;

  // Use HTML for width control, but keep it one-line.
  const width = Math.max(80, Math.min(1200, Number(payload?.width ?? 600) || 600));
  const mdOneLine = `<img src="${fullUrl}" alt="${alt}" width="${width}" />`;

  // spacing around image so paragraphs can be separated by user's blank lines
  insertImageSmart(mdOneLine);

  imageModalOpen.value = false;
  pendingImageUrl.value = "";
}

const uploadBusy = ref(false);
const drawOpen = ref(false);
const drawBusy = ref(false);

async function onUploadImage(e: Event) {
  const input = e.target as HTMLInputElement;
  const file = input.files?.[0];
  if (!file) return;

  try {
    uploadBusy.value = true;
    const res = await uploadImage(file);
    openImageInsert(res.url);
    toast.push("Image uploaded", "Choose width then insert.");
  } catch (err: any) {
    toast.push("Upload failed", err?.response?.data?.message ?? "Please try another image.");
  } finally {
    uploadBusy.value = false;
    input.value = "";
  }
}

async function onDropFileUpload(e: DragEvent) {
  const file = e.dataTransfer?.files?.[0];
  if (!file) return;
  if (!file.type.startsWith("image/")) {
    toast.push("Not an image", "Drop a PNG/JPG/WebP/GIF file.");
    return;
  }

  try {
    uploadBusy.value = true;
    const res = await uploadImage(file);
    openImageInsert(res.url);
    toast.push("Image uploaded", "Choose width then insert.");
  } catch (err: any) {
    toast.push("Upload failed", err?.response?.data?.message ?? "Please try another image.");
  } finally {
    uploadBusy.value = false;
  }
}

async function onDrawSave(blob: Blob) {
  try {
    drawBusy.value = true;
    const file = new File([blob], `drawing-${Date.now()}.png`, { type: "image/png" });
    const res = await uploadImage(file);
    openImageInsert(res.url, "drawing");
    toast.push("Drawing uploaded", "Choose width then insert.");
    drawOpen.value = false;
  } catch (err: any) {
    toast.push("Upload failed", err?.response?.data?.message ?? "Please try again.");
  } finally {
    drawBusy.value = false;
  }
}

// Fetch notes
async function refresh() {
  const q: any = {
    search: search.value || undefined,
    sortBy: sortBy.value,
    sortDir: sortDir.value,
    tag: tag.value ? tag.value : null,
    page: 1,
    pageSize: 50,
    pinned: null,
    archived: null,
  };

  if (pinnedFilter.value === "pinned") q.pinned = true;
  if (pinnedFilter.value === "unpinned") q.pinned = false;

  if (status.value === "trash") {
    await notes.fetchTrash(q);
  } else {
    if (status.value === "active") q.archived = false;
    if (status.value === "archived") q.archived = true;
    await notes.fetch(q);
  }

  await notes.fetchStats();
}

onMounted(refresh);

// Debounce the expensive refresh when typing in search/tag.
let refreshTimer: number | undefined;
watch([status, pinnedFilter, sortBy, sortDir], () => {
  refresh();
});
watch([search, tag], () => {
  if (refreshTimer) window.clearTimeout(refreshTimer);
  refreshTimer = window.setTimeout(() => refresh(), 250);
});

// Sidebar ordering: pinned notes always on top (then normal sort)
const sidebarItems = computed(() => {
  const items = [...(notes.items ?? [])];
  items.sort((a: any, b: any) => {
    const ap = a.isPinned ? 1 : 0;
    const bp = b.isPinned ? 1 : 0;
    if (ap !== bp) return bp - ap; // pinned first
    // fallback keep backend order by updated/created/title
    return 0;
  });
  return items;
});

// CRUD
async function onNew() {
  const created = await notes.create({
    title: "Untitled note",
    content: "",
    tags: [],
    isPinned: false,
    isArchived: false,
  });
  await refresh();
  notes.select(created.id);
  previewMode.value = false;
}

function parseTags(): string[] {
  return (tagsInput.value || "")
    .split(",")
    .map((t) => t.trim())
    .filter(Boolean);
}

async function onSave() {
  if (!title.value.trim()) {
    toast.push("Title required", "Please add a title before saving.");
    return;
  }

  if (!selected.value) {
    await onNew();
    return;
  }

  const payload = {
    title: title.value.trim(),
    content: repairBrokenMarkdownImages(editorContent.value ?? ""),
    tags: parseTags(),
    isPinned: isPinned.value,
    isArchived: isArchived.value,
  };

  await notes.update(selected.value.id, payload);
  toast.push("Saved", "Note updated.");
  await refresh();
  dirty.value = false;
}

async function onDeleteSoft() {
  if (!selected.value) return;
  await notes.remove(selected.value.id);
  toast.push("Moved to Trash", "You can restore it from Trash.");
  await refresh();
}

async function onRestore() {
  if (!selected.value) return;
  await notes.restore(selected.value.id);
  toast.push("Restored", "Note restored from Trash.");
  await refresh();
}

const purgeConfirmOpen = ref(false);
const purgeTargetTitle = ref("");

function onRequestPurge() {
  if (!selected.value) return;
  purgeTargetTitle.value = selected.value.title;
  purgeConfirmOpen.value = true;
}

async function onConfirmPurge() {
  purgeConfirmOpen.value = false;
  if (!selected.value) return;
  await notes.purge(selected.value.id);
  toast.push("Purged", "Note permanently deleted.");
  await refresh();
}

// Logout
function logout() {
  auth.logout();
  router.push("/login");
}

function fmtDate(d: string) {
  if (!d) return "";
  const dt = new Date(d);
  if (Number.isNaN(dt.getTime())) return d;

  // Uses the user's PC/browser timezone automatically.
  return new Intl.DateTimeFormat(undefined, {
    year: "numeric",
    month: "short",
    day: "2-digit",
    hour: "numeric",
    minute: "2-digit",
    second: "2-digit",
    hour12: true,
  }).format(dt);
}

function tabBtnClass(active: boolean) {
  return active
    ? "btn-tab btn-tab-active"
    : "btn-tab";
}
</script>

<template>
  <div class="min-h-screen bg-slate-950 text-slate-100">
    <div class="mx-auto max-w-7xl px-4 py-6">
      <div class="glass p-4">
        <div class="flex flex-wrap items-center justify-between gap-3">
          <div class="flex items-center gap-3">
            <div
              class="h-10 w-10 rounded-xl bg-white/10 border border-white/10 flex items-center justify-center font-bold"
            >
              {{
                (
                  auth.user?.displayName?.[0] ??
                  auth.user?.email?.[0] ??
                  "U"
                ).toUpperCase()
              }}
            </div>
            <div>
              <div class="text-lg font-semibold">NoteSphere</div>
              <div class="text-xs text-slate-400">{{ auth.user?.email }}</div>
            </div>
          </div>

          <button class="btn-ghost" @click="logout">Logout</button>
        </div>

        <div class="mt-4 grid grid-cols-1 gap-4 lg:grid-cols-[360px_1fr]">
          <!-- Sidebar -->
          <aside class="glass p-3">
            <input
              class="input"
              v-model="search"
              placeholder="Search notes..."
            />

            <div class="mt-3 grid grid-cols-2 gap-2">
              <div>
                <label class="text-xs text-slate-400">Sort</label>
                <select class="input mt-1" v-model="sortBy">
                  <option value="updatedAt">Updated</option>
                  <option value="createdAt">Created</option>
                  <option value="title">Title</option>
                </select>
              </div>
              <div>
                <label class="text-xs text-slate-400">Direction</label>
                <select class="input mt-1" v-model="sortDir">
                  <option value="desc">Desc</option>
                  <option value="asc">Asc</option>
                </select>
              </div>
            </div>

            <div class="mt-3 grid grid-cols-2 gap-2">
              <div>
                <label class="text-xs text-slate-400">Status</label>
                <select class="input mt-1" v-model="status">
                  <option value="active">Active</option>
                  <option value="archived">Archived</option>
                  <option value="all">All</option>
                  <option value="trash">
                    Trash ({{ notes.stats?.deletedNotes ?? 0 }})
                  </option>
                </select>
              </div>
              <div>
                <label class="text-xs text-slate-400">Pinned</label>
                <select class="input mt-1" v-model="pinnedFilter">
                  <option value="all">All</option>
                  <option value="pinned">Pinned only</option>
                  <option value="unpinned">Not pinned</option>
                </select>
              </div>
            </div>

            <div class="mt-3 grid grid-cols-2 gap-2">
              <div class="col-span-2">
                <label class="text-xs text-slate-400">Tag</label>
                <input
                  class="input mt-1"
                  v-model="tag"
                  placeholder="e.g. work"
                />
              </div>
            </div>

            <div class="mt-3 flex gap-2">
              <button class="btn-primary w-full" @click="onNew">+ New</button>
            </div>

            <div class="mt-4 space-y-2 max-h-[420px] overflow-auto pr-1">
              <button
                v-for="n in sidebarItems"
                :key="n.id"
                class="w-full text-left glass p-3 hover:bg-white/10 transition"
                :class="{
                  'ring-2 ring-indigo-500/50': n.id === notes.selectedId,
                }"
                @click="
                  notes.select(n.id);
                  previewMode = false;
                "
              >
                <div class="flex items-start justify-between gap-2">
                  <div class="font-semibold truncate">{{ n.title }}</div>
                  <span v-if="n.isPinned" class="badge">📌 Pinned</span>
                </div>
                <div class="mt-1 text-xs text-slate-400">
                  Created {{ fmtDate(n.createdAt) }}
                </div>
                <div class="mt-2 flex flex-wrap gap-1">
                  <span
                    v-for="t in (n.tags ?? []).slice(0, 3)"
                    :key="t"
                    class="badge"
                    >#{{ t }}</span
                  >
                </div>
              </button>

              <div
                v-if="!notes.items.length && !notes.loading"
                class="text-sm text-slate-400 py-6 text-center"
              >
                No notes found.
              </div>
            </div>

            <!-- Image manager (drag these into the editor to reposition) -->
            <div class="mt-4 glass p-3">
              <div class="flex items-center justify-between">
                <div class="text-sm font-semibold">Images</div>
                <div class="text-xs text-slate-400">{{ images.length }}</div>
              </div>

              <div v-if="!images.length" class="text-xs text-slate-400 mt-2">
                Upload or draw an image. Then you can drag it into the editor to move it.
              </div>

              <div v-else class="mt-3 grid grid-cols-2 gap-2">
                <div
                  v-for="img in images"
                  :key="img.url"
                  class="glass p-2 cursor-move hover:bg-white/10 transition"
                  draggable="true"
                  @dragstart="onDragStartImage($event, img)"
                  title="Drag into the editor to reposition"
                >
                  <img :src="img.url" class="w-full h-16 object-cover rounded-lg border border-white/10" />
                  <div class="mt-1 text-[11px] text-slate-300 truncate">{{ img.alt }}</div>
                </div>
              </div>
            </div>
          </aside>

          <!-- Editor -->
          <section class="glass p-4">
            <div v-if="!selected" class="text-slate-400">
              Select a note or create a new one.
            </div>

            <div v-else>
              <div class="flex flex-wrap items-center justify-between gap-2">
                <div class="flex items-center gap-2">
                  <button
                    :class="tabBtnClass(!previewMode)"
                    @click="previewMode = false"
                    :aria-pressed="!previewMode"
                  >
                    Edit
                  </button>
                  <button
                    :class="tabBtnClass(previewMode)"
                    @click="previewMode = true"
                    :aria-pressed="previewMode"
                  >
                    Preview
                  </button>

                  <span class="badge">Created: {{ fmtDate(selected.createdAt) }}</span>
                  <span class="badge">Updated: {{ fmtDate(selected.updatedAt) }}</span>

                  <span v-if="dirty" class="badge">Unsaved</span>
                </div>

                <div class="flex flex-wrap items-center gap-2">
                  <label
                    class="btn cursor-pointer"
                    :class="{ 'opacity-60 pointer-events-none': uploadBusy }"
                  >
                    <input
                      type="file"
                      accept="image/*"
                      class="hidden"
                      @change="onUploadImage"
                    />
                    {{ uploadBusy ? "Uploading..." : "Upload image" }}
                  </label>

                  <button class="btn" @click="drawOpen = true" :disabled="drawBusy">
                    {{ drawBusy ? "Saving..." : "Draw" }}
                  </button>

                  <button
                    v-if="status === 'trash'"
                    class="btn-ghost"
                    @click="onRestore"
                  >
                    Restore
                  </button>

                  <button
                    v-if="status === 'trash'"
                    class="btn-danger"
                    @click="onRequestPurge"
                  >
                    Purge
                  </button>

                  <button v-else class="btn-danger" @click="onDeleteSoft">
                    Delete
                  </button>

                  <button class="btn-primary" @click="onSave">Save</button>
                </div>
              </div>

              <div class="mt-4 grid grid-cols-1 gap-4 lg:grid-cols-2">
                <div>
                  <label class="text-sm text-slate-300">Title</label>
                  <input class="input mt-2" v-model="title" @input="markDirty" />

                  <label class="mt-4 block text-sm text-slate-300">Tags (comma-separated)</label>
                  <input
                    class="input mt-2"
                    v-model="tagsInput"
                    @input="markDirty"
                    placeholder="work, ideas, todo"
                  />

                  <div class="mt-3 flex items-center gap-4 text-sm text-slate-200">
                    <label class="inline-flex items-center gap-2">
                      <input type="checkbox" v-model="isPinned" @change="markDirty" />
                      Pin
                    </label>
                    <label class="inline-flex items-center gap-2">
                      <input type="checkbox" v-model="isArchived" @change="markDirty" />
                      Archive
                    </label>
                  </div>

                  <label class="mt-4 block text-sm text-slate-300">
                    Content (Markdown + images supported)
                  </label>

                  <textarea
                    v-if="!previewMode"
                    ref="contentRef"
                    class="input mt-2 min-h-[360px] resize-y"
                    v-model="editorContent"
                    @input="markDirty"
                    @dragover.prevent
                    @drop.prevent="onDropOnEditor"
                    placeholder="Write something… (Tip: drag & drop an image file here, or drag an existing image from the Images box to move it)"
                  ></textarea>

                  <div v-else class="mt-2 glass p-3 min-h-[360px] overflow-auto">
                    <MarkdownPreview :markdown="editorContent" />
                  </div>
                </div>

                <div class="glass p-3 min-h-[520px]">
                  <div class="text-sm text-slate-300 mb-2">Live preview</div>
                  <MarkdownPreview :markdown="editorContent" />
                </div>
              </div>
            </div>
          </section>
        </div>

        <div class="mt-4 flex flex-wrap gap-2 text-xs text-slate-400">
          <span class="badge">Vue + TS</span>
          <span class="badge">Tailwind</span>
          <span class="badge">ASP.NET + Dapper</span>
          <span class="badge">SQL Server</span>
        </div>
      </div>
    </div>

    <DrawingModal
      :open="drawOpen"
      :saving="drawBusy"
      @close="drawOpen = false"
      @save="onDrawSave"
    />

    <ImageInsertModal
      :open="imageModalOpen"
      :url="pendingImageUrl"
      :alt="pendingImageAlt"
      :width="600"
      @insert="onImageModalInsert"
      @cancel="imageModalOpen = false"
    />

    <ConfirmModal
      :open="purgeConfirmOpen"
      title="Permanently delete?"
      :message="`This will permanently delete '${purgeTargetTitle}'. This cannot be undone.`"
      confirmText="Purge"
      cancelText="Cancel"
      @confirm="onConfirmPurge"
      @cancel="purgeConfirmOpen = false"
    />
  </div>
</template>

<style scoped>
/* simple tab look (re-uses your existing button vibe) */
.btn-tab {
  @apply px-4 py-2 rounded-xl border border-white/10 bg-white/5 hover:bg-white/10 transition;
}
.btn-tab-active {
  @apply bg-indigo-600/30 border-indigo-400/40 ring-2 ring-indigo-500/30;
}
</style>
