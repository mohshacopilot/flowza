using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flowza.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrgHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "UnitNumbers",
                startValue: 2L);

            migrationBuilder.AddColumn<Guid>(
                name: "ManagerId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            // Nullable at first: users made with feature 001 get the root unit below, once it exists (plan 002 §5.2).
            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryUnitId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BusinessRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrgStructure",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangeCount = table.Column<long>(type: "bigint", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgStructure", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnitTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserBusinessRoles",
                columns: table => new
                {
                    BusinessRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBusinessRoles", x => new { x.UserId, x.BusinessRoleId });
                    table.ForeignKey(
                        name: "FK_UserBusinessRoles_BusinessRoles_BusinessRoleId",
                        column: x => x.BusinessRoleId,
                        principalTable: "BusinessRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserBusinessRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrgUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UnitTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Path = table.Column<string>(type: "varchar(900)", unicode: false, maxLength: 900, nullable: false),
                    HeadUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrgUnits_OrgUnits_ParentId",
                        column: x => x.ParentId,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrgUnits_UnitTypes_UnitTypeId",
                        column: x => x.UnitTypeId,
                        principalTable: "UnitTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrgUnits_Users_HeadUserId",
                        column: x => x.HeadUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "OrgStructure",
                columns: new[] { "Id", "ChangeCount", "ChangedAt" },
                values: new object[] { new Guid("0f6e5600-0000-4000-8000-000000000001"), 0L, null });

            migrationBuilder.InsertData(
                table: "UnitTypes",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "IsActive", "Name", "UpdatedAt", "UpdatedBy" },
                values: new object[] { new Guid("0f6e5600-0000-4000-8000-000000000002"), "ROOT", new DateTime(2026, 10, 24, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Organization", null, null });

            migrationBuilder.InsertData(
                table: "OrgUnits",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "HeadUserId", "IsActive", "Name", "Number", "ParentId", "Path", "UnitTypeId", "UpdatedAt", "UpdatedBy" },
                values: new object[] { new Guid("0f6e5600-0000-4000-8000-000000000003"), "ROOT", new DateTime(2026, 10, 24, 0, 0, 0, 0, DateTimeKind.Utc), null, null, true, "Organization", 1, null, "/1/", new Guid("0f6e5600-0000-4000-8000-000000000002"), null, null });

            // Back-fill (C2, analysis A7): every existing user is placed in the root unit, so "a user has a primary unit"
            // holds from the first start. EXEC keeps the production script valid when it is run again later (PLT-03).
            migrationBuilder.Sql(
                "EXEC(N'UPDATE [Users] SET [PrimaryUnitId] = ''0f6e5600-0000-4000-8000-000000000003'' WHERE [PrimaryUnitId] IS NULL');");

            migrationBuilder.AlterColumn<Guid>(
                name: "PrimaryUnitId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ManagerId",
                table: "Users",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PrimaryUnitId",
                table: "Users",
                column: "PrimaryUnitId")
                .Annotation("SqlServer:Include", new[] { "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessRoles_Code",
                table: "BusinessRoles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnits_Code",
                table: "OrgUnits",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnits_HeadUserId",
                table: "OrgUnits",
                column: "HeadUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnits_Number",
                table: "OrgUnits",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnits_OneRoot",
                table: "OrgUnits",
                column: "ParentId",
                unique: true,
                filter: "[ParentId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnits_ParentId",
                table: "OrgUnits",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnits_Path",
                table: "OrgUnits",
                column: "Path")
                .Annotation("SqlServer:Include", new[] { "UnitTypeId", "HeadUserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnits_UnitTypeId",
                table: "OrgUnits",
                column: "UnitTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitTypes_Code",
                table: "UnitTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserBusinessRoles_BusinessRoleId_UserId",
                table: "UserBusinessRoles",
                columns: new[] { "BusinessRoleId", "UserId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Users_OrgUnits_PrimaryUnitId",
                table: "Users",
                column: "PrimaryUnitId",
                principalTable: "OrgUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_ManagerId",
                table: "Users",
                column: "ManagerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_OrgUnits_PrimaryUnitId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_ManagerId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "OrgStructure");

            migrationBuilder.DropTable(
                name: "OrgUnits");

            migrationBuilder.DropTable(
                name: "UserBusinessRoles");

            migrationBuilder.DropTable(
                name: "UnitTypes");

            migrationBuilder.DropTable(
                name: "BusinessRoles");

            migrationBuilder.DropIndex(
                name: "IX_Users_ManagerId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_PrimaryUnitId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ManagerId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PrimaryUnitId",
                table: "Users");

            migrationBuilder.DropSequence(
                name: "UnitNumbers");
        }
    }
}
