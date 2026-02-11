using Microsoft.AspNetCore.Mvc;
using CreditAI.API.Services;
using CreditAI.API.DTO;
using System.ComponentModel.DataAnnotations;

namespace CreditAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientsController(CreditAnalysisService creditAnalysisService): ControllerBase
{
    /// <summary>
    /// Retorna lista paginada de clientes
    /// </summary>
    /// <param name="query">
    /// Parâmetros de paginação (página e quantidade por página).
    /// </param>
    /// <param name="ct">
    /// Token de cancelamento da requisição.
    /// </param>
    /// <response code="200">
    /// Lista paginada de clientes.
    /// </response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ClientResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ClientResponse>>> GetClients(
    [FromQuery] PaginationQuery query,
    CancellationToken ct)
    {
        query = query with
        {
            Page = Math.Max(query.Page, 1),
            PageSize = Math.Clamp(query.PageSize, 1, 100)
        };

        return await creditAnalysisService
                        .GetAllClients(query.Page, query.PageSize, ct);
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

        return client is null ? NotFound(): Ok(client);
    }

    ///// <summary>
    ///// Cadastra um novo cliente e gera seu perfil vetorial.
    ///// </summary>
    ///// <remarks>
    ///// Recebe os dados do cliente, envia o histórico textual para geração de embeddings 
    ///// e persiste as informações no banco de dados.
    ///// Retorna o cliente recém-criado com seu GUID público.
    ///// </remarks>
    ///// <param name="request">Objeto contendo os dados do cliente.</param>
    ///// <param name="ct">Token de cancelamento para abortar a requisição.</param>
    ///// <response code="201">Cliente inserido e vetorizado com sucesso.</response>
    ///// <response code="400">Erro de validação do request.</response>
    //[HttpPost]
    //[ProducesResponseType(typeof(ClientResponse), StatusCodes.Status201Created)]
    //[ProducesResponseType(StatusCodes.Status400BadRequest)]
    //[ProducesResponseType(StatusCodes.Status409Conflict)]
    //public async Task<IActionResult> Create(
    //    [FromBody] ClientRequest request,
    //    CancellationToken ct)
    //{
    //    var response = await creditAnalysisService.Ingest(request, ct);

    //    return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    //}

    /// <summary>
    /// Cadastra um novo cliente utilizando IA para processar dados brutos.
    /// </summary>
    /// <remarks>
    /// - Recebe um JSON "cru" (logs, transações, perfil de Open Finance).
    /// - Utiliza um LLM (Semantic Kernel) para interpretar o comportamento e gerar um histórico narrativo.
    /// - Vetoriza esse histórico automaticamente para permitir buscas por similaridade.
    /// 
    /// Útil para automatizar a criação de perfis sem intervenção humana manual.
    /// </remarks>
    /// <param name="request">Objeto contendo o nome, score e o JSON dinâmico (RawData) do cliente.</param>
    /// <param name="ct">Token de cancelamento da requisição.</param>
    /// <response code="201">Cliente processado pela IA, vetorizado e persistido com sucesso.</response>
    /// <response code="400">Dados brutos inválidos ou erro no processamento do modelo de linguagem.</response>
    [HttpPost("smart")] // Se quiser forçar a ordem, use: [HttpPost("smart", Order = 3)]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSmart(
    [FromBody] SmartClientRequest request,
    CancellationToken ct)
    {
        // Este endpoint demonstra a IA transformando dados brutos em conhecimento
        var response = await creditAnalysisService.IngestSmart(request, ct);

        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }


    /// <summary>
    /// Realiza a análise de risco de crédito de um cliente.
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
        [FromQuery][Required] string question,
        CancellationToken ct)
    {

        var result = await creditAnalysisService.AnalyzeRisk(id, question, ct);
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

        return Ok(response);
    }


}
