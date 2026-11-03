using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Data.Configurations;

public sealed class AuthenticationCodeEntityConfiguration
    : IEntityTypeConfiguration<AuthenticationCode>
{
    public void Configure(EntityTypeBuilder<AuthenticationCode> entity)
    {
        entity.HasIndex(authenticationCode => new
        {
            authenticationCode.UserId,
            authenticationCode.CodeType,
            authenticationCode.ExpiresAt
        },
                "IX_AuthenticationCodes_User_Type_Expiry");

        entity.Property(authenticationCode => authenticationCode.CodeHash)
            .HasMaxLength(255)
            .IsUnicode(false);
        entity.Property(authenticationCode => authenticationCode.CodeType)
            .HasMaxLength(30)
            .IsUnicode(false);
        entity.Property(authenticationCode => authenticationCode.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("(sysutcdatetime())", "DF_AuthenticationCodes_CreatedAt");
        entity.Property(authenticationCode => authenticationCode.ExpiresAt).HasPrecision(0);
        entity.Property(authenticationCode => authenticationCode.UsedAt).HasPrecision(0);

        entity.HasOne(dependent => dependent.User)
            .WithMany(principal => principal.AuthenticationCodes)
            .HasForeignKey(dependent => dependent.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_AuthenticationCodes_User");
    }
}
