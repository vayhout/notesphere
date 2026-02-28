import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";

export default defineConfig({
  base: "/notesphere/", // VERY IMPORTANT for GitHub Pages
  plugins: [vue()],
  server: {
    port: 5173,
  },
});
