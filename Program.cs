using Microsoft.EntityFrameworkCore;
using MinimalApiNet6;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

//Adding logger to the app.
var logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(logger); 

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<DataContext>(
            dbContextOptions => dbContextOptions
                .UseMySql(builder.Configuration["DefaultConnection"], new MySqlServerVersion(new Version(8, 0, 27)))
                // The following three options help with debugging, but should
                // be changed or removed for production.
                .LogTo(Console.WriteLine, LogLevel.Information)
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors()
        );

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

async Task<IEnumerable<SuperHero>> GetAllHeroes(DataContext context) => 
    await context.SuperHeroes.ToListAsync();

app.MapGet("/", () => {
    logger.Information("Super Hero DB executing...");
    return "Welcome to the Super Hero DB! ❤️";
});

app.MapGet("/superhero", async (DataContext context) =>
    await context.SuperHeroes.ToListAsync());

app.MapGet("/superhero/{id}", async(DataContext context, int id) =>
    await context.SuperHeroes.FindAsync(id) is SuperHero hero ? 
    Results.Ok(hero) :
    Results.NotFound("Sorry, hero not found. 😔 "));

app.MapPost("/superhero", async (DataContext context, SuperHero hero) => {
    await context.SuperHeroes.AddAsync(hero);
    await context.SaveChangesAsync();
    Results.Ok(await GetAllHeroes(context));
});

app.MapPut("/superhero/{id}", async (DataContext context, SuperHero hero, int id) => {
    var dbHero = await context.SuperHeroes.FindAsync(id);
    
    if(dbHero is null) return Results.NotFound("Sorry, hero not found. 😔 ");

    dbHero.Firstname = hero.Firstname;
    dbHero.Lastname = hero.Lastname;
    dbHero.Heroname = hero.Heroname;

    return Results.Ok(await GetAllHeroes(context));
});

app.MapDelete("/superhero/{id}", async (DataContext context, int id) => {
    var dbHero = await context.SuperHeroes.FindAsync(id);
    
    if(dbHero is null) return Results.NotFound("Sorry, hero not found. 😔 ");

    context.SuperHeroes.Remove(dbHero);
    await context.SaveChangesAsync();
    
    return Results.Ok(await GetAllHeroes(context));
});

app.Run();
