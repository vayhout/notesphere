import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";

// GitHub Pages project site base path: /<repo-name>/
const repoName = "notesphere";
const isGitHubPages = process.env.GITHUB_PAGES === "true";

export default defineConfig({
  plugins: [vue()],
  base: isGitHubPages ? `/${repoName}/` : "/",
  server: {
    port: 5173,
  },
});
