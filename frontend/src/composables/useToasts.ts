import { reactive } from 'vue'

type Toast = { id: string; title: string; message?: string }

const state = reactive({
  items: [] as Toast[]
})

function push(title: string, message?: string) {
  const id = crypto.randomUUID()
  state.items.unshift({ id, title, message })
  setTimeout(() => dismiss(id), 3500)
}

function dismiss(id: string) {
  const idx = state.items.findIndex(t => t.id === id)
  if (idx >= 0) state.items.splice(idx, 1)
}

export function useToasts() {
  return { items: state.items, push, dismiss }
}
