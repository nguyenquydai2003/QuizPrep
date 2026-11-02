using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace QuizPrep.Api.Features.Import;

/// <summary>
/// Đọc câu hỏi từ sheet đầu tiên. Dòng tiêu đề (trong 10 dòng đầu) cần có cột "Câu hỏi"
/// và ít nhất 2 cột đáp án "A", "B"... Các cột khác (Môn, Chủ đề, Độ khó, Đáp án, Giải thích, Tags) tùy chọn.
/// </summary>
public static partial class ExcelQuestionParser
{
    private enum Field { Subject, Topic, Difficulty, Type, Content, Answer, Explanation, Tags }

    private static readonly Dictionary<string, Field> Headers = new()
    {
        ["mon"] = Field.Subject, ["mon hoc"] = Field.Subject, ["subject"] = Field.Subject,
        ["chu de"] = Field.Topic, ["topic"] = Field.Topic,
        ["do kho"] = Field.Difficulty, ["muc do"] = Field.Difficulty, ["difficulty"] = Field.Difficulty,
        ["loai"] = Field.Type, ["loai cau hoi"] = Field.Type, ["type"] = Field.Type,
        ["cau hoi"] = Field.Content, ["noi dung"] = Field.Content, ["noi dung cau hoi"] = Field.Content,
        ["de bai"] = Field.Content, ["question"] = Field.Content, ["content"] = Field.Content,
        ["dap an"] = Field.Answer, ["dap an dung"] = Field.Answer, ["answer"] = Field.Answer, ["correct"] = Field.Answer,
        ["giai thich"] = Field.Explanation, ["loi giai"] = Field.Explanation, ["explanation"] = Field.Explanation,
        ["tags"] = Field.Tags, ["tag"] = Field.Tags, ["nhan"] = Field.Tags,
    };

    private const int HeaderSearchRows = 10;

    public static List<ParsedQuestion> Parse(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        var (headerRow, fields, options) = FindHeader(sheet, lastRow, lastColumn);
        if (headerRow == 0)
            throw new ImportFormatException("Không tìm thấy dòng tiêu đề. Sheet đầu tiên cần có cột \"Câu hỏi\" và các cột đáp án \"A\", \"B\", \"C\", \"D\".");

        var result = new List<ParsedQuestion>();
        for (var r = headerRow + 1; r <= lastRow; r++)
        {
            var row = sheet.Row(r);
            string Cell(int column) => ImportText.Clean(row.Cell(column).GetFormattedString());

            if (fields.Keys.Concat(options.Keys).All(c => Cell(c).Length == 0)) continue;

            var q = new ParsedQuestion { Source = $"Dòng {r}" };
            foreach (var (column, field) in fields)
            {
                var value = Cell(column);
                switch (field)
                {
                    case Field.Subject: q.SubjectName = value; break;
                    case Field.Topic: q.TopicName = value; break;
                    case Field.Difficulty: q.DifficultyText = value; break;
                    case Field.Type: q.TypeText = value; break;
                    case Field.Content: q.Content = value; break;
                    case Field.Answer: q.AnswerText = value; break;
                    case Field.Explanation: q.Explanation = value; break;
                    case Field.Tags: q.Tags = ImportText.SplitTags(value); break;
                }
            }

            foreach (var (column, letter) in options.OrderBy(o => o.Value))
            {
                var content = Cell(column);
                if (content.Length > 0) q.Options.Add(new ParsedOption { Letter = letter, Content = content });
            }
            result.Add(q);
        }
        return result;
    }

    private static (int Row, Dictionary<int, Field> Fields, Dictionary<int, char> Options) FindHeader(IXLWorksheet sheet, int lastRow, int lastColumn)
    {
        for (var r = 1; r <= Math.Min(HeaderSearchRows, lastRow); r++)
        {
            var fields = new Dictionary<int, Field>();
            var options = new Dictionary<int, char>();
            for (var c = 1; c <= lastColumn; c++)
            {
                var key = ImportText.Key(sheet.Cell(r, c).GetFormattedString());
                var optionMatch = OptionHeader().Match(key);
                if (optionMatch.Success) options[c] = char.ToUpperInvariant(optionMatch.Groups[1].Value[0]);
                else if (Headers.TryGetValue(key, out var field) && !fields.ContainsValue(field)) fields[c] = field;
            }
            if (fields.ContainsValue(Field.Content) && options.Count >= 2) return (r, fields, options);
        }
        return (0, [], []);
    }

    // "a", "đáp án a", "phương án b", "option c"
    [GeneratedRegex(@"^(?:(?:dap an|phuong an|lua chon|option)\s*)?([a-j])$")]
    private static partial Regex OptionHeader();
}
