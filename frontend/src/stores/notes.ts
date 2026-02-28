import { defineStore } from 'pinia'
import api from '../lib/api'

export type Note = {
  id: string
  title: string
  content: string
  tags: string[]
  isPinned: boolean
  isArchived: boolean
  createdAt: string
  updatedAt: string
}

type PagedResult<T> = { items: T[]; total: number; page: number; pageSize: number }

export type DashboardStats = {
  totalNotes: number
  activeNotes: number
  pinnedNotes: number
  archivedNotes: number
  deletedNotes: number
  tagsUsed: number
  updatedLast7Days: number
}


export type NoteQuery = {
  search?: string
  sortBy?: 'title' | 'createdAt' | 'updatedAt'
  sortDir?: 'asc' | 'desc'
  pinned?: boolean | null
  archived?: boolean | null
  tag?: string | null
  page?: number
  pageSize?: number
}

export const useNotesStore = defineStore('notes', {
  state: () => ({
    items: [] as Note[],
    total: 0,
    page: 1,
    pageSize: 20,
    loading: false,
    selectedId: null as string | null,
    stats: null as DashboardStats | null
  }),
  getters: {
    selectedNote(state) {
      return state.items.find(n => n.id === state.selectedId) ?? null
    }
  },
  actions: {
    async fetch(query: NoteQuery) {
      this.loading = true
      try {
        const { data } = await api.get<PagedResult<Note>>('/api/notes', { params: query })
        this.items = data.items
        this.total = data.total
        this.page = data.page
        this.pageSize = data.pageSize
        if (this.items.length && !this.selectedId) this.selectedId = this.items[0].id
        if (this.selectedId && !this.items.some(n => n.id === this.selectedId)) {
          this.selectedId = this.items[0]?.id ?? null
        }
      } finally {
        this.loading = false
      }
    },
    select(id: string) {
      this.selectedId = id
    },
    async create(payload: Partial<Note> & { title: string; content: string; tags?: string[] }) {
      const { data } = await api.post<Note>('/api/notes', {
        title: payload.title,
        content: payload.content,
        tags: payload.tags ?? [],
        isPinned: payload.isPinned ?? false,
        isArchived: payload.isArchived ?? false
      })
      return data
    },
    async update(id: string, payload: Partial<Note> & { title: string; content: string; tags?: string[] }) {
      const { data } = await api.put<Note>(`/api/notes/${id}`, {
        title: payload.title,
        content: payload.content,
        tags: payload.tags ?? [],
        isPinned: payload.isPinned ?? false,
        isArchived: payload.isArchived ?? false
      })
      return data
    },
    async fetchTrash(query: NoteQuery) {
  const { data } = await api.get<PagedResult<Note>>('/api/notes/trash', { params: query })
  this.items = data.items
  this.total = data.total
  this.page = data.page
  this.pageSize = data.pageSize
  // trash view doesn't auto-select a note
  this.selectedId = data.items[0]?.id ?? null
},

async restore(id: string) {
  await api.post(`/api/notes/${id}/restore`)
  // refresh stats after restore
  await this.fetchStats()
},

async purge(id: string) {
  await api.delete(`/api/notes/${id}/purge`)
  await this.fetchStats()
},

async fetchStats() {
  const { data } = await api.get<DashboardStats>('/api/dashboard/stats')
  this.stats = data
},

async remove(id: string) {
      await api.delete(`/api/notes/${id}`)
    }
  }
})
