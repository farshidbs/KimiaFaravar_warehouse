using ClosedXML.Excel;
using KimiaFaravar.Application.Inventory;
using KimiaFaravar.Web.Reporting;
using KimiaFaravar.Application.Security;
using QuestPDF.Infrastructure;

namespace KimiaFaravar.Tests;

public sealed class InventoryReportTests
{
    [Fact]
    public void ExcelAndPdfReportsAreValidAndContainOnlySuppliedVisibleData()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;
        var source = new InventorySnapshot(
            [
                new ProductGroupDto(1, "فروش داخلی", "INTERNAL", null, true, false),
                new ProductGroupDto(2, "محرمانه", "PRIVATE", null, false, false)
            ],
            [],
            [
                new ProductDto(1, 1, "VISIBLE-01", "کالای مجاز", "عدد", 12.5m),
                new ProductDto(2, 2, "HIDDEN-01", "کالای غیرمجاز", "عدد", 99m)
            ],
            [
                new StockMovementDto(1, 1, "کالای مجاز", MovementType.Receipt, 2m, "عدد", "ورود آزمایشی", DateTimeOffset.UtcNow),
                new StockMovementDto(2, 2, "کالای غیرمجاز", MovementType.Dispatch, 1m, "عدد", "نباید صادر شود", DateTimeOffset.UtcNow)
            ],
            [], []);
        var visible = InventoryVisibility.FilterForRole(source, RoleCodes.InternalSales);

        var service = new InventoryReportService();
        var excel = service.CreateInventoryExcel(visible, "کاربر آزمایشی");
        var pdf = service.CreateInventoryPdf(visible, "کاربر آزمایشی");
        var movementsExcel = service.CreateMovementsExcel(visible.Movements, "کاربر آزمایشی", "از ۱۴۰۵/۰۸/۰۱ تا ۱۴۰۵/۰۸/۰۷");
        var movementsPdf = service.CreateMovementsPdf(visible.Movements, "کاربر آزمایشی", "از ۱۴۰۵/۰۸/۰۱ تا ۱۴۰۵/۰۸/۰۷");


        Assert.True(excel.Length > 1_000);
        Assert.True(pdf.Length > 1_000);
        Assert.True(movementsExcel.Length > 1_000);
        Assert.True(movementsPdf.Length > 1_000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(movementsPdf, 0, 4));

        using var stream = new MemoryStream(excel);
        using var workbook = new XLWorkbook(stream);
        Assert.Single(workbook.Worksheets);
        var inventory = workbook.Worksheet("موجودی قابل مشاهده");
        Assert.True(inventory.RightToLeft);
        Assert.Equal("VISIBLE-01", inventory.Cell(6, 2).GetString());
        Assert.Equal("کالای مجاز", inventory.Cell(6, 3).GetString());
        Assert.DoesNotContain("HIDDEN", string.Join("|", inventory.CellsUsed().Select(cell => cell.GetString())));

        using var movementsStream = new MemoryStream(movementsExcel);
        using var movementsWorkbook = new XLWorkbook(movementsStream);
        Assert.Single(movementsWorkbook.Worksheets);
        var movements = movementsWorkbook.Worksheet("آخرین گردش‌ها");
        Assert.True(movements.RightToLeft);
        Assert.Equal("کالای مجاز", movements.Cell(6, 4).GetString());
        Assert.DoesNotContain("غیرمجاز", string.Join("|", movements.CellsUsed().Select(cell => cell.GetString())));
    }
}
