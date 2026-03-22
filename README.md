# RAG SaaS Backend

Production-ready .NET 8 RAG backend with:
- document upload (`.pdf` and `.txt`)
- chunking + embeddings generation
- Pinecone vector upsert/query
- question answering endpoint constrained to retrieved context
- unit tests and CI in GitHub Actions

## Project Structure

- `backend/RAGService/RAGService` - API project
- `backend/RAGService/RAGService.Tests` - unit tests
- `.github/workflows/ci.yml` - CI pipeline (restore/build/test)

## Prerequisites

- .NET SDK 8.x
- OpenAI API key
- Pinecone index and API key

## Configuration

Set values in `backend/RAGService/RAGService/appsettings.json` or via environment variables:

- `OpenAI__ApiKey`
- `OpenAI__BaseUrl` (default: `https://api.openai.com/v1/`)
- `OpenAI__EmbeddingModel` (default: `text-embedding-3-small`)
- `OpenAI__ChatModel` (default: `gpt-4o-mini`)
- `Pinecone__ApiKey`
- `Pinecone__BaseUrl`
- `Pinecone__DefaultTopK`

## Run Locally

```bash
cd backend/RAGService
dotnet restore
dotnet run --project RAGService/RAGService.csproj
```

Swagger UI is available in development mode.

## Test Locally

```bash
cd backend/RAGService
dotnet test RAGService.sln
```

## API Endpoints

- `POST /api/documents/upload` (multipart form-data, field `file`)
- `POST /api/queries/ask` (JSON body: `{ "question": "..." }`)
- `GET /health`
