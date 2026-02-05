using System.Numerics.Tensors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using CreditAI.API.Domain.Entities;
using CreditAI.API.Helpers;
using CreditAI.API.Infrastructure.Data;

namespace CreditAI.API.Services;

public class CreditAnalysisService(
    AppDbContext context, 
    Kernel kernel, 
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
{

    /// <summary>
    /// Vetoriza o histórico do cliente e armazena as informações no banco de dados para análises futuras.
    /// </summary>
    public async Task Ingest(string name, int score, string historic)
    {
        var generatedEmbeddings = await embeddingGenerator.GenerateAsync([historic]);
        var vectorArray = generatedEmbeddings[0].Vector.ToArray();

        context.Customers.Add(new Customer
        {
            Name = name,
            FinancialScore = score,
            HistoricText = historic,
            BehaviorEmbedding = VectorHelper.ToByteArray(vectorArray),
            LastAnalysisDate = DateTime.UtcNow,
        });

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Realiza a análise de risco do cliente com base no histórico e na pontuação financeira, 
    /// utilizando o modelo de linguagem para fornecer insights técnicos sobre o perfil de risco do cliente.
    /// </summary>
    public async Task<string> AnalyzeRisk(int customerId, string question)
    {
        var customer = await context.Customers
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(c => c.Id == customerId);

        if (customer == null) return "Cliente não encontrado.";

        var prompt = $"""
            System: Você é um especialista sênior em risco de crédito.
            Context:
            - Customer: {customer.Name}
            - Financial Score: {customer.FinancialScore}
            - Behavioral History: {customer.HistoricText}

            Pergunta do usuário: {question}

            Instrução: Analise o risco e o tom do cliente. Seja conciso e técnico.
            """;

        var result = await kernel.InvokePromptAsync(prompt);
        return result.ToString();
    }

    /// <summary>
    /// Busca por similaridade: Encontra outros clientes com comportamento semelhante
    /// </summary>
    public async Task<List<Customer>> GetSimilarCustomers(int customerId, int limit = 5)
    {
        var target = await context.Customers
                                .AsNoTracking()
                                .FirstOrDefaultAsync(c => c.Id == customerId);

        if (target == null) return [];

        var targetVector = VectorHelper.ToFloatArray(target.BehaviorEmbedding);

        // Como o SQL Server 2022 não possui busca vetorial nativa, 
        // traz os candidatos filtrados para a memória e utiliza as instruções 
        // de hardware do .NET via TensorPrimitives para calcular a 
        // similaridade de cosseno de forma ultra rápida.
        var candidates = await context.Customers
            .Where(c => c.Id != customerId)
            .AsNoTracking()
            .ToListAsync();

        var results = candidates
                    .Select(c => new
                    {
                        Customer = c,
                        // Compara os vetores
                        Similarity = TensorPrimitives.CosineSimilarity(
                            VectorHelper.ToFloatArray(c.BehaviorEmbedding),
                            targetVector)
                    })
                    // Ordena pela maior similaridade (1.0 é idêntico, 0.0 é ortogonal).
                    .OrderByDescending(x => x.Similarity)
                    // Aplica o "Top n", limitando o processamento apenas ao que será exibido.
                    .Take(limit)
                    // Converte de volta para Customer
                    .Select(x => x.Customer)
                    .ToList();

        return results;
    }

    /// <summary>
    /// Traz uma lista de todos os clientes para fins de demonstração, sem rastreamento para otimizar a consulta.
    /// </summary>
    public async Task<List<Customer>> GetAllClients()
        => await context.Customers
                        .AsNoTracking()
                        .OrderBy(c => c.Name)
                        .ToListAsync();

}
