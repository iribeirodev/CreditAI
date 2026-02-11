using System.Numerics.Tensors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using CreditAI.API.Domain.Entities;
using CreditAI.API.Helpers;
using CreditAI.API.Infrastructure.Data;
using CreditAI.API.DTO;

namespace CreditAI.API.Services;

public class CreditAnalysisService(
    AppDbContext context, 
    Kernel kernel, 
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
{

    /// <summary>
    /// Vetoriza o histórico do cliente e armazena as informações no banco de dados para análises futuras.
    /// </summary>
    public async Task<ClientResponse> Ingest(ClientRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedHistoric = NormalizeHistoric(request.HistoricText.Trim());
        if (normalizedHistoric.Length < 30)
            throw new ArgumentException("O histórico do cliente deve conter pelo menos 30 caracteres para uma análise significativa.");

        var embeddings = await embeddingGenerator.GenerateAsync(
                                                values: [normalizedHistoric], 
                                                cancellationToken: ct);

        var vectorBytes = VectorHelper.ToByteArray(
                            embeddings[0].Vector.ToArray());

        var customer = new Customer
        {
            Name = request.Name.Trim(),
            FinancialScore = request.FinancialScore,
            HistoricText = request.HistoricText.Trim(),
            BehaviorEmbedding = vectorBytes,
            CreatedAt = DateTime.UtcNow,
        };

        context.Customers.Add(customer);
        await context.SaveChangesAsync(ct);

        return new ClientResponse
        {
            Id = customer.PublicId,
            Name = customer.Name,
            FinancialScore = customer.FinancialScore,
            HistoricText = customer.HistoricText,
            CreatedAt = customer.CreatedAt,
        };
    }

    public async Task<ClientResponse> IngestSmart(SmartClientRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Transforma o JSON bruto em um texto narrativo
        var systemPrompt = BuildCreatePrompt();

        var rawDataJson = System.Text.Json.JsonSerializer.Serialize(request.RawData);
        var fullPrompt = $"{systemPrompt}\n\nDados do Cliente:\n{rawDataJson}";

        var aiResult = await kernel.InvokePromptAsync(fullPrompt, cancellationToken: ct);
        var generatedHistoric = aiResult.ToString().Trim();

        // Embedding
        var normalizedHistoric = NormalizeHistoric(generatedHistoric);
        var embeddings = await embeddingGenerator.GenerateAsync(
                            values: [normalizedHistoric],
                            cancellationToken: ct);

        var vectorBytes = VectorHelper.ToByteArray(embeddings[0].Vector.ToArray());

        var customer = new Customer
        {
            Name = request.Name.Trim(),
            FinancialScore = request.FinancialScore,
            HistoricText = generatedHistoric,
            BehaviorEmbedding = vectorBytes,
            CreatedAt = DateTime.UtcNow,
        };

        context.Customers.Add(customer);
        await context.SaveChangesAsync(ct);

        return new ClientResponse
        {
            Id = customer.PublicId,
            Name = customer.Name,
            FinancialScore = customer.FinancialScore,
            HistoricText = customer.HistoricText,
            CreatedAt = customer.CreatedAt
        };
    }

    /// <summary>
    /// Realiza a análise de risco do cliente com base no histórico e na pontuação financeira, 
    /// utilizando o modelo de linguagem para fornecer insights técnicos sobre o perfil de risco do cliente.
    /// </summary>
    public async Task<RiskAnalysisResponse> AnalyzeRisk(
        Guid publicId, 
        string question,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);

        var customer = await context.Customers
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(c => c.PublicId == publicId, ct);

        if (customer is null) 
            throw new KeyNotFoundException($"Cliente com ID {publicId} não encontrado.");

        var prompt = BuildAnalyzePrompt(customer, question);

        var result = await kernel.InvokePromptAsync(prompt, cancellationToken: ct);

        return new RiskAnalysisResponse
        {
            CustomerId = publicId,
            Analysis = result.ToString(),
        };

    }

    /// <summary>
    /// Identifica clientes com perfis comportamentais análogos através de busca vetorial híbrida.
    /// Prioriza o "DNA financeiro" (95% do peso) sobre o score numérico tradicional (5%),
    /// permitindo descobrir similaridades semânticas que sistemas de crédito puramente numéricos ignoram.
    /// </summary>
    public async Task<List<SimilarCustomerResponse>> GetSimilarCustomers(
        Guid publicId,
        int limit,
        CancellationToken ct)
    {
        var target = await context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.PublicId == publicId, ct);

        if (target is null)
            throw new KeyNotFoundException($"Cliente com ID {publicId} não encontrado.");

        if (target.BehaviorEmbedding is null)
            throw new InvalidOperationException($"O cliente {target.Name} ainda não possui vetorização.");

        var targetVector = VectorHelper.ToFloatArray(target.BehaviorEmbedding);

        var candidates = await context.Customers
            .Where(c => c.PublicId != publicId && c.BehaviorEmbedding != null)
            .AsNoTracking()
            .Select(c => new
            {
                c.PublicId,
                c.Name,
                c.FinancialScore,
                c.BehaviorEmbedding
            })
            .ToListAsync(ct);

        var results = candidates
            .Select(c =>
            {
                var embeddingVector = VectorHelper.ToFloatArray(c.BehaviorEmbedding!);

                // Cálculo da Similaridade de Cosseno
                var vectorSimilarity = TensorPrimitives.CosineSimilarity(embeddingVector, targetVector);

                // Corte de relevância para evitar ruído
                if (vectorSimilarity < 0.75f) return null;

                // Normalização do Score Financeiro (Escala 0..1)
                var scoreDistance = Math.Abs(c.FinancialScore - target.FinancialScore);
                var scoreSimilarity = 1f - (scoreDistance / 1000f);

                // Ajuste 95% Vetor e 5% Score para que o perfil domine a ordenação
                var finalScore = (vectorSimilarity * 0.95f) + (scoreSimilarity * 0.05f);

                return new
                {
                    c.PublicId,
                    c.Name,
                    c.FinancialScore,
                    FinalScore = finalScore,
                    VectorSimilarity = vectorSimilarity
                };
            })
            .Where(x => x is not null)
            .OrderByDescending(x => x!.VectorSimilarity)
            .Take(limit)
            .Select(x => new SimilarCustomerResponse
            {
                Id = x!.PublicId,
                Name = x.Name,
                FinancialScore = x.FinancialScore,
                // Exibe a porcentagem real de afinidade comportamental
                Similarity = MathF.Round(x.VectorSimilarity * 100, 2),
            })
            .ToList();

        return results;
    }

    /// <summary>
    /// Traz uma lista de todos os clientes para fins de demonstração, sem rastreamento para otimizar a consulta.
    /// </summary>
    public async Task<PagedResponse<ClientResponse>> GetAllClients(
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var totalItems = await context.Customers.CountAsync(ct);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var clients = await context.Customers
                                .AsNoTracking()
                                .OrderBy(c => c.Name)
                                .Skip((page - 1) * pageSize)
                                .Take(pageSize)
                                .Select(c => new ClientResponse
                                {
                                    Id = c.PublicId,
                                    Name = c.Name,
                                    FinancialScore = c.FinancialScore,
                                    HistoricText = c.HistoricText,
                                    CreatedAt = c.CreatedAt
                                })
                                .ToListAsync(ct);


        return new PagedResponse<ClientResponse>
        {
            PageNumber = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
            Items = clients
        };
    }

    /// <summary>
    /// Retorna um cliente específico pelo ID
    /// </summary>
    public async Task<ClientResponse> GetById(
        Guid id,
        CancellationToken ct)
    {
        var client = await context.Customers
            .AsNoTracking()
            .Where(c => c.PublicId == id)
            .Select(c => new ClientResponse
            {
                Id = c.PublicId,
                Name = c.Name,
                FinancialScore = c.FinancialScore,
                HistoricText = c.HistoricText,
                CreatedAt = c.CreatedAt
            })
            .FirstOrDefaultAsync(ct);

        if (client is null)
            throw new KeyNotFoundException($"Cliente com ID {id} não encontrado.");

        return client;
    }

    private static string BuildAnalyzePrompt(Customer customer, string question)
    {
        return $"""
            System: Você é um especialista sênior em risco de crédito bancário.

            Analise o cliente abaixo com rigor técnico, como se estivesse produzindo um parecer para um comitê de crédito.

            Dados do cliente:
            - Nome: {customer.Name}
            - Score financeiro: {customer.FinancialScore}
            - Histórico comportamental: {customer.HistoricText}

            Pergunta:
            {question}

            Instruções obrigatórias:

            1. Classifique objetivamente:
               - Risco de crédito: Baixo, Médio ou Alto
               - Risco de evasão (churn): Baixo, Médio ou Alto

            2. Justifique cada classificação com evidências do histórico.

            3. Aponte sinais de atenção relevantes.

            4. Recomende uma ação prática para o banco.

            Regras:
            - Seja técnico, direto e profissional.
            - Não invente informações.
            - Baseie-se apenas nos dados fornecidos.
            - Responda apenas em texto puro.
            - Não utilize símbolos como **, #, -, ou listas formatadas.
            - Escreva em parágrafos simples.
            - Não seja genérico.
            """;
    }

    private static string BuildCreatePrompt()
    {
        return """
        Você é um Analista de Risco de Crédito Sênior especializado em Behavior Score.
        Sua tarefa é converter dados brutos (JSON) em um "Perfil de DNA Financeiro" conciso.

        ### REGRAS DE ANÁLISE:
        1. IDENTIFIQUE o padrão: O cliente é Estável, Oscilante ou Insolvente?
        2. ANALISE os gatilhos: 
           - MCC_7995 ou CHQ_DEVOLVIDO = Risco Crítico. Use palavras como "Insolvência", "Descontrole" e "Inidoneidade".
           - Faturamento variável ou MEI = Risco Moderado. Use palavras como "Empreendedor", "Fluxo Inconstante" e "Sazonalidade".
           - Renda Fixa ou Aposentadoria = Risco Baixo. Use palavras como "Previsibilidade Máxima", "Segurança" e "Conservador".

        ### RESTRIÇÕES:
        - Não use frases genéricas como "Perfil interessante".
        - Seja direto: use no máximo 2 frases (aproximadamente 30 palavras).
        - Foque na distância semântica: perfis estáveis devem soar opostos a perfis de risco.

        ### SAÍDA ESPERADA:
        Um resumo narrativo que descreva a essência do comportamento financeiro para fins de busca vetorial.
        """;
    }

    private static string NormalizeHistoric(string text)
        => text.Trim().Replace("\n", " ").Replace("\r", " ").ToLowerInvariant();
}
