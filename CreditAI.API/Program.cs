using System.ClientModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using OpenAI;
using CreditAI.API.Infrastructure.Data;
using CreditAI.API.Services;

namespace CreditAI.API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Buscando a chave da Mistral no appsettings.json
            var apiKey = builder.Configuration["Mistral:ApiKey"] ?? throw new ArgumentNullException("Mistral Key is missing");
            var mistralEndpoint = new Uri("https://api.mistral.ai/v1");

            builder.Services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            var mistralClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions
            {
                Endpoint = mistralEndpoint
            });

            // Configuração do Gerador de Embeddings para o modelo da Mistral
            builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
                mistralClient.GetEmbeddingClient("mistral-embed")
                            .AsIEmbeddingGenerator());

            // Configuração do Semantic Kernel para Chat
            builder.Services.AddScoped(sp =>
            {
                var kBuilder = Kernel.CreateBuilder();
                kBuilder.AddOpenAIChatCompletion("mistral-small-latest", apiKey, httpClient: new HttpClient { BaseAddress = mistralEndpoint });
                return kBuilder.Build();
            });

            builder.Services.AddScoped<CreditAnalysisService>();
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();

            await app.RunAsync();
        }
    }
}