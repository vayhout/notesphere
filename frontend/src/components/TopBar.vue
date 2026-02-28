<script setup lang="ts">
import { computed } from 'vue'
import { useAuthStore } from '../stores/auth'
import { useRouter } from 'vue-router'

const auth = useAuthStore()
const router = useRouter()
const initials = computed(() => (auth.user?.displayName ?? 'U').split(' ').map(s => s[0]).slice(0,2).join('').toUpperCase())

function logout() {
  auth.logout()
  router.push('/login')
}
</script>

<template>
  <div class="flex items-center justify-between gap-3 border-b border-white/10 px-4 py-3">
    <div class="flex items-center gap-3">
      <div class="h-10 w-10 rounded-2xl bg-indigo-500/20 border border-indigo-400/30 flex items-center justify-center font-bold">
        {{ initials }}
      </div>
      <div>
        <p class="text-sm font-semibold">NoteSphere</p>
        <p class="text-xs text-slate-300">{{ auth.user?.email }}</p>
      </div>
    </div>
    <button class="btn-ghost" @click="logout">Logout</button>
  </div>
</template>
