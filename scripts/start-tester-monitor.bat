@echo off
title Traxon Tester Monitor
echo === Traxon Tester Agent Log Monitor ===
echo Watching: workspace\logs\tester-latest.log
echo.
tail -f workspace\logs\tester-latest.log
