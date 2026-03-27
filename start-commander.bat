@echo off
echo ============================================
echo   TRAXON - Commander Orkestrator
echo ============================================
echo.
echo Commander baslatiliyor...
echo Komutlar: "durum", "dur", "devam", veya dogal dilde talimat ver.
echo.
claude --mcp-config config/mcp.json ^
  --append-system-prompt-file agents/commander/CLAUDE.md ^
  --permission-mode bypassPermissions
