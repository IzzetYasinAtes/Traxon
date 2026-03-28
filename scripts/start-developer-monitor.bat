@echo off
title Traxon Developer Monitor
echo === Traxon Developer Agent Log Monitor ===
echo Watching: workspace\logs\developer-latest.log
echo.
tail -f workspace\logs\developer-latest.log
