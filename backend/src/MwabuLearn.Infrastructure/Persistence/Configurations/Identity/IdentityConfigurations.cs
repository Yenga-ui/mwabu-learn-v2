using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MwabuLearn.Domain.Entities.Organisations;
using MwabuLearn.Infrastructure.Identity;

namespace MwabuLearn.Infrastructure.Persistence.Configurations.Identity;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
        b.Property(x => x.LastName).IsRequired().HasMaxLength(100);
        b.Property(x => x.PhoneNumber).HasMaxLength(30);
        b.Property(x => x.Email).IsRequired().HasMaxLength(256);
        b.Property(x => x.NormalizedEmail).IsRequired().HasMaxLength(256);
        b.HasIndex(x => x.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();
        b.HasIndex(x => new { x.IsActive, x.Id });
    }
}
public sealed class OrganisationConfiguration : IEntityTypeConfiguration<Organisation>
{
    public void Configure(EntityTypeBuilder<Organisation> b)
    {
        b.ToTable("Organisations", t => t.HasCheckConstraint("CK_Organisations_NotSelfParent", "\"ParentOrganisationId\" IS NULL OR \"ParentOrganisationId\" <> \"Id\""));
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Code).IsRequired().HasMaxLength(50);
        b.Property(x => x.OrganisationType).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(x => x.UpdatedAt).IsConcurrencyToken();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => new { x.IsActive, x.Name });
        b.HasOne(x => x.ParentOrganisation).WithMany().HasForeignKey(x => x.ParentOrganisationId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class OrganisationMembershipConfiguration : IEntityTypeConfiguration<OrganisationMembership>
{
    public void Configure(EntityTypeBuilder<OrganisationMembership> b)
    {
        b.ToTable("OrganisationMemberships");
        b.HasKey(x => x.Id);
        b.Property(x => x.UpdatedAt).IsConcurrencyToken();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Organisation).WithMany(x => x.Memberships).HasForeignKey(x => x.OrganisationId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.UserId, x.OrganisationId }).IsUnique();
        b.HasIndex(x => new { x.OrganisationId, x.IsActive });
    }
}
public sealed class OrganisationRoleConfiguration : IEntityTypeConfiguration<OrganisationRole>
{
    public void Configure(EntityTypeBuilder<OrganisationRole> b)
    {
        b.ToTable("OrganisationRoles"); b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(50);
        b.Property(x => x.Name).IsRequired().HasMaxLength(100);
        b.HasIndex(x => x.Code).IsUnique();
    }
}
public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.ToTable("Permissions"); b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(100);
        b.Property(x => x.Name).IsRequired().HasMaxLength(150);
        b.HasIndex(x => x.Code).IsUnique();
    }
}
public sealed class OrganisationMembershipRoleConfiguration : IEntityTypeConfiguration<OrganisationMembershipRole>
{
    public void Configure(EntityTypeBuilder<OrganisationMembershipRole> b)
    {
        b.ToTable("OrganisationMembershipRoles"); b.HasKey(x => x.Id);
        b.HasOne(x => x.Membership).WithMany(x => x.Roles).HasForeignKey(x => x.OrganisationMembershipId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganisationMembershipId, x.RoleId }).IsUnique();
    }
}
public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("RolePermissions"); b.HasKey(x => x.Id);
        b.HasOne(x => x.Role).WithMany(x => x.Permissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();
    }
}
