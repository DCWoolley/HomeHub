using HomeHub.Web.Modules.Households;
using HomeHub.Web.Modules.MealPlanner;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HomeHub.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<HouseholdMembership> HouseholdMemberships => Set<HouseholdMembership>();
    public DbSet<HouseholdInvitation> HouseholdInvitations => Set<HouseholdInvitation>();
    public DbSet<Meal> Meals => Set<Meal>();
    public DbSet<PlannedMeal> PlannedMeals => Set<PlannedMeal>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Meal>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => new { x.HouseholdId, x.NormalizedName }).IsUnique();
            entity.HasAlternateKey(x => new { x.HouseholdId, x.Id });
            entity.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PlannedMeal>(entity =>
        {
            entity.ToTable("MealScheduleEntries");
            entity.HasIndex(x => new { x.HouseholdId, x.Day, x.Time, x.MealId }).IsUnique();
            entity.HasOne(x => x.Meal).WithMany().HasForeignKey(x => new { x.HouseholdId, x.MealId })
                .HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<Household>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Version).IsConcurrencyToken();
        });
        builder.Entity<HouseholdMember>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.HasAlternateKey(x => new { x.HouseholdId, x.Id });
            entity.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<HouseholdMembership>(entity =>
        {
            entity.HasKey(x => x.UserId);
            entity.HasIndex(x => x.MemberId).IsUnique();
            entity.HasIndex(x => x.HouseholdId).IsUnique().HasFilter("[IsOwner] = 1");
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<HouseholdMember>().WithMany().HasForeignKey(x => new { x.HouseholdId, x.MemberId })
                .HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<HouseholdInvitation>(entity =>
        {
            entity.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.CodeHash).IsUnique();
            entity.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
