using Microsoft.AspNetCore.Mvc;
using CreditAI.API.Services;
using CreditAI.API.DTO;

namespace CreditAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientsController(CreditAnalysisService creditAnalysisService): ControllerBase
{
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

    [HttpGet("{id}/analyze")]
    public async Task<IActionResult> Analyze(
        int id, [FromQuery] string question)
        => Ok(new { analysis = await creditAnalysisService.AnalyzeRisk(id, question) });

    [HttpGet("{id}/similar")]
    public async Task<IActionResult> GetSimilar(int id)
    {
        var similarClients = await creditAnalysisService.GetSimilarCustomers(id);

        var response = similarClients.Select(c => new ClientResponse(
            c.Id,
            c.Name,
            c.FinancialScore,
            c.HistoricText,
            c.LastAnalysisDate
        ));

        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var clients = await creditAnalysisService.GetAllClients();
        var response = clients.Select(c => new ClientResponse(
            c.Id,
            c.Name,
            c.FinancialScore,
            c.HistoricText,
            c.LastAnalysisDate
        ));
        return Ok(response);
    }
}
