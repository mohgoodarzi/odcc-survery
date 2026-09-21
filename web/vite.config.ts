import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

const srcPath = path.resolve(fileURLToPath(import.meta.url), '../src');

// در توسعه، برنامه‌ی React از طریق پروکسی به API می‌رسد، پس مرورگر یک مبدأ واحد می‌بیند.
// بنابراین CORS در dev فعال نمی‌شود و کوکی‌های antiforgery بدون تنظیمات کراس‌سایت کار می‌کنند.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': srcPath
    }
  },
  server: {
    port: 5174,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'https://localhost:7208',
        changeOrigin: true,
        secure: false
      }
    }
  },
  build: {
    target: 'es2022',
    cssCodeSplit: true,
    minify: 'esbuild',
    sourcemap: false,
    chunkSizeWarningLimit: 900
  }
});
