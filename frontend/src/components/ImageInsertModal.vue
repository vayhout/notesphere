<script setup lang="ts">
import { ref, watch } from "vue";

const props = defineProps<{
  open: boolean;
  url: string;
  alt: string;
  width: number;
}>();

const emit = defineEmits<{
  (e: "insert", payload: { alt: string; width: number }): void;
  (e: "cancel"): void;
}>();

const altText = ref(props.alt || "image");
const w = ref(props.width || 600);

watch(
  () => props.open,
  (o) => {
    if (o) {
      altText.value = props.alt || "image";
      w.value = props.width || 600;
    }
  },
);

function insert() {
  emit("insert", {
    alt: altText.value || "image",
    width: Number(w.value) || 600,
  });
}
</script>

<template>
  <div
    v-if="open"
    class="fixed inset-0 z-50 flex items-center justify-center bg-black/60"
  >
    <div class="glass w-[520px] max-w-[92vw] p-4">
      <div class="text-lg font-semibold">Insert image</div>
      <div class="text-sm text-slate-400 mt-1 break-all">{{ url }}</div>

      <div class="mt-4 grid grid-cols-2 gap-3">
        <div>
          <label class="text-xs text-slate-400">Alt text</label>
          <input
            class="input mt-1"
            v-model="altText"
            placeholder="e.g. diagram"
          />
        </div>

        <div>
          <label class="text-xs text-slate-400">Width</label>
          <input
            class="input mt-1"
            type="number"
            v-model="w"
            min="80"
            max="1200"
            step="10"
          />
        </div>
      </div>

      <div class="mt-4 flex justify-end gap-2">
        <button class="btn-ghost" @click="emit('cancel')">Cancel</button>
        <button class="btn-primary" @click="insert">Insert</button>
      </div>
    </div>
  </div>
</template>
