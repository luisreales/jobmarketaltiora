using backend.Application.Interfaces;
using backend.Infrastructure.Data;
using backend.Infrastructure.Repositories;
using backend.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient();

builder.Services.AddScoped<IHotelRepository, HotelRepository>();
builder.Services.AddScoped<IHotelPriceRepository, HotelPriceRepository>();

builder.Services.AddScoped<IBookingPlaywrightService, BookingPlaywrightService>();
builder.Services.AddScoped<IBookingApiInterceptorService, BookingApiInterceptorService>();
builder.Services.AddScoped<IBookingHybridService, BookingHybridService>();
builder.Services.AddScoped<IPriceTrackingService, PriceTrackingService>();
builder.Services.AddScoped<IDealAnalysisService, DealAnalysisService>();
builder.Services.AddHostedService<DailyScrapingHostedService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
    await DatabaseSchemaUpdater.EnsureTraceabilityColumnsAsync(dbContext);
    await DatabaseSeeder.SeedAsync(dbContext);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();
app.Run();
