using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MinimalApiNet6;
using MinimalApiNET6;
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

builder.Services.AddDbContext<DataContext>(
            dbContextOptions => dbContextOptions
                .UseMySql(builder.Configuration["DefaultConnection"], new MySqlServerVersion(new Version(8, 0, 27)))
                // The following three options help with debugging, but should
                // be changed or removed for production.
                .LogTo(Console.WriteLine, LogLevel.Information)
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors()
        );

var securityScheme = new OpenApiSecurityScheme()
{
    Name = "Authorization",
    Type = SecuritySchemeType.ApiKey,
    Scheme = "Bearer",
    BearerFormat = "JWT",
    In = ParameterLocation.Header,
    Description = "JWT Authentication for SuperHeroes."
};

var securityRequirements = new OpenApiSecurityRequirement()
{
    {
        new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        },
        new string[] {}
    }
};

var contactInfo = new OpenApiContact() 
{
    Name = "Kasarci",
    Url = new Uri("https://www.github.com/kasarci")
};

var license = new OpenApiLicense() 
{
    Name = "Free License"
};

var info = new OpenApiInfo() 
{
    Version = "V1",
    Title = "SuperHero API with JWT authentication.",
    Contact = contactInfo,
    License = license
};

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => 
{
    options.SwaggerDoc("v1", info);
    options.AddSecurityDefinition("Bearer", securityScheme);
    options.AddSecurityRequirement(securityRequirements);
});

builder.Services.AddAuthentication(options => {
   options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
   options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
   options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer (options => {
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtKey"])),
        ValidateLifetime = false, // This needs to be true for Release version. 
        ValidateIssuerSigningKey = true
    };
});

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.UseHttpsRedirection();

async Task<IEnumerable<SuperHero>> GetAllHeroes(DataContext context) =>
    await context.SuperHeroes.ToListAsync();

app.MapPost("/accounts/login", [AllowAnonymous] (UserDto user) => {
    if (user.username == "test" && user.password == "test") // TODO: add a proper login feature. 
    {
        var secureKey = Encoding.UTF8.GetBytes(builder.Configuration["JwtKey"]);

        var issuer = builder.Configuration["Jwt:Issuer"];
        var audience = builder.Configuration["Jwt:Audience"];
        var securityKey = new SymmetricSecurityKey(secureKey);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha512);

        var jwtTokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor{
            Subject = new ClaimsIdentity(new [] {
                new Claim("Id", "1"), //TODO: this id should come from the database. 
                new Claim(JwtRegisteredClaimNames.Sub, user.username),
                new Claim(JwtRegisteredClaimNames.Email, user.username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            }),
            Expires = DateTime.Now.AddMinutes(5),
            Audience = audience,
            Issuer = issuer,
            SigningCredentials = credentials
        };

        var token = jwtTokenHandler.CreateToken(tokenDescriptor); 
        var jwtToken = jwtTokenHandler.WriteToken(token);

        return Results.Ok(jwtToken);
    }
    return Results.Unauthorized();
});

app.MapGet("/", () =>
{
    logger.Information("Super Hero DB executing...");
    return "Welcome to the Super Hero DB! ❤️";
});

app.MapGet("/superhero", [Authorize] async (DataContext context) =>
    await context.SuperHeroes.ToListAsync());

app.MapGet("/superhero/{id}", [Authorize] async (DataContext context, int id) =>
    await context.SuperHeroes.FindAsync(id) is SuperHero hero ?
    Results.Ok(hero) :
    Results.NotFound("Sorry, hero not found. 😔 "));

app.MapPost("/superhero", [Authorize] async (DataContext context, SuperHero hero) =>
{
    await context.SuperHeroes.AddAsync(hero);
    await context.SaveChangesAsync();
    Results.Ok(await GetAllHeroes(context));
});

app.MapPut("/superhero/{id}", [Authorize] async (DataContext context, SuperHero hero, int id) =>
{
    var dbHero = await context.SuperHeroes.FindAsync(id);

    if (dbHero is null) return Results.NotFound("Sorry, hero not found. 😔 ");

    dbHero.Firstname = hero.Firstname;
    dbHero.Lastname = hero.Lastname;
    dbHero.Heroname = hero.Heroname;

    return Results.Ok(await GetAllHeroes(context));
});

app.MapDelete("/superhero/{id}", [Authorize] async (DataContext context, int id) =>
{
    var dbHero = await context.SuperHeroes.FindAsync(id);

    if (dbHero is null) return Results.NotFound("Sorry, hero not found. 😔 ");

    context.SuperHeroes.Remove(dbHero);
    await context.SaveChangesAsync();

    return Results.Ok(await GetAllHeroes(context));
});

app.Run();
