
from fastapi import FastAPI, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
from pydantic import BaseModel
import uvicorn
import sys
import os
sys.path.append(os.path.dirname(__file__))
from playwright_analyzer import analyze_url
from llm_test_generator import generate_test_cases
from playwright_script_generator import generate_playwright_scripts
from playwright_executor import execute_playwright_scripts


app = FastAPI()
# Enable CORS for frontend (adjust origins as needed)
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:5000", "http://127.0.0.1:5000"],
    allow_credentials=True,
    allow_methods=["*"] ,
    allow_headers=["*"]
)

class AnalyzeRequest(BaseModel):
    url: str

class GenerateTestsRequest(BaseModel):
    elements: list

class GenerateScriptsRequest(BaseModel):
    test_cases: list

class ExecuteTestsRequest(BaseModel):
    scripts: list

@app.post("/aitestgen/analyze-url")
def analyze_url_endpoint(request: AnalyzeRequest):
    try:
        elements = analyze_url(request.url)
        return JSONResponse({"success": True, "elements": elements})
    except Exception as e:
        return JSONResponse({"success": False, "error": str(e)})

@app.post("/aitestgen/generate-tests")
def generate_tests_endpoint(request: GenerateTestsRequest):
    try:
        test_cases = generate_test_cases(request.elements)
        return JSONResponse({"success": True, "test_cases": test_cases})
    except Exception as e:
        return JSONResponse({"success": False, "error": str(e)})

@app.post("/aitestgen/generate-scripts")
def generate_scripts_endpoint(request: GenerateScriptsRequest):
    try:
        # Assume test_cases contains 'url' in each test case or pass url separately
        url = request.test_cases[0].get('url', '') if request.test_cases else ''
        scripts = generate_playwright_scripts(request.test_cases, url)
        return JSONResponse({"success": True, "scripts": scripts})
    except Exception as e:
        return JSONResponse({"success": False, "error": str(e)})

@app.post("/aitestgen/execute-tests")
def execute_tests_endpoint(request: ExecuteTestsRequest):
    try:
        results = execute_playwright_scripts(request.scripts)
        return JSONResponse({"success": True, "results": results})
    except Exception as e:
        return JSONResponse({"success": False, "error": str(e)})

@app.get("/results")
def get_results():
    # TODO: Return execution results
    return JSONResponse({"success": True, "results": []})

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8000)
