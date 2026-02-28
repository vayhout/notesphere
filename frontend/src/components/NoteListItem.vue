<script setup lang="ts">
import type { Note } from '../stores/notes'

defineProps<{
  note: Note
  active: boolean
}>()

function formatDate(s: string) {
  const d = new Date(s)
  return d.toLocaleString(undefined, { year: 'numeric', month: 'short', day: '2-digit' })
}
</script>

<template>
  <button
    class="w-full text-left rounded-2xl border border-white/10 px-3 py-3 transition hover:bg-white/5"
    :class="active ? 'bg-white/10' : 'bg-white/5'"
  >
    <div class="flex items-start justify-between gap-2">
      <div class="min-w-0">
        <div class="flex items-center gap-2">
          <span v-if="note.isPinned" class="badge">📌 Pinned</span>
          <span v-if="note.isArchived" class="badge">🗄️ Archived</span>
        </div>
        <p class="mt-2 truncate font-semibold">{{ note.title }}</p>
        <p class="mt-1 text-xs text-slate-300">Created {{ formatDate(note.createdAt) }}</p>
      </div>
      <span class="text-xs text-slate-400">{{ formatDate(note.updatedAt) }}</span>
    </div>
    <div class="mt-2 flex flex-wrap gap-1">
      <span v-for="t in note.tags.slice(0,3)" :key="t" class="badge">#{{ t }}</span>
      <span v-if="note.tags.length > 3" class="badge">+{{ note.tags.length - 3 }}</span>
    </div>
  </button>
</template>
