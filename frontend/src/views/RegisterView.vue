<script setup lang="ts">
import { ref } from 'vue'
import { useRouter, RouterLink } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { useToasts } from '../composables/useToasts'

const router = useRouter()
const auth = useAuthStore()
const toast = useToasts()

const displayName = ref('')
const email = ref('')
const password = ref('')
const loading = ref(false)

async function onSubmit() {
  loading.value = true
  try {
    await auth.register(email.value, password.value, displayName.value)
    toast.push('Account created', `Hi ${auth.user?.displayName}!`)
    router.push('/notes')
  } catch (e: any) {
    toast.push('Register failed', e?.response?.data?.message ?? 'Please try again.')
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="min-h-screen bg-gradient-to-b from-slate-950 via-slate-950 to-indigo-950/20">
    <div class="mx-auto flex min-h-screen max-w-xl items-center justify-center px-4">
      <div class="glass w-full p-6 shadow-2xl">
        <h2 class="text-xl font-semibold">Create account</h2>
        <p class="mt-1 text-sm text-slate-300">Your notes are private to your account.</p>

        <form class="mt-6 space-y-4" @submit.prevent="onSubmit">
          <div>
            <label class="text-sm text-slate-300">Display name</label>
            <input class="input mt-2" v-model="displayName" type="text" placeholder="Your name" required />
          </div>
          <div>
            <label class="text-sm text-slate-300">Email</label>
            <input class="input mt-2" v-model="email" type="email" placeholder="you@example.com" required />
          </div>
          <div>
            <label class="text-sm text-slate-300">Password</label>
            <input class="input mt-2" v-model="password" type="password" placeholder="Min 8 characters" minlength="8" required />
          </div>

          <button class="btn-primary w-full" :disabled="loading">
            <span v-if="loading">Creating…</span>
            <span v-else>Create account</span>
          </button>

          <p class="text-center text-sm text-slate-300">
            Already have an account?
            <RouterLink class="text-indigo-300 hover:text-indigo-200" to="/login">Sign in</RouterLink>
          </p>
        </form>
      </div>
    </div>
  </div>
</template>
