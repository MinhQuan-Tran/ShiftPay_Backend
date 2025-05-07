using Microsoft.EntityFrameworkCore;
using ShiftPay_Backend.Models;

namespace ShiftPay_Backend.Data
{
    public class ShiftPay_BackendContext : DbContext
    {
        private readonly IConfiguration _configuration;

        public ShiftPay_BackendContext(DbContextOptions<ShiftPay_BackendContext> options, IConfiguration configuration)
            : base(options)
        {
            _configuration = configuration;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Shift>()
                .ToContainer("Shifts")
                .HasPartitionKey(e => new { e.UserId, e.YearMonth, e.Day });
        }

        public DbSet<Shift> Shifts { get; set; } = default!;
    }
}
