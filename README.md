# .NET RAG & Agentic Support Workflow Demo

**Türkçe eğitim projesi · ASP.NET Core 10 · sıfır API anahtarı**

Bir müşteri “Siparişimi iade edebilir miyim?” diye sorar. Uygulama iade politikasını arar, siparişi sorgular ve iş kuralını uygular. Web arayüzü, her adımı ve kullanılan kaynakları gösterir.

> **Kapsam:** Bu proje gerçek bir LLM veya embedding modeli kullanmaz. Sözcük tabanlı retrieval ve deterministik karar akışıyla RAG ve agentic workflow mimarisini öğretir. Üretim sistemi veya canlı müşteri hizmeti olarak kullanılmamalıdır.

## Hızlı başlangıç

Gereksinim: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet run --project SupportAgentDemo.csproj --urls http://localhost:5080
```

Tarayıcıda **http://localhost:5080** adresini açın. Örnek siparişleri seçip “Akışı çalıştır” düğmesine basın.

API örneği:

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5080/support `
  -ContentType 'application/json; charset=utf-8' `
  -Body '{"message":"Siparişimi iade edebilir miyim?","orderId":"A100"}'
```

## Senaryolar

| Sipariş | Veri | Beklenen davranış |
| --- | --- | --- |
| A100 | 5 gün önce teslim, standart ürün | İade süresi içinde |
| A200 | 20 gün önce teslim, standart ürün | Süre geçmiş; temsilciye aktar |
| A300 | 3 gün önce teslim, kişiye özel ürün | İstisna; temsilciye aktar |
| Boş | Sipariş numarası yok | Numara iste |

## Mimari

```text
Browser / POST /support
        │
        ▼
SupportWorkflow ──► KnowledgeBase.Search ──► politika kaynakları
        │
        └──────────► OrderService.Get ─────► sipariş durumu
                         │
                         ▼
                uygulama iş kuralları
                         │
                         ▼
            cevap + adımlar + kaynaklar
```

**RAG tarafı:** `KnowledgeBase.Search` ilgili politika metinlerini bulur. Gerçek RAG'deki embedding/vektör aramasının yerini burada basit sözcük eşleşmesi tutar.

**Agent tarafı:** `SupportWorkflow` hangi bilginin gerektiğini belirler ve sipariş sorgulama aracını çağırır. Bu örnek, modelin serbest araç seçimini içermez; kontrol edilebilir iş akışı mantığını gösterir.

**İş kuralları:** 14 gün ve kişiye özel ürün kontrolleri kodda uygulanır. Bulunan belge veya model yanıtı tek başına işlem yetkisi vermez.

## Komutlar

```powershell
dotnet build SupportAgentDemo.csproj
dotnet run --project SupportAgentDemo.csproj -- --self-test
```

Docker:

```bash
docker build -t support-agent-demo .
docker run --rm -p 5080:8080 support-agent-demo
```

## Gerçek RAG ve LLM entegrasyonu için yol haritası

1. Belgeleri kaynağı, sürümü, güncelleme tarihi ve erişim yetkisiyle içeri alın.
2. Belgeleri anlamlı parçalara ayırıp embedding oluşturun; vektör veya hibrit arama kullanın.
3. Retrieval sonuçları için minimum ilgililik eşiği ve erişim filtresi uygulayın.
4. Modeli niyet sınıflandırma, arama sorgusu oluşturma ve kaynaklı yanıt taslağı için ekleyin.
5. Sipariş sorgusunu kimliği doğrulanmış kullanıcı ve yetki kontrolü ile sınırlandırın.
6. Gerçek iade talebi oluşturma aracına onay, idempotency key ve audit log ekleyin.
7. Kaynak yoksa, kaynaklar çelişiyorsa veya araç hata verirse insana aktarın.
8. Doğruluk, kaynak uygunluğu, gecikme ve maliyet ölçümleriyle değerlendirme seti kurun.

## API

| Yöntem | Yol | Açıklama |
| --- | --- | --- |
| GET | `/` | Etkileşimli demo |
| GET | `/health` | Sağlık kontrolü |
| POST | `/support` | Destek iş akışını çalıştırır |

Yanıt alanları: `answer` (kullanıcı cevabı), `steps` (izlenen adımlar), `sources` (bulunan politika parçaları), `needsHuman` (insan desteği ihtiyacı).

## Kaynaklar

- [Microsoft Learn: .NET ile RAG](https://learn.microsoft.com/en-us/dotnet/ai/conceptual/rag)
- [Microsoft Learn: .NET vektör veritabanları](https://learn.microsoft.com/en-us/dotnet/ai/vector-stores/overview)
- [Microsoft Learn: ASP.NET Core Minimal API](https://learn.microsoft.com/en-us/aspnet/core/tutorials/min-web-api?view=aspnetcore-10.0)

## English

An educational ASP.NET Core demo of a retrieval step, an order lookup tool, and a controlled support workflow. No API key is required. The search is lexical and the decisions are deterministic; see the roadmap above for production RAG and LLM integration.
