using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace QuizPrep.Api.Features.Import;

/// <summary>Sinh file mẫu Excel/Word để người dùng tải về và điền câu hỏi.</summary>
public static class ImportTemplates
{
    public const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string WordContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public static byte[] Excel()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Câu hỏi");
        string[] headers = ["Môn", "Chủ đề", "Độ khó", "Câu hỏi", "A", "B", "C", "D", "Đáp án", "Giải thích", "Tags"];
        string[][] rows =
        [
            ["Lập trình C#", "Cơ bản", "Dễ", "Từ khóa nào dùng để khai báo hằng số lúc biên dịch trong C#?",
                "static", "const", "readonly", "volatile", "B", "`const` là hằng được thay giá trị lúc biên dịch.", "từ-khóa"],
            ["Lập trình C#", "Cơ bản", "Trung bình", "Đoạn code sau in ra gì?\n```csharp\nint a = 10, b = 3;\nConsole.WriteLine(a / b);\n```",
                "3.33", "3", "4", "Lỗi biên dịch", "B", "Chia hai số nguyên cho kết quả nguyên: 10 / 3 = **3**.", "toán-tử"],
            ["SQL", "Truy vấn", "Khó", "Những lệnh nào thuộc nhóm **DML**?",
                "INSERT", "CREATE", "UPDATE", "DROP", "A, C", "`CREATE`, `DROP` thuộc DDL.", "dml, sql-cơ-bản"],
        ];

        for (var c = 0; c < headers.Length; c++) sheet.Cell(1, c + 1).Value = headers[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                sheet.Cell(r + 2, c + 1).Value = rows[r][c];

        var header = sheet.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#DDEBF7");
        sheet.Range(2, 1, rows.Length + 1, headers.Length).Style.Alignment.WrapText = true;
        sheet.Range(2, 1, rows.Length + 1, headers.Length).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        sheet.SheetView.FreezeRows(1);
        double[] widths = [16, 14, 11, 50, 16, 16, 16, 16, 9, 40, 16];
        for (var c = 0; c < widths.Length; c++) sheet.Column(c + 1).Width = widths[c];

        var guide = workbook.AddWorksheet("Hướng dẫn");
        string[] notes =
        [
            "HƯỚNG DẪN NHẬP CÂU HỎI",
            "• Chỉ sheet đầu tiên được đọc. Dòng tiêu đề cần có cột \"Câu hỏi\" và các cột đáp án A, B, C, D (tối đa đến J).",
            "• Môn / Chủ đề: chưa có sẽ được tạo mới. Để trống thì dùng chủ đề mặc định chọn lúc import.",
            "• Độ khó: Dễ / Trung bình / Khó (để trống = Trung bình).",
            "• Đáp án: một chữ cái (B) hoặc nhiều chữ cái (A, C) cho câu nhiều đáp án đúng.",
            "• Code: xuống dòng trong ô bằng Alt+Enter, bọc code trong ```csharp ... ``` (đổi csharp thành ngôn ngữ tương ứng).",
            "• Nội dung hỗ trợ Markdown: **in đậm**, `code trong dòng`.",
            "• Tags: phân tách bằng dấu phẩy.",
        ];
        for (var i = 0; i < notes.Length; i++) guide.Cell(i + 1, 1).Value = notes[i];
        guide.Cell(1, 1).Style.Font.Bold = true;
        guide.Column(1).Width = 110;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static byte[] Word()
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body());
            var body = main.Document.Body!;

            void Line(string text, bool bold = false, bool code = false) => body.Append(Paragraph(Run(text, bold, code)));

            Line("MẪU NHẬP CÂU HỎI TRẮC NGHIỆM", bold: true);
            Line("Quy ước: mỗi câu bắt đầu bằng \"Câu <số>:\", đáp án bắt đầu bằng \"A.\", \"B.\"... Đánh dấu đáp án đúng bằng dấu * trước chữ cái, "
                + "hoặc thêm dòng \"Đáp án: B\", hoặc in đậm cả đáp án đúng. Đoạn code viết bằng font Consolas hoặc Courier New. "
                + "Các dòng Môn, Chủ đề áp dụng cho các câu phía sau. Tắt đánh số tự động (Numbering) để chữ \"Câu 1\", \"A.\" là chữ thật.");
            Line("");

            Line("Môn: Lập trình C#");
            Line("Chủ đề: Cơ bản");
            Line("Câu 1: Đoạn code sau in ra gì?");
            Line("int x = 5;", code: true);
            Line("Console.WriteLine(x++ + ++x);", code: true);
            Line("A. 10");
            Line("B. 11");
            Line("*C. 12");
            Line("D. 13");
            Line("Độ khó: Trung bình");
            Line("Giải thích: x++ trả về 5 rồi x = 6; ++x tăng x lên 7 rồi trả về 7. Kết quả 5 + 7 = 12.");
            Line("");

            Line("Câu 2: Kiểu nào sau đây là value type?");
            body.Append(Paragraph(Run("A. string"), Tab(), Run("B. object"), Tab(), Run("C. struct"), Tab(), Run("D. class")));
            Line("Đáp án: C");
            Line("Độ khó: Dễ");
            Line("");

            Line("Câu 3: Phương thức nào của LINQ thực thi truy vấn ngay lập tức?");
            Line("A. Where()");
            Line("B. ToList()", bold: true);
            Line("C. Select()");
            Line("D. OrderBy()");
            Line("Tags: linq, deferred-execution");
            Line("");

            Line("Môn: SQL");
            Line("Chủ đề: Truy vấn");
            Line("Câu 4: Những loại JOIN nào giữ lại toàn bộ dòng của bảng bên trái?");
            Line("A. INNER JOIN");
            Line("B. LEFT JOIN");
            Line("C. FULL OUTER JOIN");
            Line("D. CROSS JOIN");
            Line("Đáp án: B, C");
            Line("Giải thích: LEFT JOIN và FULL OUTER JOIN đều giữ toàn bộ dòng bảng trái.");
        }
        return stream.ToArray();
    }

    private static Paragraph Paragraph(params OpenXmlElement[] children) => new(children);

    private static Run Tab() => new(new TabChar());

    private static Run Run(string text, bool bold = false, bool code = false)
    {
        var props = new RunProperties();
        // Thứ tự phần tử theo schema: rFonts trước b
        if (code) props.Append(new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas", ComplexScript = "Consolas" });
        if (bold) props.Append(new Bold());
        return new Run(props, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }
}
