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
    /// Retorna uma lista paginada de clientes para fins de conferência e visualização.
    /// Use os parâmetros <paramref name="page"/> e <paramref name="pageSize"/> para controlar a paginação.
    /// </remarks>
    /// <param name="page">Número da página a ser retornada. Valor padrão é 1.</param>
    /// <param name="pageSize">Quantidade de clientes por página. Valor padrão é 20.</param>
    /// <param name="ct">Token de cancelamento para abortar a requisição.</param>
    /// <response code="200">Lista paginada de clientes.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ClientResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClients(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var response = await creditAnalysisService
                                .GetAllClients(page, pageSize, ct);
        return Ok(response);
    }

    /// <summary>
    /// Obtém um cliente específico pelo seu identificador público (GUID).
    /// </summary>
    /// <param name="id">Identificador público (GUID) do cliente.</param>
    /// <param name="ct">Token de cancelamento para abortar a requisição.</param>
    /// <response code="200">Retorna o cliente solicitado.</response>
    /// <response code="404">Cliente não encontrado.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientResponse>> GetById(
        Guid id,
        CancellationToken ct)
    {
        var client = await creditAnalysisService.GetById(id, ct);
        return Ok(client);
    }

    /// <summary>
    /// Cadastra um novo cliente e gera seu perfil vetorial.
    /// </summary>
    /// <remarks>
    /// Recebe os dados do cliente, envia o histórico textual para geração de embeddings 
    /// e persiste as informações no banco de dados.
    /// Retorna o cliente recém-criado com seu GUID público.
    /// </remarks>
    /// <param name="request">Objeto contendo os dados do cliente.</param>
    /// <param name="ct">Token de cancelamento para abortar a requisição.</param>
    /// <response code="201">Cliente inserido e vetorizado com sucesso.</response>
    /// <response code="400">Erro de validação do request.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Ingest(
        [FromBody] ClientRequest request,
        CancellationToken ct)
    {
        var response = await creditAnalysisService.Ingest(request, ct);

        return CreatedAtAction(nameof(GetById), new { id = response.PublicId }, response);
    }

    /// <summary>
    /// Realiza a análise cognitiva de risco de crédito de um cliente.
    /// </summary>
    /// <remarks>
    /// Envia o histórico do cliente para o modelo de linguagem e retorna insights técnicos sobre o risco financeiro.
    /// Perguntas podem ser como: "O histórico deste cliente justifica um aumento de limite?"
    /// </remarks>
    /// <param name="id">Identificador público (GUID) do cliente.</param>
    /// <param name="question">Pergunta a ser enviada para análise pelo modelo de IA.</param>
    /// <param name="ct">Token de cancelamento para abortar a requisição.</param>
    /// <response code="200">Retorna a análise de risco do cliente.</response>
    /// <response code="400">Pergunta inválida (vazia ou nula).</response>
    /// <response code="404">Cliente não encontrado.</response>
    [HttpGet("{id:guid}/analyze")]
    [ProducesResponseType(typeof(RiskAnalysisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Analyze(
        Guid id,
        [FromQuery] string question,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(question))
            return BadRequest("A pergunta não pode ser vazia.");

        var result = await creditAnalysisService.AnalyzeRisk(id, question, ct);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    /// <summary>
    /// Localiza clientes com perfis comportamentais similares.
    /// </summary>
    /// <remarks>
    /// Realiza uma busca por similaridade de cosseno no histórico vetorial do cliente informado.
    /// Retorna os perfis com características financeiras mais próximas.
    /// </remarks>
    /// <param name="id">Identificador do cliente base para a comparação.</param>
    /// <param name="limit">Número máximo de clientes similares a serem retornados. Limite entre 1 e 20.</param>
    /// <param name="ct">Token de cancelamento para abortar a requisição.</param>
    /// <response code="200">Lista de clientes semanticamente similares.</response>
    /// <response code="404">Nenhum cliente similar encontrado.</response>
    [HttpGet("{id:guid}/similar")]
    [ProducesResponseType(typeof(IEnumerable<ClientResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSimilar(
        Guid id,
        [FromQuery] int limit = 5,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 20); // Limita o número de resultados entre 1 e 20

        var response = await creditAnalysisService
                                .GetSimilarCustomers(id, limit, ct);

        if (response.Count == 0)
            return NotFound();

        return Ok(response);
    }


}
