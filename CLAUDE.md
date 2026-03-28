# Traxon - Multi-Agent Orchestration System

Bes Claude Code agent'inin (Commander + Architect + Developer + Tester + Analyst) MCP server uzerinden haberleserek otonom olarak .NET projesi gelistirdigi orkestrasyon sistemi. Tum agent'lar **Opus 4.6** modeli kullanir.

## Mimari
- **Commander** — Interaktif orkestrator. Workflow'u yonetir, agent'lari calistirir, kullanici ile iletisim kurar. Kod YAZMAZ.
- **Architect** — Headless. Plan yapar, API tasarlar, kod review eder. Kod YAZMAZ.
- **Developer** — Headless. Kod yazar, build eder, commit atar. Test YAPMAZ.
- **Tester** — Headless. UI test (Playwright), DB tutarlilik, unit test, regression. Kod YAZMAZ.
- **Analyst** — Headless. Strateji analizi, kar optimizasyonu, WebSearch arastirma. Kod YAZMAZ.

## Proje Yapisi
- `src/ClaudeOrchestrator/Traxon.Mcp/` — MCP Server (agent'lar arasi mesajlasma)
- `src/ClaudeOrchestrator/Traxon.Contracts/` — Shared tipler (AgentRole, MessageType)
- `src/CryptoTrader/` — Ana uygulama (Onion Architecture)
- `agents/commander/CLAUDE.md` — Commander talimatlari (orkestrasyon beyni)
- `agents/architect/CLAUDE.md` — Architect talimatlari
- `agents/developer/CLAUDE.md` — Developer talimatlari
- `agents/tester/CLAUDE.md` — Tester talimatlari
- `agents/analyst/CLAUDE.md` — Analyst talimatlari
- `config/mcp.json` — MCP konfigurasyonu (traxon + playwright + mssql)
- `workspace/` — Runtime veri dizini
- `workspace/logs/` — Agent log dosyalari
- `workspace/analyst/` — Analyst config ve raporlari
- `scripts/` — Agent baslat/izle scriptleri

## Baslat
```bash
start-commander.bat   # Commander'i baslat (tek giris noktasi)
```

## Nasil Calisir
1. Commander baslar, bekleyen task'lari kontrol eder
2. Task varsa: Architect planlar → Developer uygular → Tester test eder → Architect review eder → merge
3. Merge sonrasi: Analyst performans analiz eder → oneri varsa → Developer uygular → dongu
4. Kullanici istediginde Commander'a yazar: "dur", "durum", veya yeni talimat
5. Commander otonom calisir, kullanici mudahalesi opsiyoneldir

## MCP Server'lar
- **traxon** — Agent mesajlasma + task yonetimi
- **playwright** — E2E browser testi (Tester kullanir)
- **mssql** — SQL Server veritabani erisimi (Tester + Analyst kullanir)

## Kurallar
Tum agent rolleri, kod standartlari, karar yetkileri ve test sorumlulugu RULES.md dosyasinda tanimlidir.

## Guvenlik
- Agent'lar local git islemleri + push yapar
- `.claude/settings.local.json` deny kurallari aktif
