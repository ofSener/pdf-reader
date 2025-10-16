using Hangfire;
using Hangfire.Dashboard;
using Hangfire.InMemory;
using Hangfire.Redis.StackExchange;
using Pdf_Reader.Middleware;
using Pdf_Reader.Services;
using Pdf_Reader.Services.Extractors;
using Serilog;
using StackExchange.Redis;

namespace Pdf_Reader
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Serilog yapılandırması
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    path: "logs/pdf-reader-.txt",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            try
            {
                Log.Information("PDF Reader API başlatılıyor...");

                var builder = WebApplication.CreateBuilder(args);

                // Serilog'u builder'a ekle
                builder.Host.UseSerilog();

                // Add services to the container
                builder.Services.AddControllers();

                // Swagger/OpenAPI yapılandırması
                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                    {
                        Title = "PDF Policy Extractor API",
                        Version = "v1",
                        Description = "Sigorta poliçesi PDF'lerinden veri çıkarma API'si"
                    });
                });

                // CORS yapılandırması (gerekirse)
                builder.Services.AddCors(options =>
                {
                    options.AddPolicy("AllowAll", policy =>
                    {
                        policy.AllowAnyOrigin()
                              .AllowAnyMethod()
                              .AllowAnyHeader();
                    });
                });

                // Dependency Injection - Servisler
                builder.Services.AddSingleton<PdfTextExtractorService>();
                builder.Services.AddSingleton<CompanyDetectorService>();
                builder.Services.AddSingleton<ValidationService>();

                // Field Extractors
                builder.Services.AddSingleton<DateExtractor>();
                builder.Services.AddSingleton<MoneyExtractor>();
                builder.Services.AddSingleton<PolicyNumberExtractor>();
                builder.Services.AddSingleton<PlateNumberExtractor>();
                builder.Services.AddSingleton<NameExtractor>();
                builder.Services.AddSingleton<TcNoExtractor>();

                // Orchestrator
                builder.Services.AddSingleton<ExtractorOrchestrator>();

                // Batch Processing Job Service
                builder.Services.AddScoped<BatchProcessingJobService>();

                // Redis Connection (Hangfire için gerekli - opsiyonel, production'da kullanılır)
                // Development'ta Redis yoksa Hangfire in-memory storage kullanır
                var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
                IConnectionMultiplexer? redisConnection = null;

                try
                {
                    if (!string.IsNullOrEmpty(redisConnectionString))
                    {
                        redisConnection = ConnectionMultiplexer.Connect(redisConnectionString);
                        builder.Services.AddSingleton(redisConnection);
                        Log.Information("Redis bağlantısı başarılı: {Redis}", redisConnectionString);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Redis bağlantısı kurulamadı. In-memory storage kullanılacak.");
                }

                // Hangfire yapılandırması
                builder.Services.AddHangfire(config =>
                {
                    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                          .UseSimpleAssemblyNameTypeSerializer()
                          .UseRecommendedSerializerSettings();

                    if (redisConnection != null)
                    {
                        // Production: Redis storage
                        config.UseRedisStorage(redisConnection, new RedisStorageOptions
                        {
                            Prefix = "hangfire:pdfreader:",
                            InvisibilityTimeout = TimeSpan.FromMinutes(30),
                            ExpiryCheckInterval = TimeSpan.FromHours(1)
                        });
                        Log.Information("Hangfire Redis storage aktif");
                    }
                    else
                    {
                        // Development: In-memory storage
                        config.UseInMemoryStorage();
                        Log.Warning("Hangfire in-memory storage kullanılıyor (sadece development)");
                    }
                });

                // Hangfire Server
                var workerCount = builder.Configuration.GetValue<int>("Hangfire:WorkerCount", Environment.ProcessorCount);
                builder.Services.AddHangfireServer(options =>
                {
                    options.WorkerCount = workerCount;
                    options.SchedulePollingInterval = TimeSpan.FromSeconds(1);
                    options.ServerName = $"{Environment.MachineName}-pdfreader";
                });

                Log.Information($"Hangfire server yapılandırıldı: {workerCount} worker");

                var app = builder.Build();

                // Configure the HTTP request pipeline

                // Global exception handling middleware (en üstte olmalı)
                app.UseGlobalExceptionHandler();

                // Swagger (development ve production'da)
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "PDF Policy Extractor API v1");
                    c.RoutePrefix = string.Empty; // Swagger UI root'ta açılsın
                });

                // Hangfire Dashboard
                app.UseHangfireDashboard("/hangfire", new DashboardOptions
                {
                    Authorization = new[] { new HangfireAuthorizationFilter() },
                    DashboardTitle = "PDF Reader - Background Jobs"
                });

                app.UseHttpsRedirection();

                // CORS
                app.UseCors("AllowAll");

                app.UseAuthorization();

                app.MapControllers();

                Log.Information("PDF Reader API başarıyla başlatıldı");
                Log.Information("Swagger UI: http://localhost:5000 veya https://localhost:5001");

                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Uygulama başlatılırken kritik hata oluştu");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }

    /// <summary>
    /// Hangfire Dashboard Authorization Filter
    /// Production'da gerçek authentication ekleyin (API Key, JWT, vb.)
    /// </summary>
    public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();

            // Development: Herkese izin ver
            // Production: Gerçek authentication ekleyin
            // Örnek: API key check, JWT token check, IP whitelist, vb.

            // TODO: Production'da bu kısmı güvenli authentication ile değiştirin
            // Örnek: return httpContext.User.Identity?.IsAuthenticated ?? false;

            return true; // Development için herkese açık
        }
    }
}
