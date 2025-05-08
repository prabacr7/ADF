using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace DataTransfer.Infrastructure.Data
{
    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context, ILogger logger)
        {
            try
            {
                // Ensure database is created
                context.Database.EnsureCreated();
                
                logger.LogInformation("Database initialization completed successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while initializing the database.");
                throw;
            }
        }
        
        public static void InitializeDatabase(this IServiceProvider serviceProvider)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {
                    var context = services.GetRequiredService<ApplicationDbContext>();
                    var logger = services.GetRequiredService<ILogger<ApplicationDbContext>>();
                    
                    logger.LogInformation("Initializing database...");
                    Initialize(context, logger);
                }
                catch (Exception ex)
                {
                    var logger = services.GetRequiredService<ILogger<ApplicationDbContext>>();
                    logger.LogError(ex, "An error occurred while initializing the database.");
                }
            }
        }
    }
} 