using RentACar.Worker;
using RentACar.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<DailyBackupOptions>(builder.Configuration.GetSection("BackgroundJobs:DailyBackup"));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
