using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FerrarisPOS.Data;

namespace FerrarisPOS.Forms;

public sealed class GroqAssistantForm : Form
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(90) };
    private const string WorkerUrl = "https://ferraripos-asistente-ia.francoferrari9595.workers.dev";
    private readonly RichTextBox chat = new() { Dock = DockStyle.Fill, ReadOnly = true, DetectUrls = true };
    private readonly TextBox prompt = new() { Dock = DockStyle.Fill, Multiline = true, Height = 58, PlaceholderText = "Preguntá por ventas, stock, productos o pedí un borrador de reposición..." };
    private readonly Button send = new() { Text = "ENVIAR", Width = 110, Height = 38 };
    private readonly Button analyze = new() { Text = "ANALIZAR VENTAS Y SUGERIR PEDIDO", Width = 270, Height = 38 };
    private readonly List<(string role, string content)> history = new();

    public GroqAssistantForm()
    {
        Text = "FerrariPOS · Asistente IA Gemini";
        Width = 1050; Height = 740; MinimumSize = new Size(760, 540);
        BackColor = Color.FromArgb(15, 20, 30); ForeColor = Color.White;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(15, 20, 30) };
        var title = new Label { Text = "FERRARI POS · ASISTENTE IA GEMINI", Dock = DockStyle.Top, Height = 30, ForeColor = Color.Aqua, Font = new Font("Segoe UI", 12, FontStyle.Bold) };
        var subtitle = new Label { Text = "Conexión Cloudflare configurada automáticamente", Dock = DockStyle.Top, Height = 24, ForeColor = Color.Silver, Font = new Font("Segoe UI", 9) };
        header.Controls.Add(subtitle); header.Controls.Add(title);
        root.Controls.Add(header, 0, 0);

        chat.BackColor = Color.FromArgb(8, 12, 20); chat.ForeColor = Color.Gainsboro; chat.Font = new Font("Consolas", 10);
        root.Controls.Add(chat, 0, 1);
        prompt.BackColor = Color.FromArgb(28, 34, 46); prompt.ForeColor = Color.White; prompt.Font = new Font("Segoe UI", 10);
        root.Controls.Add(prompt, 0, 2);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        send.BackColor = Color.Teal; send.ForeColor = Color.White; send.FlatStyle = FlatStyle.Flat;
        analyze.BackColor = Color.FromArgb(38, 82, 58); analyze.ForeColor = Color.White; analyze.FlatStyle = FlatStyle.Flat;
        send.Click += async (_, _) => await SendAsync(false);
        analyze.Click += async (_, _) => await SendAsync(true);
        prompt.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter && !e.Shift) { e.SuppressKeyPress = true; await SendAsync(false); } };
        actions.Controls.Add(send); actions.Controls.Add(analyze); root.Controls.Add(actions, 0, 3);
        AddLine("SISTEMA", "Asistente Gemini listo. La conexión con Cloudflare está configurada automáticamente. Escribí tu consulta para comenzar.");
    }

    private async Task SendAsync(bool analyzeData)
    {
        string question = analyzeData
            ? "Analizá ventas de 7 y 30 días, detectá productos con stock bajo y rotación lenta. Prepará un BORRADOR de reposición con prioridades, cantidades sugeridas y costo estimado cuando haya datos suficientes. No confirmes compras."
            : prompt.Text.Trim();
        if (string.IsNullOrWhiteSpace(question)) return;
        prompt.Clear(); AddLine("VOS", question);
        send.Enabled = analyze.Enabled = false; Cursor = Cursors.WaitCursor;
        try
        {
            string context = BuildContext();
            // Guardar preguntas sin el contexto local para no reenviar todos los datos en cada turno.
            history.Add(("user", question));
            while (history.Count > 6) history.RemoveAt(0);
            var outbound = history.Select((m, i) => new
            {
                role = m.role,
                content = i == history.Count - 1 && m.role == "user"
                    ? m.content + "\n\nCONTEXTO REAL DE FERRARIPOS (solo para esta consulta):\n" + context
                    : m.content
            }).ToArray();
            using var req = new HttpRequestMessage(HttpMethod.Post, WorkerUrl + "/api/chat");
            req.Content = new StringContent(JsonSerializer.Serialize(new { messages = outbound }), Encoding.UTF8, "application/json");
            using var resp = await Http.SendAsync(req);
            string raw = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(raw);
            if (!resp.IsSuccessStatusCode)
            {
                string err = doc.RootElement.TryGetProperty("error", out var ev) ? ev.GetString() ?? raw : raw;
                if (doc.RootElement.TryGetProperty("detail", out var dv)) err += "\n" + dv.GetString();
                AddLine("ERROR", $"HTTP {(int)resp.StatusCode}: {err}");
                history.RemoveAt(history.Count - 1); return;
            }
            if (!doc.RootElement.TryGetProperty("answer", out var answerNode))
            { AddLine("ERROR", "La respuesta no incluye el campo answer."); history.RemoveAt(history.Count - 1); return; }
            string answer = answerNode.GetString() ?? "(sin respuesta)";
            history.Add(("assistant", answer)); AddLine("GEMINI", answer);
        }
        catch (Exception ex)
        {
            AddLine("ERROR", "No se pudo conectar. Revisá la conexión a Internet y el despliegue del Worker de Cloudflare.\n" + ex.Message);
            if (history.Count > 0 && history[^1].role == "user") history.RemoveAt(history.Count - 1);
        }
        finally { send.Enabled = analyze.Enabled = true; Cursor = Cursors.Default; prompt.Focus(); }
    }

    private static string BuildContext()
    {
        var sb = new StringBuilder();
        using var cn = Database.Open();
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT COALESCE(SUM(CASE WHEN date(created_at)=date('now','localtime') THEN total ELSE 0 END),0),
                       COALESCE(SUM(CASE WHEN datetime(created_at)>=datetime('now','localtime','-7 days') THEN total ELSE 0 END),0),
                       COALESCE(SUM(CASE WHEN datetime(created_at)>=datetime('now','localtime','-30 days') THEN total ELSE 0 END),0),
                       COALESCE(SUM(CASE WHEN datetime(created_at)>=datetime('now','localtime','-7 days') THEN 1 ELSE 0 END),0),
                       COALESCE(SUM(CASE WHEN datetime(created_at)>=datetime('now','localtime','-30 days') THEN 1 ELSE 0 END),0)
                FROM sales WHERE status='COMPLETED'
                """;
            using var r = cmd.ExecuteReader();
            if (r.Read()) sb.AppendLine($"VENTAS ARS: hoy={r.GetDouble(0):N2}; últimos 7 días={r.GetDouble(1):N2}; últimos 30 días={r.GetDouble(2):N2}; tickets 7 días={r.GetInt32(3)}; tickets 30 días={r.GetInt32(4)}.");
        }
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT p.description,p.category,p.stock,p.min_stock,p.cost_price,p.sale_price,
                       COALESCE(SUM(CASE WHEN datetime(s.created_at)>=datetime('now','localtime','-30 days') THEN si.quantity ELSE 0 END),0)
                FROM products p
                LEFT JOIN sale_items si ON si.product_id=p.id
                LEFT JOIN sales s ON s.id=si.sale_id AND s.status='COMPLETED'
                WHERE p.active=1 AND p.barcode <> '__COMUN__'
                GROUP BY p.id
                ORDER BY CASE WHEN p.stock<=p.min_stock THEN 0 ELSE 1 END, 7 DESC, p.description
                LIMIT 40
                """;
            using var r = cmd.ExecuteReader();
            sb.AppendLine("PRODUCTOS (máximo 100; vendidos en últimos 30 días):");
            while (r.Read()) sb.AppendLine($"- {r.GetString(0)} | categoría={r.GetString(1)} | stock={r.GetDouble(2):N2} | mínimo={r.GetDouble(3):N2} | costo={r.GetDouble(4):N2} | precio={r.GetDouble(5):N2} | vendidos30d={r.GetDouble(6):N2}");
        }
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM suppliers WHERE active=1 ORDER BY name LIMIT 50";
            using var r = cmd.ExecuteReader(); sb.AppendLine("PROVEEDORES ACTIVOS:");
            while (r.Read()) sb.AppendLine("- " + r.GetString(0));
        }
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM customers WHERE active=1";
            sb.AppendLine("CANTIDAD DE CLIENTES ACTIVOS (sin datos personales): " + Convert.ToInt32(cmd.ExecuteScalar() ?? 0));
        }
        sb.AppendLine("Datos tomados de la base local de este equipo; otros dispositivos se incluyen solo si ya sincronizaron sus datos aquí. No asumir sincronización total.");
        return sb.ToString();
    }

    private void AddLine(string who, string text)
    {
        chat.SelectionStart = chat.TextLength;
        chat.SelectionColor = who == "GEMINI" ? Color.Aqua : who == "ERROR" ? Color.Salmon : who == "SISTEMA" ? Color.Silver : Color.LightGreen;
        chat.AppendText($"[{who}]\n{text}\n\n");
        chat.SelectionColor = chat.ForeColor; chat.SelectionStart = chat.TextLength; chat.ScrollToCaret();
    }
}
