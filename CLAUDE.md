# Traxon - Multi-Agent Orchestration System

Uc Claude Code agent'inin (Commander + Architect + Developer) MCP server uzerinden haberleserek otonom olarak .NET projesi gelistirdigi orkestrasyon sistemi.

## Mimari
- **Commander** — Interaktif orkestrator. Workflow'u yonetir, agent'lari calistirir, kullanici ile iletisim kurar.
- **Architect** — Headless. Plan yapar, API tasarlar, kod review eder.
- **Developer** — Headless. Kod yazar, test yazar (unit + E2E), merge eder.

## Proje Yapisi
- `src/ClaudeOrchestrator/Traxon.Mcp/` — MCP Server (agent'lar arasi mesajlasma)
- `src/ClaudeOrchestrator/Traxon.Contracts/` — Shared tipler
- `agents/commander/CLAUDE.md` — Commander talimatlari (orkestrasyon beyni)
- `agents/architect/CLAUDE.md` — Architect talimatlari
- `agents/developer/CLAUDE.md` — Developer talimatlari
- `config/mcp.json` — MCP konfigurasyonu (traxon + playwright + mssql)
- `workspace/` — Runtime veri dizini

## Baslat
```bash
start-commander.bat   # Commander'i baslat (tek giris noktasi)
```

## Nasil Calisir
1. Commander baslar, bekleyen task'lari kontrol eder
2. Task varsa: Architect'i planla → Developer'a uygulat → Architect'e review ettir → merge
3. Kullanici istediginde Commander'a yazar: "dur", "durum", veya yeni talimat
4. Commander otonom calisir, kullanici mudahalesi opsiyoneldir

## MCP Server'lar
- **traxon** — Agent mesajlasma + task yonetimi
- **playwright** — E2E browser testi (Developer kullanir)
- **mssql** — SQL Server veritabani erisimi

## Kurallar
Tum agent rolleri, kod standartlari, karar yetkileri ve test sorumlulugu RULES.md dosyasinda tanimlidir.

## Guvenlik
- Agent'lar sadece local git islemleri yapabilir (push/remote/gh yasak)
- `.claude/settings.local.json` deny kurallari aktif
