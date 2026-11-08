using System.Globalization;
using ClosedXML.Excel;
using KimiaFaravar.Application.Inventory;
using KimiaFaravar.Web.Formatting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KimiaFaravar.Web.Reporting;

public interface IInventoryReportService
{
    byte[] CreateInventoryExcel(InventorySnapshot data, string preparedFor);
    byte[] CreateInventoryPdf(InventorySnapshot data, string preparedFor);
    byte[] CreateMovementsExcel(IReadOnlyCollection<StockMovementDto> movements, string preparedFor, string dateRange);
    byte[] CreateMovementsPdf(IReadOnlyCollection<StockMovementDto> movements, string preparedFor, string dateRange);
}

public sealed class InventoryReportService : IInventoryReportService
{
    private const string FontName = "Tahoma";
    private static readonly string[] InventoryHeaders = ["ردیف", "کد کالا", "نام کالا", "موجودی", "واحد"];
    private static readonly string[] MovementHeaders = ["ردیف", "تاریخ و ساعت", "نوع گردش", "نام کالا", "مقدار", "واحد", "شرح"];

    public byte[] CreateInventoryExcel(InventorySnapshot data, string preparedFor)
    {
        using var workbook = new XLWorkbook();
        CreateInventorySheet(workbook, data, preparedFor);
        return SaveWorkbook(workbook);
    }

    public byte[] CreateMovementsExcel(IReadOnlyCollection<StockMovementDto> movements, string preparedFor, string dateRange)
    {
        using var workbook = new XLWorkbook();
        CreateMovementsSheet(workbook, movements, preparedFor, dateRange);
        return SaveWorkbook(workbook);
    }

    public byte[] CreateInventoryPdf(InventorySnapshot data, string preparedFor) =>
        Document.Create(document => document.Page(page =>
        {
            ConfigurePage(page, "گزارش موجودی قابل مشاهده", preparedFor);
            page.Content().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(35);
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(2.5f);
                    columns.RelativeColumn(1.1f);
                    columns.RelativeColumn(0.8f);
                });
                table.Header(header => AddPdfHeader(header, InventoryHeaders));
                var row = 1;
                foreach (var product in data.Products)
                {
                    AddPdfCell(table, ToPersianDigits(row++.ToString(CultureInfo.InvariantCulture)), true);
                    AddPdfCell(table, product.Sku, true);
                    AddPdfCell(table, product.Name);
                    AddPdfCell(table, ToPersianDigits(FormatQuantity(product.Quantity)), true);
                    AddPdfCell(table, product.BaseUnit, true);
                }
                if (data.Products.Count == 0)
                    table.Cell().ColumnSpan(5).Element(BodyCell).AlignCenter().Text("کالایی برای این سطح دسترسی قابل مشاهده نیست.");
            });
        })).GeneratePdf();

    public byte[] CreateMovementsPdf(IReadOnlyCollection<StockMovementDto> movements, string preparedFor, string dateRange) =>
        Document.Create(document => document.Page(page =>
        {
            ConfigurePage(page, $"گزارش گردش‌های کالا | {dateRange}", preparedFor);
            page.Content().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(32);
                    columns.RelativeColumn(1.35f);
                    columns.RelativeColumn(0.75f);
                    columns.RelativeColumn(1.7f);
                    columns.RelativeColumn(0.8f);
                    columns.RelativeColumn(0.7f);
                    columns.RelativeColumn(2.1f);
                });
                table.Header(header => AddPdfHeader(header, MovementHeaders));
                var row = 1;
                foreach (var movement in movements)
                {
                    AddPdfCell(table, ToPersianDigits(row++.ToString(CultureInfo.InvariantCulture)), true);
                    AddPdfCell(table, PersianDateTime.Format(movement.CreatedAt, true), true);
                    AddPdfCell(table, movement.Type == MovementType.Receipt ? "ورود" : "خروج", true);
                    AddPdfCell(table, movement.ProductName);
                    AddPdfCell(table, ToPersianDigits(FormatQuantity(movement.Quantity)), true);
                    AddPdfCell(table, movement.Unit, true);
                    AddPdfCell(table, movement.Reason ?? "-");
                }
                if (movements.Count == 0)
                    table.Cell().ColumnSpan(7).Element(BodyCell).AlignCenter().Text("در این بازه گردشی برای سطح دسترسی شما وجود ندارد.");
            });
        })).GeneratePdf();

    private static byte[] SaveWorkbook(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void CreateInventorySheet(XLWorkbook workbook, InventorySnapshot data, string preparedFor)
    {
        var sheet = workbook.Worksheets.Add("موجودی قابل مشاهده");
        PrepareSheet(sheet, "گزارش موجودی قابل مشاهده", preparedFor, 5);
        WriteHeaders(sheet, 5, InventoryHeaders);
        var row = 6;
        var index = 1;
        foreach (var product in data.Products)
        {
            sheet.Cell(row, 1).Value = index++;
            sheet.Cell(row, 2).Value = product.Sku;
            sheet.Cell(row, 3).Value = product.Name;
            sheet.Cell(row, 4).Value = product.Quantity;
            sheet.Cell(row, 5).Value = product.BaseUnit;
            row++;
        }
        FinishSheet(sheet, row, 5, 4);
    }

    private static void CreateMovementsSheet(XLWorkbook workbook, IReadOnlyCollection<StockMovementDto> movements, string preparedFor, string dateRange)
    {
        var sheet = workbook.Worksheets.Add("آخرین گردش‌ها");
        PrepareSheet(sheet, $"گزارش گردش‌های کالا | {dateRange}", preparedFor, 7);
        WriteHeaders(sheet, 5, MovementHeaders);
        var row = 6;
        var index = 1;
        foreach (var movement in movements)
        {
            sheet.Cell(row, 1).Value = index++;
            sheet.Cell(row, 2).Value = PersianDateTime.Format(movement.CreatedAt, true);
            sheet.Cell(row, 3).Value = movement.Type == MovementType.Receipt ? "ورود" : "خروج";
            sheet.Cell(row, 4).Value = movement.ProductName;
            sheet.Cell(row, 5).Value = movement.Quantity;
            sheet.Cell(row, 6).Value = movement.Unit;
            sheet.Cell(row, 7).Value = movement.Reason ?? "-";
            row++;
        }
        FinishSheet(sheet, row, 7, 5);
    }

    private static void PrepareSheet(IXLWorksheet sheet, string title, string preparedFor, int columns)
    {
        sheet.RightToLeft = true;
        sheet.ShowGridLines = false;
        sheet.Style.Font.FontName = FontName;
        sheet.Style.Font.FontSize = 10;
        sheet.Range(1, 1, 1, columns).Merge().Value = "کیمیا فرآور صنعت";
        sheet.Range(1, 1, 1, columns).Style.Font.SetBold().Font.SetFontSize(15).Font.SetFontColor(XLColor.FromHtml("#17324D"));
        sheet.Range(2, 1, 2, columns).Merge().Value = title;
        sheet.Range(2, 1, 2, columns).Style.Font.SetBold().Font.SetFontSize(13);
        sheet.Range(3, 1, 3, columns).Merge().Value = $"تهیه‌شده برای: {preparedFor} | تاریخ گزارش: {PersianDateTime.ServerToday()}";
        sheet.Range(3, 1, 3, columns).Style.Font.SetFontColor(XLColor.Gray);
        sheet.Range(1, 1, 3, columns).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
        sheet.Row(4).Height = 8;
        sheet.SheetView.FreezeRows(5);
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.Margins.SetTop(0.5).SetBottom(0.5).SetLeft(0.4).SetRight(0.4);
    }

    private static void WriteHeaders(IXLWorksheet sheet, int row, string[] headers)
    {
        for (var column = 1; column <= headers.Length; column++) sheet.Cell(row, column).Value = headers[column - 1];
        var range = sheet.Range(row, 1, row, headers.Length);
        range.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#17324D"));
        range.Style.Font.SetBold().Font.SetFontColor(XLColor.White);
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Row(row).Height = 24;
    }

    private static void FinishSheet(IXLWorksheet sheet, int nextRow, int columns, int numericColumn)
    {
        var lastRow = Math.Max(6, nextRow - 1);
        var body = sheet.Range(6, 1, lastRow, columns);
        body.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        body.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        body.Style.Border.BottomBorder = XLBorderStyleValues.Hair;
        body.Style.Border.BottomBorderColor = XLColor.FromHtml("#D9E2EC");
        sheet.Column(numericColumn).Style.NumberFormat.Format = "#,##0.###";
        sheet.Column(1).Width = 8;
        sheet.Columns(2, columns).AdjustToContents(5, lastRow);
        foreach (var column in sheet.Columns(2, columns))
            if (column.Width > 35) column.Width = 35;
        sheet.Rows(6, lastRow).Height = 21;
        sheet.RangeUsed()?.SetAutoFilter(false);
        sheet.PageSetup.PrintAreas.Clear();
        sheet.PageSetup.PrintAreas.Add(1, 1, lastRow, columns);
        sheet.PageSetup.CenterHorizontally = true;
    }

    private static void ConfigurePage(PageDescriptor page, string title, string preparedFor)
    {
        page.Size(PageSizes.A4.Landscape());
        page.Margin(24);
        page.PageColor(Colors.White);
        page.ContentFromRightToLeft();
        page.DefaultTextStyle(style => style.FontFamily(FontName).FontSize(9).FontColor("#243B53"));
        page.Header().Column(column =>
        {
            column.Item().Text("کیمیا فرآور صنعت").FontSize(15).SemiBold().FontColor("#17324D").AlignRight();
            column.Item().Text(title).FontSize(12).SemiBold().AlignRight();
            column.Item().PaddingBottom(8).Text($"تهیه‌شده برای: {preparedFor} | تاریخ گزارش: {PersianDateTime.ServerToday()}").FontSize(8).FontColor("#627D98").AlignRight();
        });
        page.Footer().AlignCenter().Text(text =>
        {
            text.Span("صفحه ");
            text.CurrentPageNumber();
            text.Span(" از ");
            text.TotalPages();
        });
    }

    private static void AddPdfHeader(TableCellDescriptor header, IEnumerable<string> values)
    {
        foreach (var value in values)
            header.Cell().Element(HeaderCell).AlignCenter().Text(value).SemiBold().FontColor(Colors.White);
    }

    private static void AddPdfCell(TableDescriptor table, string value, bool center = false)
    {
        var cell = table.Cell().Element(BodyCell);
        if (center) cell = cell.AlignCenter();
        cell.Text(value);
    }

    private static IContainer HeaderCell(IContainer container) =>
        container.Background("#17324D").BorderColor("#FFFFFF").BorderRight(0.5f).PaddingVertical(7).PaddingHorizontal(5).AlignMiddle();

    private static IContainer BodyCell(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor("#D9E2EC").PaddingVertical(6).PaddingHorizontal(5).AlignMiddle();

    private static string ToPersianDigits(string value) => value
        .Replace('0', '۰').Replace('1', '۱').Replace('2', '۲').Replace('3', '۳').Replace('4', '۴')
        .Replace('5', '۵').Replace('6', '۶').Replace('7', '۷').Replace('8', '۸').Replace('9', '۹');
    private static string FormatQuantity(decimal value) => value.ToString("N3", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
}
