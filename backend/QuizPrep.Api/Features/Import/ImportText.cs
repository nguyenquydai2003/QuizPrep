using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using QuizPrep.Api.Domain;

namespace QuizPrep.Api.Features.Import;

/// <summary>Các hàm xử lý chữ dùng chung cho parser Excel và Word.</summary>
public static partial class ImportText
{
    /// <summary>Bỏ dấu, chữ thường, gộp khoảng trắng: "Độ  Khó" => "do kho".</summary>
    public static string Key(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var decomposed = value.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return Whitespace().Replace(sb.ToString().Normalize(NormalizationForm.FormC), " ");
    }

    /// <summary>Chuẩn hóa xuống dòng và bỏ khoảng trắng đầu/cuối.</summary>
    public static string Clean(string? value) =>
        (value ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Trim();

    public static Difficulty? ParseDifficulty(string? text) => Key(text) switch
    {
        "" => null,
        "de" or "easy" or "1" => Difficulty.Easy,
        "tb" or "trung binh" or "vua" or "medium" or "2" => Difficulty.Medium,
        "kho" or "hard" or "3" => Difficulty.Hard,
        _ => null,
    };

    public static QuestionType? ParseType(string? text)
    {
        var key = Key(text);
        if (key == "") return null;
        if (key.Contains("nhieu") || key.Contains("multi")) return QuestionType.MultipleChoice;
        if (key.Contains("mot") || key.Contains("single")) return QuestionType.SingleChoice;
        return null;
    }

    /// <summary>"A, C" / "A;C" / "AC" / "a" => ['A','C'].</summary>
    public static List<char> ParseAnswerLetters(string? text)
    {
        var letters = new List<char>();
        foreach (var c in (text ?? "").ToUpperInvariant())
            if (c is >= 'A' and <= 'J' && !letters.Contains(c)) letters.Add(c);
            else if (char.IsLetterOrDigit(c)) return []; // có chữ lạ => không phải danh sách chữ cái
        return letters;
    }

    public static List<string> SplitTags(string? text) =>
        (text ?? "").Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    /// <summary>Tên ngôn ngữ cho khối code Markdown, ví dụ "C#" => "csharp".</summary>
    public static string NormalizeLanguage(string? value) => Key(value) switch
    {
        "c#" or "csharp" or "cs" => "csharp",
        "js" or "javascript" => "javascript",
        "ts" or "typescript" => "typescript",
        "py" or "python" => "python",
        "sql" or "t-sql" or "tsql" or "mysql" or "postgresql" or "sql server" => "sql",
        "c" or "c++" or "cpp" => "cpp",
        "go" or "golang" => "go",
        "html" or "xml" => "xml",
        "sh" or "shell" or "bash" => "bash",
        var other => other.Replace(" ", ""),
    };

    /// <summary>Đoán ngôn ngữ code từ tên môn/chủ đề, ví dụ "Lập trình C#" => "csharp".</summary>
    public static string GuessLanguage(params string?[] names)
    {
        var text = $" {Key(string.Join(' ', names))} ";
        if (text.Contains("c#")) return "csharp";
        if (text.Contains("typescript")) return "typescript";
        if (text.Contains("javascript") || text.Contains(" js ")) return "javascript";
        if (text.Contains("java")) return "java";
        if (text.Contains("python")) return "python";
        if (text.Contains("sql")) return "sql";
        if (text.Contains("c++")) return "cpp";
        if (text.Contains("golang")) return "go";
        if (text.Contains("php")) return "php";
        return "";
    }

    /// <summary>Đoán ngôn ngữ từ nội dung code khi tên môn/chủ đề không gợi ý được. Trả về "" nếu không chắc.</summary>
    public static string GuessLanguageFromCode(string code)
    {
        if (CSharpSpecific().IsMatch(code)) return "csharp";
        if (code.Contains("System.out.print") || code.Contains("import java.")) return "java";
        if (code.Contains("#include") || code.Contains("std::")) return "cpp";
        if (PythonHints().IsMatch(code)) return "python";
        if (SqlHints().IsMatch(code)) return "sql";
        if (code.Contains("console.log") || JsHints().IsMatch(code)) return "javascript";
        if (CSharpGeneric().IsMatch(code)) return "csharp";
        return "";
    }

    [GeneratedRegex(@"Console\.Write|using System|\bnamespace\s+\w|\bstring\[\]|\bList<|\bIEnumerable<|\bvar\s+\w+\s*=")]
    private static partial Regex CSharpSpecific();

    [GeneratedRegex(@"^\s*def\s+\w+\(.*\)\s*:|^\s*print\(|^\s*elif\b", RegexOptions.Multiline)]
    private static partial Regex PythonHints();

    [GeneratedRegex(@"^\s*(SELECT|INSERT\s+INTO|UPDATE|DELETE\s+FROM|CREATE\s+TABLE|ALTER\s+TABLE)\b", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex SqlHints();

    [GeneratedRegex(@"\bfunction\s+\w*\s*\(|\blet\s+\w+\s*=|===")]
    private static partial Regex JsHints();

    [GeneratedRegex(@"\b(public|private|protected|internal)\s+(static\s+)?(class|interface|void|int|string)\b|\bclass\s+\w+\s*(:|\{)|=>")]
    private static partial Regex CSharpGeneric();

    /// <summary>Gộp các dòng thành Markdown, bỏ dòng trống thừa ở đầu/cuối.</summary>
    public static string JoinLines(IEnumerable<string> lines)
    {
        var text = string.Join('\n', lines).Trim('\n', ' ');
        return BlankLines().Replace(text, "\n\n");
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLines();
}
