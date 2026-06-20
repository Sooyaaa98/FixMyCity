FROM python:3.11-slim

# System dependencies for pyodbc, Tesseract, and ML libraries
RUN apt-get update && apt-get install -y \
    curl gcc g++ unixodbc-dev \
    tesseract-ocr tesseract-ocr-kan tesseract-ocr-hin \
    libgomp1 \
    && rm -rf /var/lib/apt/lists/*

# Install Microsoft ODBC driver 17 for SQL Server
RUN curl https://packages.microsoft.com/keys/microsoft.asc | apt-key add - && \
    curl https://packages.microsoft.com/config/debian/11/prod.list \
         > /etc/apt/sources.list.d/mssql-release.list && \
    apt-get update && ACCEPT_EULA=Y apt-get install -y msodbcsql17 && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY requirements.txt .

# Install CPU-only torch first to avoid pulling CUDA packages
RUN pip install --no-cache-dir torch==2.3.0 --index-url https://download.pytorch.org/whl/cpu
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

# Pre-download the sentence-transformers model into the image
RUN python -c "from sentence_transformers import SentenceTransformer; SentenceTransformer('all-MiniLM-L6-v2')"

EXPOSE 8001

CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8001"]
