import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  // Element Plus import toàn bộ nên bundle chính lớn; chấp nhận được với app chạy local
  build: { chunkSizeWarningLimit: 1500 },
  server: {
    port: 5173,
    // Gọi /api qua proxy để không phải cấu hình CORS ở backend
    proxy: {
      '/api': { target: 'http://localhost:5080', changeOrigin: true },
    },
  },
})
