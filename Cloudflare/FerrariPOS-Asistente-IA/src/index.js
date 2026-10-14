const DEFAULT_MODEL = "gemini-2.5-flash-lite";
const SYSTEM_PROMPT = `Sos el asistente de FerrariPOS. Respondé en español claro, profesional y conciso. El usuario puede enviarte contexto calculado desde la base local de FerrariPOS; tratá ese contexto como fuente de datos y aclarando su período y alcance. No inventes ventas, precios, stock ni nombres de proveedores. Si faltan datos, decilo expresamente. Podés preparar un borrador de reposición con prioridades, cantidades sugeridas y costos estimados solo cuando los datos lo permitan. Nunca confirmes compras ni modifiques el inventario. Priorizá respuestas breves y útiles para reducir consumo.`;

function json(data, status, request, env) {
  const headers = { "Content-Type": "application/json; charset=utf-8", "Cache-Control": "no-store" };
  const origin = request.headers.get("Origin");
  const allowed = (env.ALLOWED_ORIGINS || "").split(",").map(x => x.trim()).filter(Boolean);
  if (origin && (allowed.includes("*") || allowed.includes(origin))) {
    headers["Access-Control-Allow-Origin"] = allowed.includes("*") ? "*" : origin;
    headers["Vary"] = "Origin";
  }
  headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization";
  headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
  return new Response(status === 204 ? null : JSON.stringify(data), { status, headers });
}

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (request.method === "OPTIONS") return json({}, 204, request, env);

    if (url.pathname === "/health" && request.method === "GET") {
      return json({
        ok: true,
        service: "FerrariPOS Asistente IA Gemini",
        provider: "Google Gemini API",
        model: env.GEMINI_MODEL || DEFAULT_MODEL,
        configured: Boolean(env.GEMINI_API_KEY)
      }, 200, request, env);
    }

    if (url.pathname !== "/api/chat") return json({ ok: false, error: "Ruta no encontrada." }, 404, request, env);
    if (request.method !== "POST") return json({ ok: false, error: "Método no permitido." }, 405, request, env);
    if (!env.GEMINI_API_KEY) {
      return json({ ok: false, error: "Falta el secreto GEMINI_API_KEY en Cloudflare. Configuralo en Workers & Pages > ferraripos-asistente-ia > Settings > Variables and Secrets." }, 503, request, env);
    }

    let body;
    try { body = await request.json(); }
    catch { return json({ ok: false, error: "JSON inválido." }, 400, request, env); }

    if (!Array.isArray(body.messages) || body.messages.length < 1 || body.messages.length > 8) {
      return json({ ok: false, error: "messages debe contener entre 1 y 8 mensajes." }, 400, request, env);
    }

    let totalChars = 0;
    const contents = [];
    for (const m of body.messages) {
      if (!m || !["user", "assistant"].includes(m.role) || typeof m.content !== "string" || m.content.length > 18000) {
        return json({ ok: false, error: "Mensaje inválido o demasiado extenso." }, 400, request, env);
      }
      totalChars += m.content.length;
      contents.push({ role: m.role === "assistant" ? "model" : "user", parts: [{ text: m.content }] });
    }
    if (totalChars > 42000) {
      return json({ ok: false, error: "El contexto total es demasiado extenso. Reducí el historial o la cantidad de datos enviados." }, 413, request, env);
    }

    try {
      const model = env.GEMINI_MODEL || DEFAULT_MODEL;
      const upstream = await fetch(`https://generativelanguage.googleapis.com/v1beta/models/${encodeURIComponent(model)}:generateContent`, {
        method: "POST",
        headers: {
          "x-goog-api-key": env.GEMINI_API_KEY,
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          systemInstruction: { parts: [{ text: SYSTEM_PROMPT }] },
          contents,
          generationConfig: {
            temperature: 0.2,
            maxOutputTokens: 900,
            thinkingConfig: { thinkingBudget: 0 }
          }
        })
      });
      const data = await upstream.json().catch(() => ({}));
      if (!upstream.ok) {
        const providerMessage = data?.error?.message || "Sin detalle del proveedor.";
        if (upstream.status === 429) {
          return json({ ok: false, error: "Se alcanzó temporalmente el límite gratuito de Gemini. Esperá a que se renueve la cuota y volvé a intentar.", status: 429, detail: providerMessage }, 429, request, env);
        }
        return json({ ok: false, error: "Gemini rechazó la solicitud.", status: upstream.status, detail: providerMessage }, 502, request, env);
      }
      const answer = data?.candidates?.[0]?.content?.parts?.map(p => p.text || "").join("").trim();
      if (!answer) {
        const reason = data?.candidates?.[0]?.finishReason;
        return json({ ok: false, error: "Gemini no devolvió texto utilizable.", detail: reason || "Respuesta vacía." }, 502, request, env);
      }
      return json({ ok: true, answer, model: model, usage: data?.usageMetadata || undefined }, 200, request, env);
    } catch {
      return json({ ok: false, error: "No se pudo conectar con la API de Gemini. Revisá la conexión y el estado del proveedor." }, 502, request, env);
    }
  }
};
