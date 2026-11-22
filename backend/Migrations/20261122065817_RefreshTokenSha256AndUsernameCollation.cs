using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JwtDemo.Migrations;

/// <inheritdoc />
public partial class _20261122065817_RefreshTokenSha256AndUsernameCollation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM ActiveRefreshTokens;");

        migrationBuilder.DropColumn(
            name: "TokenSalt",
            table: "ActiveRefreshTokens");

        migrationBuilder.AlterColumn<string>(
            name: "Username",
            table: "Users",
            type: "TEXT",
            maxLength: 60,
            nullable: false,
            collation: "NOCASE",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 60);

        migrationBuilder.CreateIndex(
            name: "IX_ActiveRefreshTokens_TokenHash",
            table: "ActiveRefreshTokens",
            column: "TokenHash",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ActiveRefreshTokens_TokenHash",
            table: "ActiveRefreshTokens");

        migrationBuilder.AlterColumn<string>(
            name: "Username",
            table: "Users",
            type: "TEXT",
            maxLength: 60,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 60,
            oldCollation: "NOCASE");

        migrationBuilder.AddColumn<byte[]>(
            name: "TokenSalt",
            table: "ActiveRefreshTokens",
            type: "BLOB",
            nullable: false,
            defaultValue: new byte[0]);
    }
}
