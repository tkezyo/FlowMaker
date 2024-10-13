using Microsoft.EntityFrameworkCore;

namespace FlowMaker.Persistence.EntityFramework.Models
{
    public class FlowMakerDbContext(IFlowMakerDbContextBuilder flowMakerDbContextBuilder) : DbContext
    {
        public DbSet<FlowDefinitionModel> Flows { get; set; }
        public DbSet<ConfigDefinitionModel> Configs { get; set; }
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
