using FlowMaker.Persistence.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;

namespace Test1.Data
{
    public class UIDbContext(IFlowMakerDbContextBuilder flowMakerDbContextBuilder) : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            flowMakerDbContextBuilder.OnConfiguring(optionsBuilder);
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            flowMakerDbContextBuilder.OnModelCreating(modelBuilder);
        }
    }
}
