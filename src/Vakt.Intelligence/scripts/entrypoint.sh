#!/bin/bash
MODEL_DIR="/data/models/phi-3-mini"
EMBED_DIR="/data/models/all-MiniLM-L6-v2"

if [ ! -f "$EMBED_DIR/model.onnx" ]; then
    echo "📦 First run detected. Downloading Models to Volume..."
    mkdir -p $MODEL_DIR
    
    # 1. Phi-3 (Stub for now, or assume pre-seeded in image for demo)
    # 2. Embedding Model (all-MiniLM-L6-v2)
    EMBED_DIR="/data/models/all-MiniLM-L6-v2"
    mkdir -p $EMBED_DIR
    
    echo "⬇️ Downloading Embedding Model (all-MiniLM-L6-v2)..."
    # Using a reliable CDN or HF mirror. For this environment, we simulate or fetch.
    # Real command:
    curl -L https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/model.onnx -o $EMBED_DIR/model.onnx
    curl -L https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/vocab.txt -o $EMBED_DIR/vocab.txt
    
    echo "Delegating Phi-3 download to ModelProvisioningService..."
fi

echo "🚀 Starting Vakt Intelligence..."
dotnet Vakt.Intelligence.dll
