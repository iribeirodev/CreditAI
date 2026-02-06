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

        var embeddings = await embeddingGenerator.GenerateAsync(
                                                values: [request.HistoricText.Trim()], 
                                                cancellationToken: ct);

        var vectorBytes = VectorHelper.ToByteArray(
                            embeddings[0].Vector.ToArray());

        var customer = new Customer
        {
            Name = request.Name.Trim(),
            FinancialScore = request.FinancialScore,
            HistoricText = request.HistoricText.Trim(),
            BehaviorEmbedding = VectorHelper.ToByteArray(embeddings[0].Vector.ToArray()),
            CreatedAt = DateTime.UtcNow,
        };

        context.Customers.Add(customer);
        await context.SaveChangesAsync(ct);

        return new ClientResponse
        {
            PublicId = customer.PublicId,
            Name = customer.Name,
            FinancialScore = customer.FinancialScore,
            HistoricText = customer.HistoricText,
            LastAnalysisDate = customer.CreatedAt,
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
    /// Busca por similaridade: Encontra outros clientes com comportamento semelhante
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
            throw new InvalidOperationException($"Cliente com ID {publicId} não possui um embedding.");

        var targetVector = VectorHelper.ToFloatArray(target.BehaviorEmbedding);

        // Traz candidados que tenham embedding
        var candidates = await context.Customers
            .Where(c => c.PublicId != publicId
                    && c.BehaviorEmbedding != null)
            .AsNoTracking()
            .Select(c => new
            {
                c.PublicId,
                c.Name,
                c.FinancialScore,
                c.BehaviorEmbedding
            })
            .ToListAsync(ct);

        // Calcula a similaridade e ordena os resultados retornando top N clientes mais similares
        var results = candidates
            .Select(c =>
            {
                var similarity = TensorPrimitives.CosineSimilarity(
                    VectorHelper.ToFloatArray(c.BehaviorEmbedding!),
                    targetVector);

                return new SimilarCustomerResponse
                {
                    Id = c.PublicId,
                    Name = c.Name,
                    FinancialScore = c.FinancialScore,
                    Similarity = similarity
                };
            })
            .OrderByDescending(x => x.Similarity)
            .Take(limit)
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
                                    PublicId = c.PublicId,
                                    Name = c.Name,
                                    FinancialScore = c.FinancialScore,
                                    HistoricText = c.HistoricText,
                                    LastAnalysisDate = c.CreatedAt
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
                PublicId = c.PublicId,
                Name = c.Name,
                FinancialScore = c.FinancialScore,
                HistoricText = c.HistoricText,
                LastAnalysisDate = c.CreatedAt
            })
            .FirstOrDefaultAsync(ct);

        if (client is null)
            throw new KeyNotFoundException($"Cliente com ID {id} não encontrado.");

        return client;
    }

    private static string BuildPrompt(Customer customer, string question)
    {
        return $"""
            System: Você é um especialista sênior em risco de crédito.
            Context:
            - Customer: {customer.Name}
            - Financial Score: {customer.FinancialScore}
            - Behavioral History: {customer.HistoricText}

            Pergunta do usuário: 
            {question}

            Regras:
            - Seja técnico e objetivo.
            - Justifique a decisão.
            - Não alucine informações, baseie-se apenas no contexto fornecido.
            """;
    }
}
