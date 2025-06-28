// Kræver dotnet 10
// dotner run Kristoffer.cs
#:package Microsoft.EntityFrameworkCore.Design@10.0.0-preview.5.25277.114
#:package Npgsql.EntityFrameworkCore.PostgreSQL@10.0.0-preview.5
#:package Microsoft.EntityFrameworkCore@10.0.0-preview.5.25277.114
#:package Testcontainers@4.6.0
#:package Testcontainers.PostgreSql@4.6.0

// .NET Usings
using Microsoft.EntityFrameworkCore;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging;

// --- Application Logic ---

Console.WriteLine("🚀 Starting PostgreSQL container...");

await using var postgresContainer = new ContainerBuilder()
    .WithImage("postgres:16-alpine")
    .WithPortBinding(5432, true) // Map to a random host port
    .WithEnvironment("POSTGRES_DB", "mydatabase")
    .WithEnvironment("POSTGRES_USER", "myuser")
    .WithEnvironment("POSTGRES_PASSWORD", "mypassword")
    .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("pg_isready -U myuser -d mydatabase"))
    .Build();

await postgresContainer.StartAsync();

var connectionString = $"Host={postgresContainer.Hostname};Port={postgresContainer.GetMappedPublicPort(5432)};Database=mydatabase;Username=myuser;Password=mypassword;Include Error Detail=true;";

Console.WriteLine($"✅ PostgreSQL container running on: {postgresContainer.Hostname}:{postgresContainer.GetMappedPublicPort(5432)}");

// Configure and use the database context
var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(connectionString)
    .EnableDetailedErrors().
            EnableSensitiveDataLogging().
            LogTo(Console.WriteLine,
                LogLevel.Information)
    .Options;

await using var dbContext = new AppDbContext(options);
await dbContext.Database.EnsureCreatedAsync();

// --- Data Operations ---

Console.WriteLine("\nAdding a new product...");
var newProduct = new Product { Name = "Keyboard", Price = 75.00m };
dbContext.Products.Add(newProduct);
await dbContext.SaveChangesAsync();
Console.WriteLine($"Added product: '{newProduct.Name}'");

Console.WriteLine("\nFetching all products...");
var products = await dbContext.Products.TagWith("Hello").TagWith("World").ToListAsync();
foreach (var product in products)
{
    Console.WriteLine($"  - ID: {product.Id}, Name: {product.Name}, Price: {product.Price:C}");
}

Console.WriteLine("\nTestcontainers operation completed. Stopping container...");


// --- Entity and DbContext Definitions ---

public class Product
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
}

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Seed initial data
        modelBuilder.Entity<Product>().HasData(
            new Product { Id = 1, Name = "Laptop", Price = 1200.00m },
            new Product { Id = 2, Name = "Mouse", Price = 25.00m }
        );
    }
}