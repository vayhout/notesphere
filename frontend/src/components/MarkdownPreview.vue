<script setup lang="ts">
import { computed } from "vue";
import { marked } from "marked";

const props = defineProps<{ markdown: string }>();

marked.setOptions({
  gfm: true,
  breaks: true,
});

const rendered = computed(() => marked.parse(props.markdown ?? ""));
</script>

<template>
  <!-- v-html is REQUIRED so images (and HTML <img>) render -->
  <div class="prose prose-invert max-w-none md-preview" v-html="rendered"></div>
</template>

<style scoped>
/* Images */
.md-preview :deep(img) {
  max-width: 100%;
  height: auto;
  display: block;
  margin: 0.75rem 0;
  border-radius: 0.75rem;
}

/* Paragraph spacing */
.md-preview :deep(p) {
  margin: 0.6rem 0;
}

/* Code blocks */
.md-preview :deep(pre) {
  overflow-x: auto;
}

.md-preview :deep(ol) {
  list-style: decimal;
  padding-left: 1.5rem;
  margin: 0.6rem 0;
}
.md-preview :deep(ul) {
  list-style: disc;
  padding-left: 1.5rem;
  margin: 0.6rem 0;
}
.md-preview :deep(li) {
  margin: 0.25rem 0;
}
.md-preview :deep(li::marker) {
  color: rgba(226, 232, 240, 0.95); /* visible on dark */
  font-weight: 600;
}
</style>
