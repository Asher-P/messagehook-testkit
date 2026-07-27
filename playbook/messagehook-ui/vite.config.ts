import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Dev only: proxy /api to the running service (dotnet run defaults are noted in the service README).
// In the container the service serves this build from wwwroot, so /api is same-origin and no proxy is used.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': 'http://localhost:8099'
    }
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true
  }
})
