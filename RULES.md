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

### Developer (Gelistirici + Tester)
- Architect'in planini uygular, kod yazar
- TUM testlerden sorumludur: unit test, integration test, E2E test
- Feature branch olusturur, commit atar, merge eder
- dotnet build ve dotnet test ile dogrular
- Playwright ile E2E test yapar (gerektiginde)
- **Mimari karar VERMEZ** — Architect'in planina uyar, sapma varsa sorar

---

## Karar Yetkisi

| Karar Alani | Yetkili Agent |
|-------------|---------------|
| Hangi pattern/yaklasim kullanilacak | Architect |
| API kontrati (endpoint, model) | Architect |
| Veritabani schema tasarimi | Architect |
| Teknoloji/paket secimi | Architect |
| Kod yazmak, dosya olusturmak | Developer |
| Test yazmak ve calistirmak | Developer |
| Branch olusturma, commit, merge | Developer |
| Bug fix stratejisi | Architect planlar, Developer uygular |
| Workflow yonetimi, agent koordinasyonu | Commander |
| Kullanici ile iletisim | Commander |

---

## Iletisim Kurallari

- Tum agent'lar MCP tool'lari uzerinden iletisim kurar (send_message, get_messages)
- `from` parametresinde kendi rolunu kullan (Architect, Developer, Commander)
- Architect ve Developer birbirlerine direkt mesaj gonderir
- Commander her iki agent'a da mesaj gonderebilir
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
| Unit Test | Developer | xUnit + FluentAssertions | Her public metod |
| Integration Test | Developer | xUnit + TestContainers | Her dis servis entegrasyonu |
| E2E Test | Developer | Playwright MCP | Web uygulamasi varsa |
| Code Review | Architect | Read + Grep + git diff | Her implementation sonrasi |

---

## Git Kurallari

- Feature branch: `feature/{task-id}`
- Kucuk, anlamli commit'ler
- Review onaylandiktan sonra main'e merge: `git merge --no-ff`
- PR OLUSTURMA — direkt merge
- `git push`, `git remote`, `gh` YASAK (sadece local)

---

## Guvenlik

- Secret'lar koda GOMULME — environment variable veya Secret Manager kullan
- SQL injection korunmasi: parameterized queries
- Dis repo'lara erisim YASAK
- Agent'lar sadece kendi repo'lari icinde calisir
