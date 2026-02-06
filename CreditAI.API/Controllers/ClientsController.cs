using Microsoft.AspNetCore.Mvc;
using CreditAI.API.Services;
using CreditAI.API.DTO;

namespace CreditAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientsController(CreditAnalysisService creditAnalysisService): ControllerBase
{
    /// <summary>
    /// Lista todos os clientes cadastrados na base.
    /// </summary>
    /// <remarks>
    /// Retorna a lista completa para fins de conferência.
    /// </remarks>
    /// <response code="200">Lista de todos os clientes.</response>
    /// <returns>Lista de clientes.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var clients = await creditAnalysisService.GetAllClients();
        var response = clients.Select(c => new ClientResponse
        {
            Id = c.Id,
            Name = c.Name,
            FinancialScore = c.FinancialScore,
            HistoricText = c.HistoricText,
            LastAnalysisDate = c.LastAnalysisDate
        });

        return Ok(response);
    }

    /// <summary>
    /// Cadastra um novo cliente e gera seu perfil vetorial.
    /// </summary>
    /// <remarks>
    /// Este endpoint envia o texto do histórico para o modelo 'mistral-embed', 
    /// gera um vetor numérico e persiste no banco de dados para buscas futuras.
    /// </remarks>
    /// <param name="name">Nome completo do cliente.</param>
    /// <param name="score">Score financeiro tradicional (0 a 1000).</param>
    /// <param name="historic">Texto detalhado com o histórico de crédito e comportamento do cliente.</param>
    /// <response code="200">Cliente vetorizado e inserido.</response>
    /// <response code="400">Erro de validação</response>
    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest(
        string name,
        int score,
        string historic)
    {
        if (string.IsNullOrWhiteSpace(historic))
            return BadRequest(new { error = "O histórico é obrigatório para vetorização." });

        await creditAnalysisService.Ingest(name, score, historic);

        return Ok(new { message = "Cliente inserido e vetorizado." });
    }

    /// <summary>
    /// Realiza a análise cognitiva de um cliente (RAG).
    /// </summary>
    /// <remarks>
    /// Envia o histórico do cliente para a IA e processa uma pergunta específica.
    /// Exemplo: "O histórico deste cliente justifica um aumento de limite?"
    /// </remarks>
    /// <param name="id">ID numérico do cliente.</param>
    /// <param name="question">A pergunta a ser feita.</param>
    /// <response code="200">Retorna a análise da IA.</response>
    [HttpGet("{id}/analyze")]
    public async Task<IActionResult> Analyze(
        int id, [FromQuery] string question)
        => Ok(new { analysis = await creditAnalysisService.AnalyzeRisk(id, question) });

    /// <summary>
    /// Localiza clientes com perfis comportamentais similares.
    /// </summary>
    /// <remarks>
    /// Realiza uma busca por similaridade de cosseno no banco de dados. 
    /// Retorna os perfis que possuem as características financeiras mais próximas ao do cliente informado, 
    /// baseando-se no vetor gerado pelo histórico textual.
    /// </remarks>
    /// <param name="id">ID do cliente base para a comparação.</param>
    /// <response code="200">Lista de clientes semanticamente similares.</response>
    [HttpGet("{id}/similar")]
    public async Task<IActionResult> GetSimilar(int id)
    {
        var similarClients = await creditAnalysisService.GetSimilarCustomers(id);

        var response = similarClients.Select(c => new ClientResponse {
            Id = c.Id,
            Name = c.Name,
            FinancialScore = c.FinancialScore,
            HistoricText = c.HistoricText,
            LastAnalysisDate = c.LastAnalysisDate
        });

        return Ok(response);
    }


}
