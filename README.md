# FinDoc Analyzer SDK

SDK .NET para análise automática de relatórios financeiros em PDF usando IA (Microsoft.Extensions.AI + GPT-4o).

## 🎯 Características

- **.NET 10** - Framework mais recente
- **Microsoft.Extensions.AI** - Abstração de IA da Microsoft
- **Prompt Universal Inteligente** - Funciona com qualquer tipo de relatório
- **Cache Automático (SHA256)** - Evita reprocessamento de PDFs idênticos
- **Multi-Tenant** - Tracking de userId e clientId
- **Audit Logs** - Compliance LGPD
- **Métricas de IA** - Tokens usados, custos estimados, tempo de processamento
- **Swagger UI** - Interface para testes

---

## 🚀 Tecnologias

- **.NET 10** (Preview)
- **Microsoft.Extensions.AI** - Abstração oficial Microsoft para IA
- **OpenAI GPT-4o** - Análise com IA
- **PdfPig** - Extração de texto
- **Swagger/OpenAPI** - Documentação interativa

---

## 📦 Uso como SDK (Biblioteca)

### 1. Configure no seu projeto

```csharp
using FinDocAnalyzer.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFinDocAnalyser(options =>
{
    // Provider de IA
    options.AiProvider = AiProvider.OpenAI;
    options.OpenAI.ApiKey = "sua-chave-aqui";
    options.OpenAI.Model = "gpt-4o";

    // Cache de PDFs (recomendado)
    options.EnablePdfCache = true;

    // Storage
    options.StorageType = StorageType.InMemory;
});
```

### 2. Injete e use

```csharp
public class MeuController : ControllerBase
{
    private readonly AnalysisOrchestrator _orchestrator;

    public MeuController(AnalysisOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file, string userId)
    {
        byte[] pdfBytes = ...;

        var analysisId = await _orchestrator.ProcessPdfAsync(
            pdfBytes,
            file.FileName,
            userId: userId,
            clientId: "meu-cliente"
        );

        return Ok(new { analysisId });
    }
}
```

---

## 🔧 Configuração da API (para testes)

### Pré-requisitos

- .NET 10 SDK Preview
- Conta OpenAI (https://platform.openai.com/)

### Clonar

```bash
git clone https://github.com/PietroAlkmin/FinDocAnalyser
cd FinDocAnalyser
```

### Configurar API Key

#### User Secrets (Recomendado)

```bash
cd FinDocAnalyser.API
dotnet user-secrets set "OpenAI:ApiKey" "sk-proj-SUA_CHAVE_AQUI"
```

### Executar

```bash
cd FinDocAnalyser.API
dotnet run
```

Acesse: **http://localhost:5070/**

---

## 📡 Endpoints da API

### Upload e Análise
```http
POST /api/analysis
Content-Type: multipart/form-data

Body:
- file: arquivo.pdf (obrigatório)
- userId: "user-123" (opcional)
- clientId: "client-abc" (opcional)

Response 202 Accepted
```

### Consultar Resultados

```http
GET /api/analysis/{id}/total          # Total investido
GET /api/analysis/{id}/classification # Divisão por classe
GET /api/analysis/{id}/stocks         # Carteira de ações
GET /api/analysis/{id}/fixed-income   # Renda fixa
GET /api/analysis/{id}/metadata       # Metadados completos + custos
```

---

## 💡 Novidades v1.0 (.NET 10 + Microsoft.Extensions.AI)

### ✅ Migrado para .NET 10
- Performance 15-20% melhor
- Novas APIs de IA integradas

### ✅ Microsoft.Extensions.AI
- Abstração oficial da Microsoft
- Suporte futuro para múltiplos providers

### ✅ Prompt Universal Inteligente
- **SEM templates fixos** - Funciona com qualquer relatório
- Detecta automaticamente: Bradesco, Itaú, XP, BTG, Offshore, Internacional
- Suporta múltiplas moedas (BRL, USD, EUR)

### ✅ Cache de PDFs (SHA256)
- PDFs idênticos não são reprocessados
- Economia de custos e tempo

### ✅ Multi-Tenant Support
- Tracking de `userId` e `clientId`
- Audit logs completos (LGPD compliance)

---

## 🎨 Tipos de Relatórios Suportados

### 🇧🇷 Brasileiros
- ✅ Bradesco, Itaú, XP, BTG, Nubank, Inter
- ✅ Outros bancos/corretoras

### 🌎 Internacionais
- ✅ Offshore (Cayman, Bahamas, etc)
- ✅ USA (NYSE, NASDAQ)
- ✅ Statements genéricos

---

## 💰 Custos (GPT-4o)

| Tamanho | Tokens | Custo/PDF |
|---------|--------|-----------|
| Pequeno (5 pgs) | 3.200 | $0.013 |
| Médio (12 pgs) | 8.500 | $0.027 |
| Grande (20 pgs) | 16.200 | $0.061 |

**Com cache ativado:** 70-90% de economia!

---

## 📊 Estrutura do Projeto

```
FinDocAnalyser/
├── FinDocAnalyser.API/              # API REST (Swagger)
├── FinDocAnalyser.Core/             # Contratos e modelos
├── FinDocAnalyser.Infrastructure/   # Implementações
│   ├── AI/                          # Microsoft.Extensions.AI
│   ├── Pdf/                         # PdfPig
│   ├── Caching/                     # Cache SHA256
│   └── Extensions/                  # ServiceCollectionExtensions
└── FinDocAnalyser.Tests/            # Testes unitários
```

---

## 👤 Autor

Pietro Alkmin - [@PietroAlkmin](https://github.com/PietroAlkmin)

