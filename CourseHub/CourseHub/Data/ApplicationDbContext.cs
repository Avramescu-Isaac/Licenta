using CourseHub.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CourseHub.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<Message> Messages { get; set; }
        public DbSet<HireRequest> HireRequests { get; set; }
        public DbSet<Topic> Topics { get; set; }
        public DbSet<OtpCode> OtpCodes { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>()
                .HasIndex(u => u.Email)
                .IsUnique();

            builder.Entity<Topic>().HasData(
                new Topic { Id = 1, Name = "Mathematics", Description = "Algebra, Calculus, Geometry" },
                new Topic { Id = 2, Name = "Science", Description = "Physics, Chemistry, Biology" },
                new Topic { Id = 3, Name = "Programming", Description = "C#, Python, JavaScript" },
                new Topic { Id = 4, Name = "Languages", Description = "English, Spanish, French" },
                new Topic { Id = 5, Name = "Arts", Description = "Drawing, Painting, Music" },
                new Topic { Id = 6, Name = "Business", Description = "Marketing, Finance, Management" }
            );

            builder.Entity<HireRequest>()
                .HasOne(h => h.Requester)
                .WithMany()
                .HasForeignKey(h => h.RequesterId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<HireRequest>()
                .HasOne(h => h.Target)
                .WithMany()
                .HasForeignKey(h => h.TargetId)
                .OnDelete(DeleteBehavior.Restrict);
        }

    }
}

