using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace QuizPrep.Api.Features.Questions;

public static partial class QuestionHasher
{
    /// <summary>
    /// Hash nội dung câu hỏi + các đáp án (bỏ qua thứ tự đáp án và khoảng trắng thừa),
    /// để câu bị xáo đáp án vẫn được nhận là trùng. Không lowercase vì code phân biệt hoa thường.
    /// </summary>
    public static string Compute(string content, IEnumerable<string> options)
    {
        var parts = options.Select(Normalize).OrderBy(x => x, StringComparer.Ordinal).Prepend(Normalize(content));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001F', parts)));
        return Convert.ToHexString(bytes);
    }

    private static string Normalize(string s) => Whitespace().Replace(s.Trim(), " ");

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
