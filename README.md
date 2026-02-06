# CreditAI - Análise de Crédito com IA Vetorial \& .NET 8

[![Build](https://github.com/iribeirodev/CreditAI/actions/workflows/dotnet.yml/badge.svg)](https://github.com/iribeirodev/CreditAI/actions/workflows/dotnet.yml)

**CreditAI** é uma API para avaliação de risco financeiro. Em vez de depender apenas de números (Scores), o sistema utiliza **IA Generativa (Mistral AI)** e **Busca Vetorial** para entender o comportamento real do cliente através do seu histórico textual.



---



## Diferenciais do Projeto



* **Busca Semântica (Embeddings):** Encontra perfis similares por comportamento e "DNA financeiro", indo além de palavras-chave.

* **Análise Cognitiva (RAG):** Utiliza LLMs para atuar como um analista de crédito sênior, justificando decisões complexas.

* **Segurança com User Secrets:** Proteção total de chaves de API e strings de conexão, seguindo padrões profissionais de desenvolvimento.

* **Neutralidade:** O motor de IA foca no histórico comportamental, ignorando dados biográficos para evitar vieses.



---



## Tecnologias Utilizadas



* **Runtime:** .NET 8 (ASP.NET Core)

* **IA:** [Mistral AI](https://mistral.ai/) (Modelos: `mistral-embed` e `mistral-small-latest`)

* **Orquestração de IA:** [Microsoft Semantic Kernel](https://learn.microsoft.com/en-us/semantic-kernel/) \& `Microsoft.Extensions.AI`

* **Banco de Dados utilizado:** SQL Server Express 2022.

* **ORM:** Entity Framework Core.



---



## Endpoints



### 1. `GET /api/Clients`

Lista todos os perfis cadastrados. Útil para verificar o estado inicial da base.



### 2. `POST /api/Clients/ingest`

Realiza o processamento de novos perfis. O histórico textual é enviado ao modelo mistral-embed para geração de um vetor de 1024 dimensões. O perfil, juntamente com seu Embedding, é persistido no SQL Server.

Input: JSON contendo nome, score e histórico textual.



### 3. `GET /api/Clients/{id}/similar/`

Executa uma busca por similaridade de cosseno no banco de dados. O sistema utiliza o vetor do cliente informado para encontrar outros perfis que possuam comportamentos financeiros semanticamente próximos, independentemente de valores numéricos de score.



Parâmetro: ID do cliente base para a comparação.



### 4. `GET /api/Clients/{id}/analyze`

O "Cérebro" do projeto. Recebe uma pergunta e utiliza o histórico do cliente para fornecer uma resposta inteligente.



---



## Configuração e Segurança



O projeto utiliza o **Secret Manager** do .NET para gerenciar credenciais sensíveis.



### 1. Configurar Chave da Mistral

No terminal da pasta do projeto:

```bash

dotnet user-secrets init

dotnet user-secrets set "Mistral:ApiKey" "SUA_CHAVE_AQUI"

```



## Exemplos de Teste Rápido (Ingest)



Para testar o funcionamento da API e a geração de embeddings pela Mistral, você pode utilizar o seguinte JSON no endpoint `POST /api/Clients/ingest`:



**Payload:**

```json

{

  "name": "Maria Oliveira",

  "financialScore": 680,

  "historicText": "Dona de uma pequena floricultura há 10 anos. Possui renda estável, mas prefere não utilizar serviços de crédito complexos. Movimentação bancária constante com fornecedores e baixo índice de endividamento. Busca crédito para renovação de fachada."

}

```



```json

{

  "name": "Carlos Freelancer",

  "financialScore": 610,

  "historicText": "Designer gráfico autônomo. Recebe pagamentos via plataformas internacionais em dólar. Possui alta volatilidade mensal, mas mantém as contas em dia. Não possui bens imóveis, mas tem investimentos em criptoativos e reserva de liquidez."

}

```



### Exemplo de Analyze (Pergunta Crítica)

Use este exemplo para mostrar a capacidade da Mistral de interpretar "volatilidade" vs "capacidade de pagamento"



\- Endpoint: `GET /api/Clients/{id}/analyze` (use o id do Carlos)



Pergunta de exemplo:



```

O Carlos é freelancer e recebe em dólar. Considerando a volatilidade da renda dele, 

mas a existência de reserva de liquidez, ele seria um bom candidato 

para um cartão de crédito com limite de R$ 5.000,00?

```



### Exemplo de Similaridade (Busca semântica)



Utiliza a distância vetorial para encontrar vizinhos próximos no "mapa" de comportamento financeiro.



\- Endpoint: GET /api/Clients/similar/{id} (Use o ID da Maria Oliveira)



O sistema retornará clientes que possuem estabilidade de longo prazo e baixo endividamento, mesmo que atuem em ramos totalmente diferentes (ex: comparando a floricultura com um funcionário público aposentado).

