# Traxon - Genel Kurallar

Bu dosya tum agent'lar icin gecerli olan genel kurallari tanimlar.

---

## Temel Prensip

Commander tek bir kullanici komutu ile baslar ve o is **%100 tamamlanana** kadar otonom calisir.
Kullanici "dur" demedikce **HICBIR agent DURMAZ**.
Hatalar agent'lar tarafindan cozulur — kullaniciya eskalasyon **YOKTUR**.
Tum agent'lar internetten arastirma yapabilir (WebSearch, WebFetch) — bilmedikleri konularda arastirip cozum bulurlar.

---

## Agent Rolleri

### Commander (Orkestrator + Kullanici Arayuzu)
- Kullanici ile iletisim kurar, talimat alir
- Architect ve Developer'i calistirir ve yonetir
- Workflow'u yonetir (Planning → Implementing → Reviewing → Merging)
- Task olusturur ve takip eder
- Hata durumlarinda karar verir ve cozum uretir
- **Kod YAZMAZ** — sadece yonetir ve koordine eder

### Architect (Mimar + Reviewer)
- Yazilim mimarisi tasarlar
- API kontrati ve veritabani sema degisiklikleri planlar
- Developer icin adim adim task breakdown olusturur
- Kod review yapar ve onaylar/reddeder
- Business kararlari verir (hangi pattern, hangi yaklasim, hangi teknoloji)
- **Kod YAZMAZ** — sadece planlar ve review eder

### Developer (Gelistirici)
- Architect'in planini uygular, kod yazar
- Feature branch olusturur, commit atar, merge eder
- dotnet build ile dogrular
- **Test YAPMAZ** — testler Tester agent'in gorevidir
- **Mimari karar VERMEZ** — Architect'in planina uyar, sapma varsa sorar

### Tester (QA Test Agent)
- TUM testlerden sorumludur: unit test, integration test, E2E test
- Playwright MCP ile UI test yapar (tum sayfalar, screenshot)
- MSSQL ile veritabani tutarliligini kontrol eder
- Sorunlari Commander'a raporlar (TestReport / BugReport)
- **Kod YAZMAZ** — sadece test eder ve raporlar

### Analyst (Strateji Analisti)
- Paper trading performansini analiz eder (PnL, win rate, Sharpe)
- WebSearch ile yeni stratejiler arastirir
- Kar optimizasyonu icin parametre onerileri sunar
- En iyi konfigurasyonu takip eder (workspace/analyst/best-config.json)
- Commander'a AnalysisReport / ConfigRecommendation gonderir
- **Kod YAZMAZ** — sadece analiz eder ve onerir

---

## Karar Yetkisi

| Karar Alani | Yetkili Agent |
|-------------|---------------|
| Hangi pattern/yaklasim kullanilacak | Architect |
| API kontrati (endpoint, model) | Architect |
| Veritabani schema tasarimi | Architect |
| Teknoloji/paket secimi | Architect |
| Kod yazmak, dosya olusturmak | Developer |
| Test yazmak ve calistirmak | Tester |
| UI test (Playwright) | Tester |
| Veritabani tutarliligi | Tester |
| Branch olusturma, commit, merge | Developer |
| Bug fix stratejisi | Architect planlar, Developer uygular, Tester dogrular |
| Strateji analizi ve optimizasyon | Analyst |
| Kar/zarar takibi | Analyst |
| Workflow yonetimi, agent koordinasyonu | Commander |
| Kullanici ile iletisim | Commander |

---

## Iletisim Kurallari

- Tum agent'lar MCP tool'lari uzerinden iletisim kurar (send_message, get_messages)
- `from` parametresinde kendi rolunu kullan (Architect, Developer, Commander, Tester, Analyst)
- Agent'lar birbirlerine direkt mesaj gonderir
- Commander tum agent'lara mesaj gonderebilir
- Acil/oncelikli durumlarda subject'e "URGENT:" ekle
- Mesajlar Turkce yazilir

---

## Kod Standartlari (Tum Projeler Icin)

### Mimari: Onion Architecture
```
Core → Domain → Application → Infrastructure → Presentation
```
- Bagimlilik yonu HER ZAMAN iceriye dogru
- Core ve Domain: SIFIR dis NuGet bagimliligi
- Application: Sadece MediatR + FluentValidation
- Infrastructure: Tum agir bagimliliklar

### Zorunlu Pattern'ler
- CQRS (MediatR Command/Query)
- Repository + UnitOfWork
- Result<T>/Error (exception firlatma, Result don)
- FluentValidation (input validation)
- Domain Events (IDomainEvent)
- Polly v8 (resilience: retry, circuit breaker, timeout)

### Proje Yapisi
- `global.json` — SDK versiyon pinleme
- `Directory.Build.props` — Ortak ayarlar (TreatWarningsAsErrors, Nullable, ImplicitUsings)
- `Directory.Packages.props` — Central Package Management
- `src/` ve `tests/` ayrimli klasor yapisi
- Her katman icin ayri test projesi

### Naming Convention
- PascalCase: siniflar, method'lar, property'ler
- camelCase: parametreler, local degiskenler
- _camelCase: private field'lar
- IPrefix: interface'ler
- Async suffix: async method'lar

### Error Handling
- Exception FIRLATMA — Result<T>/Error kullan
- Dis servislerde Polly resilience
- FluentValidation ile validation

### Veritabani
- EF Core 10 + SQL Server
- Migration-first yaklasim
- Parameterized queries
- IEntityTypeConfiguration<T> ile ayri configuration

### Logging
- Serilog structured logging
- Template-based: `Log.Information("{Action} for {Entity}", action, entity)`
- Hassas veri LOGLAMA

---

## Test Standartlari

| Test Turu | Sorumlu | Araclar | Ne Zaman |
|-----------|---------|---------|----------|
| Unit Test | Tester | xUnit + FluentAssertions + dotnet test | Her implementation sonrasi |
| Integration Test | Tester | xUnit + TestContainers | Her dis servis entegrasyonu |
| E2E / UI Test | Tester | Playwright MCP | Her deployment sonrasi |
| DB Tutarliligi | Tester | MSSQL MCP sorgulari | Her test dongusunde |
| Code Review | Architect | Read + Grep + git diff | Her implementation sonrasi |
| Strateji Analizi | Analyst | MSSQL + WebSearch | Periyodik (saatlik) |

---

## Git Kurallari

- Feature branch: `feature/{task-id}`
- Kucuk, anlamli commit'ler
- Review onaylandiktan sonra main'e merge: `git merge --no-ff`
- PR OLUSTURMA — direkt merge
- Her merge sonrasi `git push origin main` yap

---

## Guvenlik

- Secret'lar koda GOMULME — environment variable veya Secret Manager kullan
- SQL injection korunmasi: parameterized queries
- Dis repo'lara erisim YASAK
- Agent'lar sadece kendi repo'lari icinde calisir

---

## Altın Kurallar (İhlal Edilemez)

Bu kurallar 34 loop deneyimi sonucu çıkmış, hepsi pratik hatalardan doğmuştur.

### Trading Kuralları
1. **Saat filtresi YASAK** — "Şu saatler iyi, şu saatler kötü" YANLIŞ. Her saat trade edilmeli.
2. **T=0+2sn giriş** — Polymarket market açılır açılmaz gir. Geç giriş YASAK.
3. **Timeout/fake loss YASAK** — Trade'i zaman aşımıyla kapatma. Gamma API resolution bekle.
4. **Position size sabit**: `MAX(Bakiye × 2%, $1)`. Dinamik değiştirme.
5. **Direction agnostic**: Algoritma UP/DOWN ayrımı yapmaz. Edge yönüne bak.

### Sinyal Kuralları
6. **40+ sinyal/saat ideal** — Retail'de imkansız olabilir ama hedef bu. Az sinyal → küçük örneklem → yanlış karar.
7. **Agresif filtreleme YASAK** — Permutation entropy, edge threshold gibi sıkı filtreler sinyali öldürür.
8. **Kayıp önlemek için sinyal azaltma** — Filtre değil, prediction doğruluğunu artır.
9. **MCP process killing**: sadece `Traxon.CryptoTrader*` pattern kullan. `Traxon*` yazma, MCP server'ları öldürür.

### Değişiklik Kuralları
10. **Köklü değişiklik UNUTULDU** — Loop34 Binance-Polymarket arbitrage doğru yol. İyileştir, replace etme.
11. **Küçük tweak'ler 30 loop boyunca çalışmadı** — Ya matematiksel sağlam yeni yaklaşım, ya mevcut yaklaşımın parametresi.
12. **Çalışmayan değişikliği geri al** — Sonraki loop'ta bırak, 2 loop taşıma.
13. **Başarısız loop'ta sıfırdan araştır** — PnL -$10+ → derin araştırma, yeni yaklaşım.

### Loop Kuralları
14. **Loop süresi 8 saat** — 4 saat yetersiz (küçük örneklem), 12 saat gereksiz.
15. **Her loop raporu yazılır** — `LoopTest/Loop{N}/report.md`. Kârlı loop'un başına kazanç bilgisi.
16. **DB temizliği loop başında** — Settings + Migrations hariç her şey silinir.
17. **LivePoly branch + 1 commit/loop** — Feature branch oluşturma.
18. **Erken durdurma sadece fatal durumda** — bakiye < $1 VE açık trade 0. Düşük WR'da durdurma.

### Geliştirme Kuralları
19. **Commander kod yazmaz** — Her şeyi alt agent'lara yaptırır.
20. **Git push LivePoly'ye** — Her commit sonrası push.
21. **Build 0 hata zorunlu** — Warning bile kabul edilmez (loop33'te çalıştı).
22. **Algoritma değişikliği → doküman güncelleme zorunlu** — Her kod değişikliği sonrası tüm UI pages (DocsPage, LivePolyTransition, PyClobClient, LoopsPage) ve md dosyaları (README, CLAUDE, RULES, docs/algorithm-*.md) anında güncellenir. Eski algoritma referansları SİLİNİR, yenisi eklenir. Alt agent'lar paralel kullanılır, hızlı yapılır.
