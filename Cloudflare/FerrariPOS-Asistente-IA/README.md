# FerrariPOS Asistente IA — Cloudflare Worker + Google Gemini

Esta versión reemplaza Groq por la API oficial de Google Gemini. Mantiene la ruta `/api/chat` y el formato de respuesta que consume FerrariPOS Windows, por lo que no hace falta cambiar la URL del Worker en la aplicación.

## Modelo y control de consumo

- Modelo por defecto: `gemini-2.5-flash-lite`.
- Respuesta limitada a 900 tokens.
- Presupuesto de pensamiento configurado en 0 para evitar gasto innecesario en razonamiento interno.
- FerrariPOS envía solo las últimas 6 intervenciones; el contexto local se adjunta únicamente a la consulta actual.
- El contexto se limita a 40 productos y el Worker limita la solicitud completa.

Las cuotas gratuitas exactas dependen del proyecto y pueden cambiar. Verifica las cuotas activas en https://aistudio.google.com/ y los límites oficiales en https://ai.google.dev/gemini-api/docs/rate-limits. No hay una cantidad fija de preguntas garantizada para todas las cuentas.

## 1. Crear la API key

1. Entrá en https://aistudio.google.com/apikey
2. Creá una API key en un proyecto de Google.
3. No la pegues en el código, el ZIP, GitHub ni en el ejecutable.

## 2. Configurar el secreto en Cloudflare

En Cloudflare → Workers & Pages → `ferraripos-asistente-ia` → Settings → Variables and Secrets, creá un **Secret** llamado exactamente:

`GEMINI_API_KEY`

Pegá el valor de la API key de Google y guardá. No es necesario conservar `FERRARIPOS_GROQ_ASSISTANT_KEY` para esta versión. Después desplegá el Worker actualizado desde GitHub Actions.

Alternativa con Wrangler desde la carpeta de este Worker:

`npx wrangler secret put GEMINI_API_KEY`

Pegá la clave cuando Wrangler lo solicite.

## 3. Desplegar desde GitHub

El repositorio principal debe conservar los secretos de Actions `CLOUDFLARE_API_TOKEN` y `CLOUDFLARE_ACCOUNT_ID`, que permiten publicar el Worker. La API key de Gemini es un secreto de ejecución de Cloudflare y **no** debe agregarse a GitHub Actions.

Subí el proyecto a la rama `main` o `master` y comprobá que el trabajo **Validar y desplegar Worker Gemini en Cloudflare** termine correctamente.

## 4. Verificar

Abrí:

`https://ferraripos-asistente-ia.francoferrari9595.workers.dev/health`

Debe devolver `ok: true`, `provider: "Google Gemini API"` y `configured: true`.

Después abrí FerrariPOS y enviá una pregunta real al asistente. La prueba `/health` solo confirma que el secreto está presente; no prueba por sí sola que Gemini haya respondido.

## Seguridad y privacidad

El Worker no accede directamente a SQLite. FerrariPOS prepara un resumen local de ventas, productos y proveedores y lo envía a Gemini. No envíes datos personales que no sean necesarios. La disponibilidad del nivel gratuito, los límites y las condiciones de tratamiento de datos dependen de Google y del proyecto utilizado. En el nivel gratuito, Google indica que los datos pueden utilizarse para mejorar sus productos; revisá las condiciones vigentes antes de enviar información comercial sensible.

El endpoint público puede recibir solicitudes de terceros. Para uso personal, restringí el acceso mediante controles de Cloudflare/rate limiting antes de dejarlo expuesto. Nunca incluyas la API key en el ejecutable.
