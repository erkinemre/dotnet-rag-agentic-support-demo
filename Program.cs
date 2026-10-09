using System.Globalization;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<KnowledgeBase>();
builder.Services.AddSingleton<OrderService>();
builder.Services.AddSingleton<SupportWorkflow>();
if (args.Contains("--self-test"))
{
    SelfTest.Run(new SupportWorkflow(new KnowledgeBase(), new OrderService()));
    return;
}
var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/support", (SupportRequest request, SupportWorkflow workflow) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest(new { error = "message gerekli" });
    return Results.Ok(workflow.Run(request));
});
app.Run();

static class SelfTest
{
    public static void Run(SupportWorkflow workflow)
    {
        Check("A100", "süresi içinde", false);
        Check("A200", "14 günden fazla", true);
        Check("A300", "kişiye özel", true);
        var missing = workflow.Run(new SupportRequest("İade edebilir miyim?", null));
        if (!missing.Answer.Contains("sipariş numaranızı") || missing.NeedsHuman)
            throw new Exception("Sipariş numarası senaryosu başarısız.");
        Console.WriteLine("4 senaryo başarılı.");

        void Check(string id, string expected, bool needsHuman)
        {
            var result = workflow.Run(new SupportRequest("Siparişimi iade edebilir miyim?", id));
            if (!result.Answer.Contains(expected) || result.NeedsHuman != needsHuman)
                throw new Exception($"{id} senaryosu başarısız: {result.Answer}");
        }
    }
}

record SupportRequest(string Message, string? OrderId);
record Evidence(string Id, string Text, int Score);
record SupportResponse(string Answer, string[] Steps, Evidence[] Sources, bool NeedsHuman);
record Document(string Id, string Text);

// Eğitim amaçlı sözcük eşleşmesi. Üretimde embedding + vektör araması ile değiştirilebilir.
sealed class KnowledgeBase
{
    private readonly Document[] _documents =
    [
        new("POL-RET-01", "İade politikası: Teslimden itibaren 14 gün içinde iade talebi açılabilir."),
        new("POL-RET-02", "İade politikası: Kişiye özel üretilen ürünler iade edilemez."),
        new("POL-SHIP-01", "Kargo politikası: Sipariş gönderildikten sonra takip numarası paylaşılır.")
    ];

    public Evidence[] Search(string question, int topK = 2)
    {
        var terms = Tokens(question).Where(t => t.Length >= 3).ToHashSet();
        return _documents
            .Select(d => new Evidence(d.Id, d.Text, Tokens(d.Text).Count(terms.Contains)))
            .Where(e => e.Score > 0)
            .OrderByDescending(e => e.Score)
            .Take(topK)
            .ToArray();
    }

    private static IEnumerable<string> Tokens(string text) =>
        Regex.Matches(text.ToLower(CultureInfo.GetCultureInfo("tr-TR")), @"[\p{L}\p{N}]+")
            .Select(m => m.Value);
}

sealed class OrderService
{
    private readonly Dictionary<string, (string Status, int DaysSinceDelivery, bool CustomMade)> _orders = new()
    {
        ["A100"] = ("Teslim edildi", 5, false),
        ["A200"] = ("Teslim edildi", 20, false),
        ["A300"] = ("Teslim edildi", 3, true)
    };

    public (string Status, int DaysSinceDelivery, bool CustomMade)? Get(string id) =>
        _orders.TryGetValue(id, out var order) ? order : null;
}

sealed class SupportWorkflow(KnowledgeBase knowledge, OrderService orders)
{
    public SupportResponse Run(SupportRequest request)
    {
        var steps = new List<string> { "1. Kullanıcı niyeti belirlendi" };
        var isReturn = CultureInfo.GetCultureInfo("tr-TR").CompareInfo
            .IndexOf(request.Message, "iade", CompareOptions.IgnoreCase) >= 0;
        var sources = knowledge.Search(request.Message);
        steps.Add($"2. Bilgi tabanında arama yapıldı; {sources.Length} kaynak bulundu");

        if (!isReturn)
            return new("Bu örnek yalnızca iade senaryosunu işler; temsilciye aktarılıyor.",
                steps.ToArray(), sources, true);

        if (sources.All(s => !s.Id.StartsWith("POL-RET")))
            return new("İade politikasına ait güvenilir kaynak bulunamadı; temsilciye aktarılıyor.",
                steps.ToArray(), sources, true);

        if (string.IsNullOrWhiteSpace(request.OrderId))
            return new("İade uygunluğunu kontrol etmek için sipariş numaranızı paylaşın.",
                steps.ToArray(), sources, false);

        var order = orders.Get(request.OrderId);
        steps.Add("3. Sipariş sorgulama aracı çağrıldı");
        if (order is null)
            return new("Sipariş bulunamadı; numarayı kontrol edin veya temsilciye başvurun.",
                steps.ToArray(), sources, true);

        // İş kuralı kodda uygulanır; arama sonucu tek başına yetki vermez.
        var relevant = sources.Where(s => s.Id.StartsWith("POL-RET")).ToArray();
        if (order.Value.CustomMade)
            return new("Bu sipariş kişiye özel üretildiği için otomatik iade uygunluğu veremiyorum.",
                steps.ToArray(), relevant, true);
        if (order.Value.DaysSinceDelivery > 14)
            return new("Teslimden beri 14 günden fazla geçtiği için otomatik iade uygunluğu veremiyorum.",
                steps.ToArray(), relevant, true);

        steps.Add("4. İade uygunluğu hesaplandı");
        return new($"{request.OrderId} siparişi teslimden {order.Value.DaysSinceDelivery} gün sonra iade süresi içinde görünüyor. İade talebi açabilirsiniz. Bu işlem talep oluşturmaz.",
            steps.ToArray(), relevant, false);
    }
}
