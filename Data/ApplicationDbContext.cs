using Microsoft.EntityFrameworkCore;

namespace Skill_Hub_BackEnd.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Add your DbSets here
        // public DbSet<User> Users { get; set; }
    }
}
