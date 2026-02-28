<template>
  <div v-if="open" class="fixed inset-0 z-50 flex items-center justify-center p-4">
    <div class="absolute inset-0 bg-black/60" @click="emit('cancel')"></div>
    <div class="relative w-full max-w-md glass p-4">
      <h3 class="text-lg font-semibold">{{ title }}</h3>
      <p class="mt-2 text-sm text-slate-300">{{ message }}</p>

      <div class="mt-4 flex justify-end gap-2">
        <button class="btn" @click="emit('cancel')">{{ cancelText }}</button>
        <button class="btn-danger" @click="emit('confirm')">{{ confirmText }}</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  open: boolean
  title: string
  message: string
  confirmText?: string
  cancelText?: string
}>()

const emit = defineEmits<{
  (e: 'confirm'): void
  (e: 'cancel'): void
}>()
</script>
