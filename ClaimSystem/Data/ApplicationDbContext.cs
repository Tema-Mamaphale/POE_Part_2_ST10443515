using Microsoft.EntityFrameworkCore;
using ClaimSystem.Models;

namespace ClaimSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Claim> Claims => Set<Claim>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Claim>(e =>
            {
                e.Property(x => x.LecturerName).HasMaxLength(120).IsRequired();
                e.Property(x => x.Month).HasMaxLength(40).IsRequired();
                e.Property(x => x.HoursWorked).HasColumnType("decimal(10,2)");
                e.Property(x => x.HourlyRate).HasColumnType("decimal(10,2)");
                e.Property(x => x.Status).HasConversion<int>();
            });
        }
    }
}
