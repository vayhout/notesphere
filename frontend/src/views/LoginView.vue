<script setup lang="ts">
import { ref } from 'vue'
import { useRouter, RouterLink } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { useToasts } from '../composables/useToasts'

const router = useRouter()
const auth = useAuthStore()
const toast = useToasts()

const email = ref('demo@notesphere.dev')
const password = ref('Password123!')
const loading = ref(false)

async function onSubmit() {
  loading.value = true
  try {
    await auth.login(email.value, password.value)
    toast.push('Welcome back', auth.user?.displayName)
    router.push('/notes')
  } catch (e: any) {
    toast.push('Login failed', e?.response?.data?.message ?? 'Please check your credentials.')
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="min-h-screen bg-gradient-to-b from-slate-950 via-slate-950 to-indigo-950/20">
    <div class="mx-auto flex min-h-screen max-w-6xl items-center justify-center px-4">
      <div class="grid w-full grid-cols-1 gap-6 md:grid-cols-2">
        <div class="hidden md:flex flex-col justify-center">
          <h1 class="text-4xl font-bold tracking-tight">NoteSphere</h1>
          <p class="mt-3 text-slate-300">
            Fast notes, markdown preview, pinned & archived, tags, search, and clean UX.
          </p>
          <div class="mt-6 flex gap-2 text-xs text-slate-300">
            <span class="badge">Vue + TS</span>
            <span class="badge">Tailwind</span>
            <span class="badge">ASP.NET + Dapper</span>
            <span class="badge">SQL Server</span>
          </div>
        </div>

        <div class="glass w-full p-6 shadow-2xl">
          <h2 class="text-xl font-semibold">Sign in</h2>
          <p class="mt-1 text-sm text-slate-300">Use the demo account or your own.</p>

          <form class="mt-6 space-y-4" @submit.prevent="onSubmit">
            <div>
              <label class="text-sm text-slate-300">Email</label>
              <input class="input mt-2" v-model="email" type="email" placeholder="you@example.com" required />
            </div>
            <div>
              <label class="text-sm text-slate-300">Password</label>
              <input class="input mt-2" v-model="password" type="password" placeholder="••••••••" required />
            </div>

            <button class="btn-primary w-full" :disabled="loading">
              <span v-if="loading">Signing in…</span>
              <span v-else>Sign in</span>
            </button>

            <p class="text-center text-sm text-slate-300">
              No account?
              <RouterLink class="text-indigo-300 hover:text-indigo-200" to="/register">Create one</RouterLink>
            </p>
          </form>
        </div>
      </div>
    </div>
  </div>
</template>
