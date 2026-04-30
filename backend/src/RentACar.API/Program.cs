using RentACar.API.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiApplicationServices(builder.Configuration, builder.Environment);

var app = builder.Build();
await app.InitializeApiAsync();

app.Run();

public partial class Program;
