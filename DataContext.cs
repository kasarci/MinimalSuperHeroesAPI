using Microsoft.EntityFrameworkCore;

namespace MinimalApiNet6;

public class DataContext : DbContext
{
    public DataContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<SuperHero> SuperHeroes => Set<SuperHero>();
}