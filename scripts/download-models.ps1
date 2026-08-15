<#
Telecharge les poids ONNX et fichiers de tokenisation necessaires au pipeline RAG
(STACK_TECHNIQUE.md #4). Idempotent : ne re-telecharge pas un fichier deja present.
Jamais commite (voir .gitignore) - a relancer sur toute nouvelle machine/session.
#>

$ErrorActionPreference = "Stop"

function Get-IfMissing {
    param([string]$Url, [string]$Destination)
    if (Test-Path $Destination) {
        Write-Host "deja present : $Destination"
        return
    }
    Write-Host "telechargement : $Url -> $Destination"
    Invoke-WebRequest -Uri $Url -OutFile $Destination
}

$root = Split-Path -Parent $PSScriptRoot
$embeddingDir = Join-Path $root "models\embedding"
$rerankerDir = Join-Path $root "models\reranker"

New-Item -ItemType Directory -Force -Path $embeddingDir | Out-Null
New-Item -ItemType Directory -Force -Path $rerankerDir | Out-Null

# Embedding : Xenova/paraphrase-multilingual-mpnet-base-v2 (export ONNX de
# sentence-transformers/paraphrase-multilingual-mpnet-base-v2), variante quantifiee int8 (~278 Mo)
$embeddingBase = "https://huggingface.co/Xenova/paraphrase-multilingual-mpnet-base-v2/resolve/main"
Get-IfMissing "$embeddingBase/onnx/model_quantized.onnx" (Join-Path $embeddingDir "model_quantized.onnx")
Get-IfMissing "$embeddingBase/tokenizer.json" (Join-Path $embeddingDir "tokenizer.json")
Get-IfMissing "$embeddingBase/sentencepiece.bpe.model" (Join-Path $embeddingDir "sentencepiece.bpe.model")
Get-IfMissing "$embeddingBase/tokenizer_config.json" (Join-Path $embeddingDir "tokenizer_config.json")
Get-IfMissing "$embeddingBase/special_tokens_map.json" (Join-Path $embeddingDir "special_tokens_map.json")
Get-IfMissing "$embeddingBase/config.json" (Join-Path $embeddingDir "config.json")

# Reranking : onnx-community/bge-reranker-v2-m3-ONNX (export ONNX de BAAI/bge-reranker-v2-m3),
# variante quantifiee int8 (~570 Mo)
$rerankerBase = "https://huggingface.co/onnx-community/bge-reranker-v2-m3-ONNX/resolve/main"
Get-IfMissing "$rerankerBase/onnx/model_quantized.onnx" (Join-Path $rerankerDir "model_quantized.onnx")
Get-IfMissing "$rerankerBase/tokenizer.json" (Join-Path $rerankerDir "tokenizer.json")
Get-IfMissing "$rerankerBase/tokenizer_config.json" (Join-Path $rerankerDir "tokenizer_config.json")
Get-IfMissing "$rerankerBase/special_tokens_map.json" (Join-Path $rerankerDir "special_tokens_map.json")
Get-IfMissing "$rerankerBase/config.json" (Join-Path $rerankerDir "config.json")
# sentencepiece.bpe.model absent du depot ONNX communautaire -> recupere depuis le
# depot d'origine BAAI (meme vocabulaire, la conversion ONNX ne change pas la tokenisation)
Get-IfMissing "https://huggingface.co/BAAI/bge-reranker-v2-m3/resolve/main/sentencepiece.bpe.model" (Join-Path $rerankerDir "sentencepiece.bpe.model")

Write-Host "Modeles prets dans $root\models\"
