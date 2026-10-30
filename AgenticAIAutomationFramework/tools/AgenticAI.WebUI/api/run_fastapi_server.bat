@echo off
REM Run FastAPI backend for AI Test Generator
REM Must stay on port 8000 to match the fetch() calls in wwwroot/app.js and
REM to avoid colliding with the .NET Web UI, which already listens on 5000.
cd /d %~dp0
py -m uvicorn aitestgen_api:app --host 0.0.0.0 --port 8000 --reload
pause
