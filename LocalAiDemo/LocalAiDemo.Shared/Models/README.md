# all-MiniLM-L6-v2 Model Setup

This directory should contain the ONNX model files for the all-MiniLM-L6-v2 sentence transformer.

## Required Files

To use the local embedding service, you need to download the following files from Hugging Face:

### 1. ONNX Model File
- **File**: `all-MiniLM-L6-v2.onnx`
- **Source**: https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/tree/main/onnx
- **Download**: https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx
- **Rename to**: `all-MiniLM-L6-v2.onnx`

### 2. Tokenizer File (Optional but Recommended)
- **File**: `tokenizer.json`
- **Source**: https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/blob/main/tokenizer.json
- **Download**: https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/tokenizer.json

## Download Instructions

### Option 1: Manual Download
1. Go to the Hugging Face model page: https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2
2. Navigate to the `onnx` folder
3. Download `model.onnx` and rename it to `all-MiniLM-L6-v2.onnx`
4. Download `tokenizer.json` from the root folder

### Option 2: Using wget/curl (if available)
```bash
# Download ONNX model
wget https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx -O all-MiniLM-L6-v2.onnx

# Download tokenizer
wget https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/tokenizer.json -O tokenizer.json
```

### Option 3: Using PowerShell
```powershell
# Download ONNX model
Invoke-WebRequest -Uri "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx" -OutFile "all-MiniLM-L6-v2.onnx"

# Download tokenizer
Invoke-WebRequest -Uri "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/tokenizer.json" -OutFile "tokenizer.json"
```

## File Structure
After downloading, your Models folder should look like this:
```
Models/
├── README.md (this file)
├── all-MiniLM-L6-v2.onnx
└── tokenizer.json (optional)
```

## Vector Database Changes

The SQLite vector search has been updated to support the all-MiniLM-L6-v2 model:

### Updates Made:
1. **Vector Dimensions**: Changed from 128 to 384 dimensions to match all-MiniLM-L6-v2
2. **Chat Segments**: Added support for searching chat segments instead of just chats
3. **Dual Vector Tables**: 
   - `chat_vectors` - For legacy chat-level embeddings
   - `chat_segment_vectors` - For segment-level embeddings (recommended approach)

### Vector Tables:
```sql
-- Chat-level vectors (legacy)
CREATE VIRTUAL TABLE chat_vectors USING vec0(
    embedding_vector FLOAT[384],
    chat_id INTEGER UNINDEXED
);

-- Segment-level vectors (recommended)
CREATE VIRTUAL TABLE chat_segment_vectors USING vec0(
    embedding_vector FLOAT[384],
    segment_id INTEGER UNINDEXED
);
```

## Model Information
- **Model**: all-MiniLM-L6-v2
- **Output Dimensions**: 384 (updated from 128)
- **Max Sequence Length**: 512 tokens
- **Use Case**: Sentence embeddings for semantic similarity
- **Search Target**: Chat segments for better thematic search

## Troubleshooting
- Ensure the files are named exactly as specified
- Check that the files are not corrupted (the ONNX model should be around 90MB)
- Verify that the Models directory is in the correct location relative to your application
- If you get dimension mismatch errors, ensure you're using the 384-dimensional model
- The application will throw exceptions if model files are missing (no fallback embeddings)
