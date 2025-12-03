# 📊 Bateria de Testes - FinDocAnalyser

**Data:** 03/12/2025  
**Branch:** feat/DocumentLayoutAnalysis  
**Total de PDFs:** 8  

---

## 📄 PDF 1: Safra - Ocultado.pdf

**Metadata:**
- **Analysis ID:** f3ff2a05-d9ea-4e11-82c2-1b2a9820f8dc
- **Tamanho:** 496,951 bytes (~485 KB)
- **Hash:** 4ea1ad7408aa7fa372ee7c8f7bffa7759afd46ff7f6b3ac725f9ddc83c00536d
- **Duração Processamento:** 45.44 segundos
- **Tokens Usados:** 24,140
- **Custo Estimado:** $0.114665
- **Modelo:** gpt-4o-2024-08-06

---

### 🎯 Endpoint 1: `/api/analysis/total-invested`

#### Dados Extraídos
| Campo | Valor Extraído |
|-------|----------------|
| **Total Investido** | R$ 27.434.756,44 |
| **Moeda** | BRL |

#### Dados Reais (a preencher)
| Campo | Valor Real | Match | Erro |
|-------|------------|-------|------|
| **Total Investido** | _aguardando_ | ⏳ | - |

---

### 🎯 Endpoint 2: `/api/analysis/asset-classification`

#### Dados Extraídos
| Classe de Ativo | Valor Investido | Percentual | Confidence |
|-----------------|-----------------|------------|------------|
| **Renda Fixa** | R$ 7.821.422,48 | 28,51% | 0.95 |
| **Curto Prazo** | R$ 52.304,01 | 0,19% | 0.95 |
| **Previdência** | R$ 300.568,77 | 1,10% | 0.95 |
| **Corretora** | R$ 19.260.461,18 | 70,20% | 0.95 |
| **TOTAL** | **R$ 27.434.756,44** | **100%** | - |

#### Dados Reais (a preencher)
| Classe de Ativo | Valor Real | Valor Extraído | Match | Erro (R$) | Erro (%) |
|-----------------|------------|----------------|-------|-----------|----------|
| Renda Fixa | _aguardando_ | R$ 7.821.422,48 | ⏳ | - | - |
| Curto Prazo | _aguardando_ | R$ 52.304,01 | ⏳ | - | - |
| Previdência | _aguardando_ | R$ 300.568,77 | ⏳ | - | - |
| Corretora | _aguardando_ | R$ 19.260.461,18 | ⏳ | - | - |

---

### 🎯 Endpoint 3: `/api/analysis/stocks`

**Total Investido em Ações:** R$ 19.260.461,18

#### Dados Extraídos (15 ativos)

| # | Ticker | Qtd | Preço Médio | Valor Atual | Retorno % | Confidence |
|---|--------|-----|-------------|-------------|-----------|------------|
| 1 | CSAN3 | 58.900 | R$ 6,17 | R$ 363.413,00 | -22,70% | 0.95 |
| 2 | ENEV3 | 93.200 | R$ 16,55 | R$ 1.542.460,00 | +58,08% | 0.95 |
| 3 | FESA4 | 84.000 | R$ 6,45 | R$ 541.800,00 | -19,13% | 0.95 |
| 4 | GOAU4 | 150.000 | R$ 9,52 | R$ 1.428.000,00 | -9,54% | 0.95 |
| 5 | ITSA4 | 104.317 | R$ 11,47 | R$ 1.196.515,99 | +44,40% | 0.95 |
| 6 | PRIO3 | 152.700 | R$ 38,13 | R$ 5.822.451,00 | -6,87% | 0.95 |
| 7 | TAEE11 | 30.000 | R$ 36,66 | R$ 1.099.800,00 | +20,90% | 0.95 |
| 8 | BPAC11 | 45.000 | R$ 48,26 | R$ 2.171.700,00 | +80,21% | 0.95 |
| 9 | HAPV3 | 3.878 | R$ 35,85 | R$ 139.026,30 | +9,08% | 0.95 |
| 10 | LOGG3 | 45.900 | R$ 23,50 | R$ 1.078.650,00 | +32,42% | 0.95 |
| 11 | LWSA3 | 88.000 | R$ 4,58 | R$ 403.040,00 | +40,48% | 0.95 |
| 12 | INBR32 | 31.826 | R$ 49,48 | R$ 1.574.750,48 | +99,57% | 0.95 |
| 13 | ALOS3 | 60.000 | R$ 25,83 | R$ 1.549.800,00 | +49,64% | 0.95 |
| 14 | AZZA3 | 3.251 | R$ 30,15 | R$ 98.017,65 | -13,14% | 0.95 |
| 15 | BRAV3 | 13.962 | R$ 17,98 | R$ 251.036,76 | -10,32% | 0.95 |

#### Dados Reais (a preencher)

| Ticker | Qtd Real | Qtd Extraída | Match Qtd | Preço Real | Preço Extraído | Match Preço | Valor Real | Valor Extraído | Match Valor |
|--------|----------|--------------|-----------|------------|----------------|-------------|------------|----------------|-------------|
| CSAN3 | _aguardando_ | 58.900 | ⏳ | _aguardando_ | R$ 6,17 | ⏳ | _aguardando_ | R$ 363.413,00 | ⏳ |
| ENEV3 | _aguardando_ | 93.200 | ⏳ | _aguardando_ | R$ 16,55 | ⏳ | _aguardando_ | R$ 1.542.460,00 | ⏳ |
| FESA4 | _aguardando_ | 84.000 | ⏳ | _aguardando_ | R$ 6,45 | ⏳ | _aguardando_ | R$ 541.800,00 | ⏳ |
| GOAU4 | _aguardando_ | 150.000 | ⏳ | _aguardando_ | R$ 9,52 | ⏳ | _aguardando_ | R$ 1.428.000,00 | ⏳ |
| ITSA4 | _aguardando_ | 104.317 | ⏳ | _aguardando_ | R$ 11,47 | ⏳ | _aguardando_ | R$ 1.196.515,99 | ⏳ |
| PRIO3 | _aguardando_ | 152.700 | ⏳ | _aguardando_ | R$ 38,13 | ⏳ | _aguardando_ | R$ 5.822.451,00 | ⏳ |
| TAEE11 | _aguardando_ | 30.000 | ⏳ | _aguardando_ | R$ 36,66 | ⏳ | _aguardando_ | R$ 1.099.800,00 | ⏳ |
| BPAC11 | _aguardando_ | 45.000 | ⏳ | _aguardando_ | R$ 48,26 | ⏳ | _aguardando_ | R$ 2.171.700,00 | ⏳ |
| HAPV3 | _aguardando_ | 3.878 | ⏳ | _aguardando_ | R$ 35,85 | ⏳ | _aguardando_ | R$ 139.026,30 | ⏳ |
| LOGG3 | _aguardando_ | 45.900 | ⏳ | _aguardando_ | R$ 23,50 | ⏳ | _aguardando_ | R$ 1.078.650,00 | ⏳ |
| LWSA3 | _aguardando_ | 88.000 | ⏳ | _aguardando_ | R$ 4,58 | ⏳ | _aguardando_ | R$ 403.040,00 | ⏳ |
| INBR32 | _aguardando_ | 31.826 | ⏳ | _aguardando_ | R$ 49,48 | ⏳ | _aguardando_ | R$ 1.574.750,48 | ⏳ |
| ALOS3 | _aguardando_ | 60.000 | ⏳ | _aguardando_ | R$ 25,83 | ⏳ | _aguardando_ | R$ 1.549.800,00 | ⏳ |
| AZZA3 | _aguardando_ | 3.251 | ⏳ | _aguardando_ | R$ 30,15 | ⏳ | _aguardando_ | R$ 98.017,65 | ⏳ |
| BRAV3 | _aguardando_ | 13.962 | ⏳ | _aguardando_ | R$ 17,98 | ⏳ | _aguardando_ | R$ 251.036,76 | ⏳ |

---

### 🎯 Endpoint 4: `/api/analysis/fixed-income`

**Total Investido em Renda Fixa:** R$ 7.821.422,48

#### Dados Extraídos (7 ativos)

| # | Nome do Ativo | Tipo | Emissor | Valor Investido | Valor Atual | Taxa | Vencimento | Aplicação | Retorno % |
|---|---------------|------|---------|-----------------|-------------|------|------------|-----------|-----------|
| 1 | CDB Emissão Safra CDI | CDB | SAFRABM | R$ 214.000,00 | R$ 219.132,32 | 100% CDI | 21/07/2027 | 31/07/2025 | +10,35% |
| 2 | LCA Emissão Safra CDI | LCA | SAFRABM | R$ 1.200.000,00 | R$ 1.239.595,61 | 95% CDI | 14/02/2029 | 04/07/2025 | +11,87% |
| 3 | LCA Emissão Safra CDI | LCA | SAFRABM | R$ 1.600.000,00 | R$ 1.642.441,74 | 95% CDI | 23/07/2029 | 22/07/2025 | +11,87% |
| 4 | LCA Emissão Safra CDI | LCA | SAFRABM | R$ 529.000,00 | R$ 709.440,43 | 98.5% CDI | 18/02/2028 | 16/03/2023 | +11,87% |
| 5 | LCA Emissão Terceiros CDI | LCA | BNDESBD | R$ 1.502.137,48 | R$ 1.537.445,48 | 86% CDI | 15/05/2026 | 23/07/2025 | +9,79% |
| 6 | TCM CRAPRE - PRE IE | Debenture | MARFRIG FRIG | R$ 1.000.000,00 | R$ 968.909,33 | +11% PRE | 16/08/2027 | 10/08/2023 | +21,18% |
| 7 | TCM CRAPRE - PRE IE | Debenture | IPIRANGAPRODPET | R$ 1.531.000,00 | R$ 1.504.457,57 | +11.17% PRE | 16/07/2027 | 28/07/2023 | +21,18% |

#### Dados Reais (a preencher)

| Nome do Ativo | Valor Investido Real | Valor Extraído | Match | Erro (R$) | Erro (%) | Taxa Real | Taxa Extraída | Match Taxa |
|---------------|----------------------|----------------|-------|-----------|----------|-----------|---------------|------------|
| CDB Emissão Safra CDI | _aguardando_ | R$ 214.000,00 | ⏳ | - | - | _aguardando_ | 100% CDI | ⏳ |
| LCA Emissão Safra CDI (1) | _aguardando_ | R$ 1.200.000,00 | ⏳ | - | - | _aguardando_ | 95% CDI | ⏳ |
| LCA Emissão Safra CDI (2) | _aguardando_ | R$ 1.600.000,00 | ⏳ | - | - | _aguardando_ | 95% CDI | ⏳ |
| LCA Emissão Safra CDI (3) | _aguardando_ | R$ 529.000,00 | ⏳ | - | - | _aguardando_ | 98.5% CDI | ⏳ |
| LCA Emissão Terceiros CDI | _aguardando_ | R$ 1.502.137,48 | ⏳ | - | - | _aguardando_ | 86% CDI | ⏳ |
| TCM CRAPRE - PRE IE (MARFRIG) | _aguardando_ | R$ 1.000.000,00 | ⏳ | - | - | _aguardando_ | +11% PRE | ⏳ |
| TCM CRAPRE - PRE IE (IPIRANGA) | _aguardando_ | R$ 1.531.000,00 | ⏳ | - | - | _aguardando_ | +11.17% PRE | ⏳ |

---

## 📈 Resumo Parcial - PDF 1 (Safra)

### Estatísticas de Extração
- ✅ **Total de Endpoints:** 4
- ✅ **Ações Detectadas:** 15
- ✅ **Renda Fixa Detectada:** 7 ativos
- ✅ **Classes de Ativo:** 4
- ⏱️ **Tempo de Processamento:** 45,44s
- 💰 **Custo:** $0.114665

### Métricas (aguardando dados reais)
- **Acurácia Geral:** _aguardando validação_
- **Precisão Média:** _aguardando validação_
- **Recall:** _aguardando validação_

---

## 📊 Resumo Geral da Bateria (0/8 completos)

| PDF | Status | Endpoints | Ações | Renda Fixa | Acurácia | Tempo |
|-----|--------|-----------|-------|------------|----------|-------|
| 1. Safra - Ocultado.pdf | ⏳ Aguardando validação | 4 | 15 | 7 | - | 45.44s |
| 2. _aguardando_ | - | - | - | - | - | - |
| 3. _aguardando_ | - | - | - | - | - | - |
| 4. _aguardando_ | - | - | - | - | - | - |
| 5. _aguardando_ | - | - | - | - | - | - |
| 6. _aguardando_ | - | - | - | - | - | - |
| 7. _aguardando_ | - | - | - | - | - | - |
| 8. _aguardando_ | - | - | - | - | - | - |

---

## 🎯 Próximos Passos

1. ⏳ Receber valores reais do PDF 1 (Safra) para validação
2. ⏳ Receber metadata dos PDFs 2-8
3. ⏳ Calcular métricas de precisão/acurácia
4. ⏳ Gerar relatório final consolidado

---

**Última atualização:** 03/12/2025 às 17:51 UTC
