using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acme.Shop.Data.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Customers",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Customers", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Products",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                Sku = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Price = table.Column<decimal>(type: "decimal(10,2)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Products", x => x.Id));

        migrationBuilder.CreateTable(
            name: "OrderAudit",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                OrderId = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                ChangedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_OrderAudit", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Orders",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                CustomerId = table.Column<int>(type: "int", nullable: false),
                PlacedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "new")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Orders", x => x.Id);
                table.ForeignKey("FK_Orders_Customers", x => x.CustomerId, "Customers", "Id");
            });

        migrationBuilder.CreateTable(
            name: "OrderLines",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                OrderId = table.Column<int>(type: "int", nullable: false),
                ProductId = table.Column<int>(type: "int", nullable: false),
                Quantity = table.Column<int>(type: "int", nullable: false),
                UnitPrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrderLines", x => x.Id);
                table.ForeignKey("FK_OrderLines_Orders", x => x.OrderId, "Orders", "Id");
                table.ForeignKey("FK_OrderLines_Products", x => x.ProductId, "Products", "Id");
            });

        migrationBuilder.CreateIndex("IX_Orders_CustomerId", "Orders", "CustomerId");
        migrationBuilder.CreateIndex("IX_OrderLines_OrderId", "OrderLines", "OrderId");
        migrationBuilder.CreateIndex("IX_OrderLines_ProductId", "OrderLines", "ProductId");

        // Objects EF Core does not model are raw SQL.
        migrationBuilder.Sql("""
            CREATE TRIGGER dbo.trg_Orders_Audit ON dbo.Orders AFTER INSERT, UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                INSERT INTO dbo.OrderAudit (OrderId, Status, ChangedAt)
                SELECT i.Id, i.Status, SYSUTCDATETIME()
                FROM inserted AS i LEFT JOIN deleted AS d ON d.Id = i.Id
                WHERE d.Id IS NULL OR d.Status <> i.Status;
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "OrderLines");
        migrationBuilder.DropTable(name: "Orders");
        migrationBuilder.DropTable(name: "OrderAudit");
        migrationBuilder.DropTable(name: "Products");
        migrationBuilder.DropTable(name: "Customers");
    }
}
