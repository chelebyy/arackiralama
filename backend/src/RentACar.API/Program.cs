using RentACar.API.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiApplicationServices(builder.Configuration);

var app = builder.Build();
await app.InitializeApiAsync();

app.Run();
