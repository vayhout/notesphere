<script setup lang="ts">
import { computed } from 'vue'
import { useToasts } from '../composables/useToasts'

const toasts = useToasts()
const items = computed(() => toasts.items)
</script>

<template>
  <div class="fixed right-4 top-4 z-50 flex w-[min(360px,calc(100vw-2rem))] flex-col gap-2">
    <div
      v-for="t in items"
      :key="t.id"
      class="glass px-4 py-3 shadow-lg"
    >
      <div class="flex items-start justify-between gap-3">
        <div>
          <p class="text-sm font-semibold">{{ t.title }}</p>
          <p v-if="t.message" class="mt-1 text-sm text-slate-300">{{ t.message }}</p>
        </div>
        <button class="btn-ghost px-2 py-1 text-xs" @click="toasts.dismiss(t.id)">✕</button>
      </div>
    </div>
  </div>
</template>
