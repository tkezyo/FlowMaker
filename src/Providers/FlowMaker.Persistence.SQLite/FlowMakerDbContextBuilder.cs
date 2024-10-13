using FlowMaker.Persistence.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowMaker.Persistence.SQLite
{
    public class FlowMakerDbContextBuilder(IOptions<SQLiteOptions> options) : IFlowMakerDbContextBuilder
    {
        public void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder
              .UseSqlite(options.Value.ConnectString)
              .LogTo(Console.WriteLine, LogLevel.Information);
            ;
        }

        public void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FlowDefinitionModel>().ToTable("Flows")
                .HasKey(c => c.Id);
            modelBuilder.Entity<ConfigDefinitionModel>().ToTable("Configs")
                .HasKey(c => c.Id);
        }
    }
}
