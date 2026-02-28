import { defineStore } from 'pinia'
import api from '../lib/api'

type UserProfile = { id: string; email: string; displayName: string }
type AuthResponse = { accessToken: string; user: UserProfile }

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem('notesphere_token') as string | null,
    user: (localStorage.getItem('notesphere_user')
      ? JSON.parse(localStorage.getItem('notesphere_user')!)
      : null) as UserProfile | null
  }),
  getters: {
    isAuthenticated: (s) => !!s.token
  },
  actions: {
    async login(email: string, password: string) {
      const { data } = await api.post<AuthResponse>('/api/auth/login', { email, password })
      this.token = data.accessToken
      this.user = data.user
      localStorage.setItem('notesphere_token', data.accessToken)
      localStorage.setItem('notesphere_user', JSON.stringify(data.user))
    },
    async register(email: string, password: string, displayName: string) {
      const { data } = await api.post<AuthResponse>('/api/auth/register', { email, password, displayName })
      this.token = data.accessToken
      this.user = data.user
      localStorage.setItem('notesphere_token', data.accessToken)
      localStorage.setItem('notesphere_user', JSON.stringify(data.user))
    },
    logout() {
      this.token = null
      this.user = null
      localStorage.removeItem('notesphere_token')
      localStorage.removeItem('notesphere_user')
    }
  }
})
