# Integración actual de IA FerrariPOS — Gemini

La integración activa de esta entrega usa Google Gemini API a través del Worker de Cloudflare `ferraripos-asistente-ia`.

- Ruta mantenida: `/api/chat`
- Modelo por defecto: `gemini-2.5-flash-lite`
- Secreto requerido en Cloudflare: `GEMINI_API_KEY`
- URL Worker mantenida: `https://ferraripos-asistente-ia.francoferrari9595.workers.dev`

Para crear la API key, revisar los límites y desplegar, seguir `Cloudflare/FerrariPOS-Asistente-IA/README.md`.

Este archivo conserva su nombre histórico para no cambiar rutas/documentación antigua que lo referencien; el proveedor actual de esta versión es Gemini, no Groq.
