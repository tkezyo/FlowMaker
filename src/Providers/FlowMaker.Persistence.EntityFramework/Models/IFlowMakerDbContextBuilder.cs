using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowMaker.Persistence.EntityFramework.Models
{
    public interface IFlowMakerDbContextBuilder
    {
        void OnConfiguring(DbContextOptionsBuilder optionsBuilder);
        void OnModelCreating(ModelBuilder modelBuilder);
    }
}
