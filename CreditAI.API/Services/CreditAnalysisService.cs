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

        var prompt = BuildPrompt(customer, question);

        var result = await kernel.InvokePromptAsync(prompt, cancellationToken: ct);

        return new RiskAnalysisResponse
        {
            CustomerId = publicId,
            Analysis = result.ToString(),
        };

    }

    /// <summary>
    /// Encontra clientes com comportamento semelhante utilizando busca vetorial.
    /// O histórico textual é convertido em embeddings (vetores numéricos),
    /// permitindo comparar semanticamente os perfis.
    /// </summary>
    public async Task<List<SimilarCustomerResponse>> GetSimilarCustomers(
        Guid publicId,
        int limit,
        CancellationToken ct)
    {
        // Cliente base da comparação
        var target = await context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.PublicId == publicId, ct);

        if (target is null)
            throw new KeyNotFoundException($"Cliente com ID {publicId} não encontrado.");

        if (target.BehaviorEmbedding is null)
            throw new InvalidOperationException($"Cliente com ID {publicId} não possui um embedding.");

        var targetVector = VectorHelper.ToFloatArray(target.BehaviorEmbedding);

        // Apenas candidatos com embedding
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

        // Calcula a similaridade entre o cliente alvo e os demais utilizando cosine similarity.
        // Cada histórico foi previamente transformado em um vetor numérico (embedding),
        // permitindo comparar semanticamente os perfis em vez de apenas dados estruturados.
        var results = candidates
            .Select(c =>
            {
                var embeddingVector = VectorHelper.ToFloatArray(c.BehaviorEmbedding!);

                var vectorSimilarity = TensorPrimitives.CosineSimilarity(
                    embeddingVector,
                    targetVector);

                // remove baixa relevância semântica
                if (vectorSimilarity < 0.75f)
                    return null;

                // normaliza diferença de score para escala 0..1
                var scoreDistance = Math.Abs(c.FinancialScore - target.FinancialScore);
                var scoreSimilarity = 1f - (scoreDistance / 1000f);

                // ranking híbrido (semântica + regra de negócio)
                var finalScore = (vectorSimilarity * 0.75f) +
                                 (scoreSimilarity * 0.25f);

                string explanation;

                if (vectorSimilarity > 0.90f)
                    explanation = "Perfis comportamentais quase idênticos.";
                else if (vectorSimilarity > 0.80f)
                    explanation = "Comportamento financeiro muito semelhante.";
                else
                    explanation = "Similaridade moderada baseada no histórico.";

                if (scoreSimilarity > 0.90f)
                    explanation += " Scores financeiros praticamente iguais.";
                else if (scoreSimilarity > 0.80f)
                    explanation += " Scores financeiros próximos.";

                return new
                {
                    c.PublicId,
                    c.Name,
                    c.FinancialScore,
                    FinalScore = finalScore,
                    VectorSimilarity = vectorSimilarity,
                    Explanation = explanation
                };
            })
            .Where(x => x is not null)
            .OrderByDescending(x => x!.FinalScore)
            .Take(limit)
            .Select(x => new SimilarCustomerResponse
            {
                Id = x!.PublicId,
                Name = x.Name,
                FinancialScore = x.FinancialScore,
                Similarity = MathF.Round(x.VectorSimilarity, 4),
                Reason = x.Explanation
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

    private static string BuildPrompt(Customer customer, string question)
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

    private static string NormalizeHistoric(string text)
        => text.Trim().Replace("\n", " ").Replace("\r", " ").ToLowerInvariant();
}
