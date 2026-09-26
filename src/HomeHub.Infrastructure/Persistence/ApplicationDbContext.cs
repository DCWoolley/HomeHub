using HomeHub.Infrastructure.Identity;
using HomeHub.Domain.Homes;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Infrastructure.Persistence
{
	public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
	{
		public DbSet<Home> Homes => Set<Home>();
		public DbSet<HomeMembership> HomeMemberships => Set<HomeMembership>();
		public DbSet<HomeInvitation> HomeInvitations => Set<HomeInvitation>();

		protected override void OnModelCreating(ModelBuilder builder)
		{
			base.OnModelCreating(builder);
			builder.Entity<Home>(entity =>
			{
				entity.Property(home => home.Name).HasMaxLength(100);
				entity.Property(home => home.OwnerUserId).HasMaxLength(450);
				entity.HasIndex(home => home.OwnerUserId).IsUnique();
				entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(home => home.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
			});
			builder.Entity<HomeMembership>(entity =>
			{
				entity.HasKey(member => member.UserId);
				entity.Property(member => member.UserId).HasMaxLength(450);
				entity.HasOne<Home>().WithMany().HasForeignKey(member => member.HomeId).OnDelete(DeleteBehavior.Cascade);
				entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(member => member.UserId).OnDelete(DeleteBehavior.Cascade);
			});
			builder.Entity<HomeInvitation>(entity =>
			{
				entity.Property(invite => invite.CodeHash).HasMaxLength(64);
				entity.HasIndex(invite => invite.CodeHash).IsUnique();
				entity.Property(invite => invite.Used).IsConcurrencyToken();
				entity.HasOne<Home>().WithMany().HasForeignKey(invite => invite.HomeId).OnDelete(DeleteBehavior.Cascade);
			});
		}
	}
}
