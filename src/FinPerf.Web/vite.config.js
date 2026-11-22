import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// El navegador llama a /api en el mismo origen; Vite reenvía esas peticiones a ASP.NET Core.
const proxy = { '/api': 'http://127.0.0.1:5080' }

export default defineConfig({
  plugins: [react()],
  server: { host: '127.0.0.1', port: 5173, strictPort: true, proxy },
  preview: { host: '127.0.0.1', proxy },
})
