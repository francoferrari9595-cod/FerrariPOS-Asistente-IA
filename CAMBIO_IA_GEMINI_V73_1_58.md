# FerrariPOS — Migración de asistente IA de Groq a Gemini

Base de trabajo: ZIP FerrariPOS V73.1.58 IA Groq autoconfigurada recibido en esta conversación. No se recuperaron archivos de versiones anteriores ni se modificaron funciones del POS ajenas al asistente.

## Cambios aplicados

- Worker Cloudflare cambia del endpoint de Groq a Google Gemini API.
- Modelo configurado: `gemini-2.5-flash-lite`.
- Se mantiene la ruta `/api/chat` y el contrato `{ ok, answer, model }`, así que se conserva la URL del Worker usada por FerrariPOS.
- Nuevo secreto de Cloudflare: `GEMINI_API_KEY`.
- `/health` informa proveedor, modelo y si el secreto está configurado.
- Respuesta limitada a 900 tokens y pensamiento interno desactivado para reducir consumo.
- El formulario conserva el contexto de ventas/productos/proveedores, pero ya no reenvía todo el contexto local en cada mensaje del historial.
- Historial enviado limitado a 6 intervenciones; lista de productos limitada a 40.
- Mensajes de error específicos para límites de cuota de Gemini.

## Antes de probar

1. Crear una API key en https://aistudio.google.com/apikey.
2. En Cloudflare Workers & Pages → `ferraripos-asistente-ia` → Settings → Variables and Secrets, crear el secreto `GEMINI_API_KEY`.
3. Publicar el proyecto para desplegar el Worker nuevo.
4. Confirmar que GitHub Actions finalice correctamente.
5. Verificar `/health` y realizar una consulta real desde FerrariPOS.

No se guardó ninguna API key dentro del ZIP. El secreto anterior de Groq ya no se utiliza por este Worker.
