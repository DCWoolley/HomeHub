using HomeHub.Infrastructure.Identity;
using HomeHub.Domain.Homes;
using HomeHub.Domain.Meals;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Infrastructure.Persistence
{
	public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
	{
		public DbSet<Home> Homes => Set<Home>();
		public DbSet<Meal> Meals => Set<Meal>();
		public DbSet<PlannedMeal> PlannedMeals => Set<PlannedMeal>();
		public DbSet<HomeFloor> HomeFloors => Set<HomeFloor>();
		public DbSet<HomeRoom> HomeRooms => Set<HomeRoom>();
		public DbSet<HomeMembership> HomeMemberships => Set<HomeMembership>();
		public DbSet<HomeMember> HomeMembers => Set<HomeMember>();
		public DbSet<HomeInvitation> HomeInvitations => Set<HomeInvitation>();

		protected override void OnModelCreating(ModelBuilder builder)
		{
			base.OnModelCreating(builder);
            builder.Entity<Meal>(entity =>
            {
                entity.Property(meal => meal.Name).HasMaxLength(100);
                entity.HasOne<Home>().WithMany().HasForeignKey(meal => meal.HomeId).OnDelete(DeleteBehavior.Cascade);
            });
            builder.Entity<PlannedMeal>(entity =>
            {
                entity.HasOne<Meal>().WithMany().HasForeignKey(entry => entry.MealId).OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(entry => entry.Members).WithOne().HasForeignKey(member => member.PlannedMealId).OnDelete(DeleteBehavior.Cascade);
                entity.ToTable(table => table.HasCheckConstraint("CK_PlannedMeals_Weekday", "[Day] >= 1 AND [Day] <= 5"));
            });
            builder.Entity<PlannedMealMember>(entity =>
            {
                entity.HasKey(member => new { member.PlannedMealId, member.MemberId });
                entity.HasOne<HomeMember>().WithMany().HasForeignKey(member => member.MemberId).OnDelete(DeleteBehavior.Restrict);
            });
			builder.Entity<HomeFloor>(entity =>
			{
				entity.Property(floor => floor.Name).HasMaxLength(100);
				entity.HasOne<Home>().WithMany().HasForeignKey(floor => floor.HomeId).OnDelete(DeleteBehavior.Cascade);
			});
			builder.Entity<HomeRoom>(entity =>
			{
				entity.Property(room => room.Name).HasMaxLength(100);
				entity.HasOne<HomeFloor>().WithMany().HasForeignKey(room => room.FloorId).OnDelete(DeleteBehavior.Cascade);
			});
			builder.Entity<Home>(entity =>
			{
				entity.Property(home => home.Name).HasMaxLength(100);
				entity.Property(home => home.TimeZoneId).HasMaxLength(100);
				entity.Property(home => home.OwnerUserId).HasMaxLength(450);
				entity.HasIndex(home => home.OwnerUserId).IsUnique();
				entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(home => home.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
			});
			builder.Entity<HomeMembership>(entity =>
			{
				entity.HasKey(member => member.UserId);
				entity.Property(member => member.UserId).HasMaxLength(450);
				entity.HasIndex(member => member.MemberId).IsUnique();
				entity.HasOne<HomeMember>().WithMany().HasForeignKey(member => member.MemberId).OnDelete(DeleteBehavior.Restrict);
				entity.HasOne<Home>().WithMany().HasForeignKey(member => member.HomeId).OnDelete(DeleteBehavior.Cascade);
				entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(member => member.UserId).OnDelete(DeleteBehavior.Cascade);
			});
			builder.Entity<HomeMember>(entity =>
			{
				entity.Property(member => member.DisplayName).HasMaxLength(100);
				entity.Property(member => member.Colour).HasMaxLength(7);
				entity.HasOne<Home>().WithMany().HasForeignKey(member => member.HomeId).OnDelete(DeleteBehavior.Restrict);
			});
			builder.Entity<HomeInvitation>(entity =>
			{
				entity.Property(invite => invite.CodeHash).HasMaxLength(64);
				entity.HasIndex(invite => invite.CodeHash).IsUnique();
				entity.Property(invite => invite.Used).IsConcurrencyToken();
				entity.Property(invite => invite.Revoked).IsConcurrencyToken();
				entity.HasOne<Home>().WithMany().HasForeignKey(invite => invite.HomeId).OnDelete(DeleteBehavior.Cascade);
			});
		}
	}
}
