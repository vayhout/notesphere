<template>
  <div v-if="open" class="fixed inset-0 z-50 flex items-center justify-center p-4">
    <div class="absolute inset-0 bg-black/60" @click="emit('close')"></div>

    <div class="relative w-full max-w-4xl glass p-4">
      <div class="flex items-center justify-between gap-2">
        <h3 class="text-lg font-semibold">Draw</h3>
        <button class="btn" @click="emit('close')">Close</button>
      </div>

      <div class="mt-3 flex flex-wrap items-center gap-2">
        <label class="text-sm text-slate-300">Brush</label>
        <input type="range" min="1" max="24" v-model.number="brush" class="w-40" />
        <span class="text-sm text-slate-200 w-10 text-right">{{ brush }}</span>

        <label class="text-sm text-slate-300 ml-2">Color</label>
        <input type="color" v-model="color" class="h-9 w-12 rounded-md border border-white/10 bg-transparent" />

        <button class="btn ml-auto" @click="clearCanvas">Clear</button>
        <button class="btn-primary" @click="saveAsImage" :disabled="saving">{{ saving ? 'Saving...' : 'Insert into note' }}</button>
      </div>

      <div class="mt-3 rounded-xl border border-white/10 bg-slate-950/40 p-2">
        <canvas ref="canvasRef" class="w-full rounded-lg touch-none" :width="w" :height="h"></canvas>
      </div>

      <p class="mt-2 text-xs text-slate-400">
        Tip: Use mouse or touch to draw. Click "Insert into note" to upload and insert a markdown image.
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref, watch } from 'vue'

const props = defineProps<{
  open: boolean
  saving?: boolean
  width?: number
  height?: number
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'save', blob: Blob): void
}>()

const canvasRef = ref<HTMLCanvasElement | null>(null)
const ctx = ref<CanvasRenderingContext2D | null>(null)

const brush = ref(6)
const color = ref('#7c3aed')

const w = props.width ?? 1200
const h = props.height ?? 520

let drawing = false
let lastX = 0
let lastY = 0

function getPos(e: MouseEvent | TouchEvent) {
  const canvas = canvasRef.value!
  const rect = canvas.getBoundingClientRect()
  const clientX = 'touches' in e ? e.touches[0].clientX : (e as MouseEvent).clientX
  const clientY = 'touches' in e ? e.touches[0].clientY : (e as MouseEvent).clientY
  return {
    x: ((clientX - rect.left) / rect.width) * canvas.width,
    y: ((clientY - rect.top) / rect.height) * canvas.height,
  }
}

function start(e: MouseEvent | TouchEvent) {
  if (!ctx.value) return
  drawing = true
  const p = getPos(e)
  lastX = p.x
  lastY = p.y
}

function move(e: MouseEvent | TouchEvent) {
  if (!drawing || !ctx.value) return
  e.preventDefault()
  const p = getPos(e)
  ctx.value.strokeStyle = color.value
  ctx.value.lineWidth = brush.value
  ctx.value.lineCap = 'round'
  ctx.value.beginPath()
  ctx.value.moveTo(lastX, lastY)
  ctx.value.lineTo(p.x, p.y)
  ctx.value.stroke()
  lastX = p.x
  lastY = p.y
}

function end() {
  drawing = false
}

function clearCanvas() {
  if (!ctx.value || !canvasRef.value) return
  ctx.value.clearRect(0, 0, canvasRef.value.width, canvasRef.value.height)
}

function saveAsImage() {
  const canvas = canvasRef.value
  if (!canvas) return
  canvas.toBlob((blob) => {
    if (!blob) return
    emit('save', blob)
  }, 'image/png')
}

function attach() {
  const canvas = canvasRef.value
  if (!canvas) return
  const c = canvas.getContext('2d')
  if (!c) return
  ctx.value = c
  // nice background for transparent strokes (optional)
  c.fillStyle = 'rgba(0,0,0,0)'
  c.fillRect(0,0,canvas.width,canvas.height)

  canvas.addEventListener('mousedown', start)
  canvas.addEventListener('mousemove', move)
  window.addEventListener('mouseup', end)

  canvas.addEventListener('touchstart', start, { passive: false })
  canvas.addEventListener('touchmove', move, { passive: false })
  window.addEventListener('touchend', end)
}

function detach() {
  const canvas = canvasRef.value
  if (!canvas) return
  canvas.removeEventListener('mousedown', start)
  canvas.removeEventListener('mousemove', move)
  window.removeEventListener('mouseup', end)

  canvas.removeEventListener('touchstart', start as any)
  canvas.removeEventListener('touchmove', move as any)
  window.removeEventListener('touchend', end)
}

onMounted(() => {
  if (props.open) attach()
})

watch(() => props.open, (v) => {
  if (v) setTimeout(attach, 0)
  else detach()
})

onBeforeUnmount(detach)
</script>
