@echo off
:: FixMyCity ML Service — Windows Setup Script
:: Run this from: C:\Users\syama\Desktop\FMC_S2\FixMyCity\FixMyCity.AI\ml_service
:: Double-click OR run in Command Prompt as normal user (no admin needed)

echo.
echo ====================================================
echo  FixMyCity ML Service — Windows Setup
echo ====================================================
echo.

:: ── Step 1: Find a working Python ────────────────────────────────────────────
echo [1/6] Checking Python installation...

py -3.11 --version >nul 2>&1
if %errorlevel%==0 (
    set PYTHON=py -3.11
    echo     Found Python 3.11 via launcher.
    goto :found_python
)

py -3.12 --version >nul 2>&1
if %errorlevel%==0 (
    set PYTHON=py -3.12
    echo     Found Python 3.12 via launcher.
    goto :found_python
)

py -3.10 --version >nul 2>&1
if %errorlevel%==0 (
    set PYTHON=py -3.10
    echo     Found Python 3.10 via launcher.
    goto :found_python
)

python --version >nul 2>&1
if %errorlevel%==0 (
    set PYTHON=python
    echo     Found Python via PATH.
    goto :found_python
)

echo.
echo ERROR: No Python installation found.
echo Download Python 3.11 from https://www.python.org/downloads/
echo Make sure to check "Add Python to PATH" during install.
pause
exit /b 1

:found_python
%PYTHON% --version

:: ── Step 2: Create virtual environment ───────────────────────────────────────
echo.
echo [2/6] Creating virtual environment (.venv)...

if exist .venv (
    echo     .venv already exists, skipping creation.
) else (
    %PYTHON% -m venv .venv
    if %errorlevel% neq 0 (
        echo ERROR: Failed to create virtual environment.
        pause
        exit /b 1
    )
    echo     Virtual environment created.
)

:: Activate it
call .venv\Scripts\activate.bat
echo     Virtual environment activated.

:: ── Step 3: Upgrade pip ───────────────────────────────────────────────────────
echo.
echo [3/6] Upgrading pip...
python -m pip install --upgrade pip --quiet

:: ── Step 4: Install packages (with Windows-compatible versions) ──────────────
echo.
echo [4/6] Installing packages (this may take 5-10 minutes)...
echo     Installing binary-only packages first to avoid build errors...

:: Install numpy first (other packages depend on it)
pip install "numpy>=1.26,<2.0" --only-binary=:all: --quiet
if %errorlevel% neq 0 ( echo WARN: numpy install issue, continuing... )

:: pandas — force binary wheel (avoids the Meson/vswhere build error you hit)
pip install "pandas>=2.2,<3.0" --only-binary=:all: --quiet
if %errorlevel% neq 0 ( echo WARN: pandas install issue, continuing... )

:: torch CPU — 2.3.0 no longer exists on PyPI; install latest stable CPU build
echo     Installing PyTorch (CPU-only, ~200MB)...
pip install torch --index-url https://download.pytorch.org/whl/cpu --quiet
if %errorlevel% neq 0 ( echo WARN: torch install issue, continuing... )

:: Core web framework
:: Phase 3 (2026-05-19): python-dotenv is required — without it, the .env
:: file's HF_API_TOKEN is silently ignored at startup and every HF call
:: fails with "HF_API_TOKEN is not set". huggingface_hub is also explicit
:: here so HF API mode works even without sentence-transformers installed.
pip install "fastapi==0.111.0" "uvicorn[standard]==0.29.0" "pydantic==2.7.1" "python-multipart==0.0.9" "python-dotenv==1.0.1" "huggingface_hub>=0.23,<1.0" --quiet

:: Database
pip install "pyodbc==5.1.0" --quiet

:: ML stack
pip install "scikit-learn==1.4.2" "lightgbm==4.3.0" "joblib==1.4.2" "scipy==1.13.0" --quiet

:: NLP (sentence-transformers pulls transformers + tokenizers automatically)
pip install "sentence-transformers==2.7.0" --quiet

:: Image
pip install "Pillow==10.3.0" "pytesseract==0.3.10" --quiet

:: Keyword extraction
pip install "keybert==0.8.3" --quiet

:: Recommendations
pip install "implicit==0.7.2" --quiet

:: Forecasting
pip install "prophet==1.1.5" "matplotlib==3.8.4" --quiet

:: HTTP + chatbot + safety
pip install "httpx==0.27.0" "ollama==0.2.1" "better-profanity==0.7.0" --quiet

echo     All packages installed.

:: ── Step 5: Verify key imports ────────────────────────────────────────────────
echo.
echo [5/6] Verifying critical imports...
python -c "import fastapi, uvicorn, pandas, numpy, sklearn, torch; print('    Core imports OK')"
if %errorlevel% neq 0 (
    echo ERROR: Import check failed. Review errors above.
    pause
    exit /b 1
)

:: ── Step 6: Start the service ────────────────────────────────────────────────
echo.
echo [6/6] Starting FixMyCity AI Service...
echo     URL:    http://localhost:8001
echo     Docs:   http://localhost:8001/docs
echo     Health: http://localhost:8001/health
echo.
echo     Press Ctrl+C to stop.
echo.

:: Use "python -m uvicorn" — avoids the broken launcher .exe issue
python -m uvicorn main:app --host 0.0.0.0 --port 8001 --reload

pause
