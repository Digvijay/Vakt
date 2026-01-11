# Developer Integration Guide 👩‍💻

Vakt is designed as a **transparent proxy**. This means you can use your existing Azure OpenAI SDKs without code rewrites—just a configuration change.

## 1. C# (.NET)

Using the standard `Azure.AI.OpenAI` library.

### Before (Direct to Azure)
```csharp
var endpoint = new Uri("https://my-resource.openai.azure.com/");
var credentials = new AzureKeyCredential("my-azure-api-key");

var client = new OpenAIClient(endpoint, credentials);
```

### After (Via Vakt)
Point the client to your local Vakt Gateway (default: `http://localhost:5000`).

```csharp
// 1. Change Endpoint to Vakt
var endpoint = new Uri("http://localhost:5000/"); 

// 2. Keep your real Azure Key (Vakt forwards it)
var credentials = new AzureKeyCredential("my-azure-api-key");

var client = new OpenAIClient(endpoint, credentials);
```

---

## 2. Python

### Native OpenAI Client
Using the official `openai` python package.

#### Before
```python
import os
from openai import AzureOpenAI

client = AzureOpenAI(
    api_key=os.getenv("AZURE_OPENAI_API_KEY"),  
    api_version="2024-02-01",
    azure_endpoint="https://my-resource.openai.azure.com"
)
```

#### After
```python
import os
from openai import AzureOpenAI

client = AzureOpenAI(
    api_key=os.getenv("AZURE_OPENAI_API_KEY"),  
    api_version="2024-02-01",
    # Point to Vakt local address
    azure_endpoint="http://localhost:5000"
)
```

### LangChain (Python)

#### Before
```python
from langchain_openai import AzureChatOpenAI

llm = AzureChatOpenAI(
    azure_deployment="gpt-4",
    api_version="2024-02-01",
    azure_endpoint="https://my-resource.openai.azure.com"
)
```

#### After
```python
from langchain_openai import AzureChatOpenAI

llm = AzureChatOpenAI(
    azure_deployment="gpt-4",
    api_version="2024-02-01",
    # Point to Vakt
    azure_endpoint="http://localhost:5000"
)
```

---

## 💡 Troubleshooting

### SSL / Certificate Errors
Since `localhost` often uses self-signed certificates in dev, you might see SSL errors.

**C# Fix:**
Allow the dev certificate in your `HttpClient` handler or trust the dotnet dev certs (`dotnet dev-certs https --trust`).

**Python Fix:**
For testing **only**, you can disable SSL verification (not recommended for production):
```python
import httpx
client = AzureOpenAI(
    ...,
    http_client=httpx.Client(verify=False)
)
```
