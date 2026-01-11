using Vakt.Intelligence.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
// builder.Services.AddOpenApi();

// Register Services
builder.Services.Configure<Vakt.Core.Options.SemanticCacheOptions>(builder.Configuration.GetSection("SemanticCache")); // Use string logic or reference options
// Better to use strong type if referenced
builder.Services.Configure<Vakt.Core.Options.SemanticCacheOptions>(builder.Configuration.GetSection(Vakt.Core.Options.SemanticCacheOptions.SectionName));

builder.Services.AddSingleton<EmbeddingService>();
// ... (rest of service registrations)

var app = builder.Build();

app.MapPost("/embeddings", (EmbeddingService embedder, [FromBody] string text) => 
{
    var vector = embedder.GenerateEmbedding(text);
    return Results.Ok(vector);
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();