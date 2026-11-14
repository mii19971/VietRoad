using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace VietRoad.Admin.Services;

/// <summary>
/// Doc file CSV / Excel (.xlsx) thanh danh sach dong: moi dong = { ten cot (dong dau tien): gia tri }.
/// Chi doc, KHONG hieu y nghia cot - viec do (cot nao la vi do, loai nao...) do VietRoad.Api lam.
/// Moi dong co them khoa "__row" = so dong trong file, de bao loi dung dong.
/// </summary>
public static class SpreadsheetReader
{
    public const string RowKey = "__row";

    public static List<Dictionary<string, string?>> Read(Stream stream, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".xlsx" => ReadExcel(stream),
            ".csv" or ".txt" => ReadCsv(stream),
            _ => throw new SpreadsheetException($"Chỉ nhận file .csv hoặc .xlsx (file của bạn: {ext})."),
        };
    }

    // ---------- Excel ----------

    private static List<Dictionary<string, string?>> ReadExcel(Stream stream)
    {
        XLWorkbook workbook;
        try { workbook = new XLWorkbook(stream); }
        catch (Exception ex) { throw new SpreadsheetException($"Không đọc được file Excel: {ex.Message}"); }

        using (workbook)
        {
            var sheet = workbook.Worksheets.First(); // chi doc sheet dau tien
            var used = sheet.RangeUsed();
            if (used is null) return [];

            var header = used.FirstRow().Cells().Select(c => c.GetString().Trim()).ToList();
            var rows = new List<Dictionary<string, string?>>();
            foreach (var row in used.RowsUsed().Skip(1))
            {
                var values = Enumerable.Range(1, header.Count).Select(i => CellText(row.Cell(i))).ToList();
                AddRow(rows, header, values, row.RowNumber());
            }
            return rows;
        }
    }

    // O so (vd vi do 21.0285) doc theo dinh dang chuan "." - khong phu thuoc may cai tieng Viet hay Anh.
    private static string? CellText(IXLCell cell) =>
        cell.Value.IsNumber ? cell.Value.GetNumber().ToString(CultureInfo.InvariantCulture)
        : cell.Value.IsBlank ? null
        : cell.GetFormattedString();

    // ---------- CSV ----------

    private static List<Dictionary<string, string?>> ReadCsv(Stream stream)
    {
        // UTF-8 (co / khong co BOM). Excel: "Lưu dưới dạng" -> "CSV UTF-8".
        using var reader = new StreamReader(stream, new UTF8Encoding(false, throwOnInvalidBytes: true), detectEncodingFromByteOrderMarks: true);
        string text;
        try { text = reader.ReadToEnd(); }
        catch (DecoderFallbackException)
        {
            throw new SpreadsheetException("File CSV không phải UTF-8 (chữ có dấu sẽ bị lỗi). Trong Excel chọn \"Lưu dưới dạng\" → \"CSV UTF-8\", hoặc tải lên file .xlsx.");
        }

        var lines = ParseCsv(text);
        if (lines.Count == 0) return [];

        var header = lines[0].Select(h => h.Trim()).ToList();
        var rows = new List<Dictionary<string, string?>>();
        for (var i = 1; i < lines.Count; i++)
            AddRow(rows, header, lines[i], i + 1);
        return rows;
    }

    /// <summary>
    /// Tach CSV: ho tro o co dau ngoac kep ("Ha Noi, Viet Nam"), "" trong ngoac, xuong dong trong o.
    /// Tu nhan dau phan cach: "," hoac ";" (Excel tieng Viet hay dung ";") hoac Tab.
    /// </summary>
    public static List<List<string>> ParseCsv(string text)
    {
        var firstLine = text.Split('\n')[0];
        var delimiter = new[] { ';', '\t', ',' }.OrderByDescending(d => firstLine.Count(c => c == d)).First();

        var result = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else cell.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == delimiter) { row.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\n')
            {
                row.Add(cell.ToString().TrimEnd('\r')); cell.Clear();
                result.Add(row); row = [];
            }
            else cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString().TrimEnd('\r')); result.Add(row); }
        return result;
    }

    // Bo dong trong hoan toan (hay gap o cuoi file Excel).
    private static void AddRow(List<Dictionary<string, string?>> rows, List<string> header, IList<string?> values, int rowNumber)
    {
        if (values.All(string.IsNullOrWhiteSpace)) return;

        var dict = new Dictionary<string, string?> { [RowKey] = rowNumber.ToString(CultureInfo.InvariantCulture) };
        for (var i = 0; i < header.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(header[i])) continue;
            dict.TryAdd(header[i], i < values.Count ? values[i] : null);
        }
        rows.Add(dict);
    }
}

public class SpreadsheetException(string message) : Exception(message);
