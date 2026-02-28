<script setup lang="ts">
import ToastHost from './components/ToastHost.vue'
</script>

<template>
  <div class="min-h-screen">
    <router-view />
    <ToastHost />
  </div>
</template>
