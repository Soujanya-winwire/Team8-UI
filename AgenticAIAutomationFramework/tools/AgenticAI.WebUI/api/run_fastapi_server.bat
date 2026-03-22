@echo off
REM Run FastAPI backend for AI Test Generator
cd /d %~dp0
python -m uvicorn aitestgen_api:app --host 0.0.0.0 --port 5000 --reload
pause
