@echo off
title Traxon Analyst Monitor
echo === Traxon Analyst Agent Log Monitor ===
echo Watching: workspace\logs\analyst-latest.log
echo.
tail -f workspace\logs\analyst-latest.log
