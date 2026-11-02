using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace QuizPrep.Api.Features.Import;

/// <summary>
/// Đọc câu hỏi từ file Word theo quy ước:
/// <code>
/// Môn: Lập trình C#          (áp dụng cho các câu phía sau)
/// Chủ đề: Cơ bản
/// Câu 1: Nội dung câu hỏi...
/// [các đoạn font Consolas/Courier được coi là code]
/// A. ...
/// *B. ...                    (dấu * = đáp án đúng; hoặc dòng "Đáp án: B"; hoặc in đậm/gạch chân/tô đỏ cả đáp án)
/// Độ khó: Dễ | Tags: a, b | Loại: Nhiều đáp án
/// Giải thích: ...
/// </code>
/// </summary>
public sealed partial class WordQuestionParser
{
    private static readonly string[] MonospaceFonts =
        ["consolas", "courier", "mono", "cascadia", "menlo", "monaco", "lucida console", "source code", "fira code", "jetbrains"];

    private static readonly string[] CodeStyleHints = ["code", "preformatted", "plaintext", "sourcecode", "macro"];

    private enum MetaKey { Subject, Topic, Language, Difficulty, Tags, Type, Answer, Explanation }

    private static readonly Dictionary<string, MetaKey> MetaKeys = new()
    {
        ["mon"] = MetaKey.Subject, ["mon hoc"] = MetaKey.Subject,
        ["chu de"] = MetaKey.Topic,
        ["ngon ngu"] = MetaKey.Language, ["language"] = MetaKey.Language,
        ["do kho"] = MetaKey.Difficulty, ["muc do"] = MetaKey.Difficulty,
        ["tags"] = MetaKey.Tags, ["tag"] = MetaKey.Tags,
        ["loai"] = MetaKey.Type, ["loai cau hoi"] = MetaKey.Type,
        ["dap an"] = MetaKey.Answer, ["dap an dung"] = MetaKey.Answer, ["answer"] = MetaKey.Answer,
        ["giai thich"] = MetaKey.Explanation, ["loi giai"] = MetaKey.Explanation, ["explanation"] = MetaKey.Explanation,
    };

    private enum Target { None, Content, Option, Explanation }

    /// <param name="HeadingLevel">0 = Title của tài liệu, 1..9 = Heading, null = đoạn thường.</param>
    private record ParagraphInfo(string Text, bool IsCode, bool IsEmphasized, bool HasImage, int? HeadingLevel);

    private readonly Dictionary<string, Style> _styles;
    private readonly List<ParsedQuestion> _result = [];

    // Trạng thái đọc
    private string? _subject, _topic, _language, _defaultDifficulty;
    private QuestionBuilder? _current;
    private readonly List<string> _code = [];
    private bool _inFence;

    // Khối "ĐÁP ÁN" ở cuối file: số câu => đáp án, ghép vào câu hỏi sau khi đọc xong
    private sealed record AnswerKeyEntry(string Letters, List<string> Explanation);
    private readonly Dictionary<int, AnswerKeyEntry> _answerKey = [];
    private readonly List<(int Number, ParsedQuestion Question)> _numbered = [];
    private AnswerKeyEntry? _lastKey;
    private bool _inAnswerKey;

    private WordQuestionParser(WordprocessingDocument doc)
    {
        _styles = doc.MainDocumentPart?.StyleDefinitionsPart?.Styles?.Elements<Style>()
            .Where(s => s.StyleId?.Value != null)
            .GroupBy(s => s.StyleId!.Value!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase) ?? [];
    }

    public static List<ParsedQuestion> Parse(Stream stream)
    {
        using var doc = WordprocessingDocument.Open(stream, false);
        var body = doc.MainDocumentPart?.Document?.Body ?? throw new ImportFormatException("File Word không có nội dung.");

        var parser = new WordQuestionParser(doc);
        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            var info = parser.Read(paragraph);
            if (info.HeadingLevel is { } level && parser.TryHandleHeading(info.Text.Trim(), level)) continue;
            if (info.HasImage) parser._current?.Warn("Câu có hình ảnh, hiện chưa hỗ trợ nên đã bỏ qua hình.");
            foreach (var line in info.Text.Split('\n')) parser.HandleLine(line, info);
        }
        parser.Finish();
        parser.ApplyAnswerKey();
        return parser._result;
    }

    /// <summary>Heading: "ĐÁP ÁN..." mở khối đáp án; heading khác là tên chủ đề; Title của tài liệu bỏ qua.</summary>
    private bool TryHandleHeading(string text, int level)
    {
        // Có người định dạng cả dòng "Câu 1" bằng Heading: coi như dòng thường
        if (text.Length == 0 || QuestionStart().IsMatch(text)) return false;

        Finish();
        if (IsAnswerKeyTitle(text)) _inAnswerKey = true;
        else if (level > 0)
        {
            _inAnswerKey = false;
            _topic = text;
        }
        return true;
    }

    private void HandleLine(string line, ParagraphInfo info)
    {
        if (_inAnswerKey)
        {
            HandleAnswerKeyLine(line);
            return;
        }

        // Đang trong khối ``` do người dùng tự gõ: giữ nguyên từng dòng
        if (_inFence)
        {
            _current?.Lines?.Add(line.TrimEnd());
            if (line.TrimStart().StartsWith("```")) _inFence = false;
            return;
        }

        if (info.IsCode && _current?.Lines != null)
        {
            _code.Add(line.TrimEnd());
            return;
        }

        if (string.IsNullOrWhiteSpace(line))
        {
            // Dòng trống giữa các đoạn code vẫn thuộc khối code
            if (_code.Count > 0) _code.Add("");
            else _current?.Lines?.Add("");
            return;
        }

        FlushCode();
        var text = line.Trim();

        if (text.StartsWith("```"))
        {
            if (_current?.Lines is { } lines)
            {
                lines.Add(text);
                _inFence = true;
            }
            return;
        }

        // Dòng in đậm "ĐÁP ÁN" (không dùng Heading) cũng mở khối đáp án cuối file
        if (info.IsEmphasized && IsAnswerKeyTitle(text))
        {
            Finish();
            _inAnswerKey = true;
            return;
        }

        if (TryMeta(text, out var key, out var value))
        {
            HandleMeta(key, value);
            return;
        }

        var question = QuestionStart().Match(text);
        if (question.Success)
        {
            Finish();
            _current = new QuestionBuilder(int.Parse(question.Groups[1].Value), _subject, _topic, _defaultDifficulty);
            var rest = question.Groups[2].Value.Trim();
            if (rest.Length > 0) _current.Content.Add(rest);
            return;
        }

        if (_current is { Target: Target.Content or Target.Option } && TrySplitOptions(text, _current.NextLetter, out var options))
        {
            foreach (var (letter, marked, content) in options)
                _current.AddOption(new ParsedOption { Letter = letter, Marked = marked, Emphasized = info.IsEmphasized && options.Count == 1 }, content);
            return;
        }

        // Dòng trước câu đầu tiên (tiêu đề, hướng dẫn) bị bỏ qua
        _current?.Lines?.Add(text);
    }

    private void HandleMeta(MetaKey key, string value)
    {
        switch (key)
        {
            case MetaKey.Subject:
                Finish();
                _subject = value;
                _topic = null;
                break;
            case MetaKey.Topic:
                Finish();
                _topic = value;
                break;
            case MetaKey.Language:
                _language = ImportText.NormalizeLanguage(value);
                break;
            case MetaKey.Difficulty:
                if (_current != null) _current.Question.DifficultyText = value;
                else _defaultDifficulty = value;
                break;
            case MetaKey.Tags:
                _current?.Question.Tags.AddRange(ImportText.SplitTags(value));
                break;
            case MetaKey.Type:
                if (_current != null) _current.Question.TypeText = value;
                break;
            case MetaKey.Answer:
                if (_current != null)
                {
                    _current.Question.AnswerText = value;
                    _current.Target = Target.None;
                }
                break;
            case MetaKey.Explanation:
                if (_current != null)
                {
                    _current.Target = Target.Explanation;
                    if (value.Length > 0) _current.Explanation.Add(value);
                }
                break;
        }
    }

    private void FlushCode()
    {
        while (_code.Count > 0 && string.IsNullOrWhiteSpace(_code[^1])) _code.RemoveAt(_code.Count - 1);
        if (_code.Count > 0 && _current?.Lines is { } lines)
        {
            var code = string.Join('\n', _code);
            var language = _language ?? ImportText.GuessLanguage(_subject, _topic);
            if (language.Length == 0) language = ImportText.GuessLanguageFromCode(code);
            lines.Add($"```{language}\n{code}\n```");
        }
        _code.Clear();
    }

    private void Finish()
    {
        FlushCode();
        if (_current != null)
        {
            var question = _current.Build();
            _result.Add(question);
            _numbered.Add((_current.Number, question));
        }
        _current = null;
        _inFence = false;
    }

    /// <summary>Một dòng trong khối đáp án: "Câu 1 — A. giải thích", "1. A", "Câu 4: B, D"; dòng khác nối vào giải thích trước đó.</summary>
    private void HandleAnswerKeyLine(string line)
    {
        var text = line.Trim();
        if (text.Length == 0) return;

        var match = AnswerKeyLine().Match(text);
        if (match.Success)
        {
            var letters = ImportText.ParseAnswerLetters(match.Groups[2].Value.Replace("và", ","));
            if (letters.Count > 0)
            {
                _lastKey = new AnswerKeyEntry(string.Join(", ", letters), []);
                _answerKey[int.Parse(match.Groups[1].Value)] = _lastKey;
                var rest = match.Groups[3].Value.Trim();
                if (rest.Length > 0) _lastKey.Explanation.Add(rest);
                return;
            }
        }
        _lastKey?.Explanation.Add(text);
    }

    /// <summary>Ghép đáp án cuối file vào câu cùng số. Đáp án ghi ngay trong câu (dòng "Đáp án:" hoặc dấu *) được ưu tiên.</summary>
    private void ApplyAnswerKey()
    {
        if (_answerKey.Count == 0) return;

        var byNumber = _numbered.GroupBy(x => x.Number).ToDictionary(g => g.Key, g => g.Select(x => x.Question).ToList());
        foreach (var (number, entry) in _answerKey)
        {
            if (!byNumber.TryGetValue(number, out var questions)) continue;
            if (questions.Count > 1)
            {
                foreach (var q in questions)
                    q.Warnings.Add("Có nhiều câu cùng số nên không ghép được đáp án ở cuối file.");
                continue;
            }

            var question = questions[0];
            if (question.AnswerText == null && !question.Options.Any(o => o.Marked)) question.AnswerText = entry.Letters;
            if (question.Explanation == null && entry.Explanation.Count > 0) question.Explanation = ImportText.JoinLines(entry.Explanation);
        }
    }

    private static bool IsAnswerKeyTitle(string text) => AnswerKeyTitle().IsMatch(ImportText.Key(text).TrimEnd('.', ':'));

    private static bool TryMeta(string text, out MetaKey key, out string value)
    {
        key = default;
        value = "";
        var colon = text.IndexOf(':');
        if (colon <= 0 || colon > 20) return false;
        if (!MetaKeys.TryGetValue(ImportText.Key(text[..colon]), out key)) return false;
        value = text[(colon + 1)..].Trim();
        return true;
    }

    /// <summary>
    /// Tách một dòng thành các đáp án. Hỗ trợ "A. ..." và nhiều đáp án trên một dòng ("A. 10    B. 11"),
    /// với điều kiện chữ cái liên tiếp bắt đầu từ chữ cái kế tiếp và giữa các đáp án có tab/nhiều dấu cách.
    /// </summary>
    private static bool TrySplitOptions(string text, char expected, out List<(char Letter, bool Marked, string Content)> options)
    {
        options = [];
        var boundaries = new List<(int Start, int ContentStart, char Letter, bool Marked)>();
        foreach (Match m in OptionMarker().Matches(text))
        {
            var letter = char.ToUpperInvariant(m.Groups[2].Value[0]);
            if (letter != expected) continue;
            var isFirst = boundaries.Count == 0;
            if (isFirst && m.Index != 0) return false;
            if (!isFirst && (m.Index < 2 || !char.IsWhiteSpace(text[m.Index - 1]) || !char.IsWhiteSpace(text[m.Index - 2]))) continue;
            boundaries.Add((m.Index, m.Index + m.Length, letter, m.Groups[1].Length + m.Groups[3].Length > 0));
            expected++;
        }
        if (boundaries.Count == 0) return false;

        for (var i = 0; i < boundaries.Count; i++)
        {
            var end = i + 1 < boundaries.Count ? boundaries[i + 1].Start : text.Length;
            var b = boundaries[i];
            options.Add((b.Letter, b.Marked, text[b.ContentStart..end].Trim()));
        }
        return true;
    }

    private ParagraphInfo Read(Paragraph paragraph)
    {
        var text = new StringBuilder();
        var paragraphStyle = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        var paragraphFont = ResolveStyleFont(paragraphStyle);
        int textRuns = 0, codeRuns = 0, emphasizedRuns = 0;

        foreach (var run in paragraph.Descendants<Run>())
        {
            var runText = new StringBuilder();
            foreach (var child in run.ChildElements)
            {
                switch (child)
                {
                    case Text t: runText.Append(t.Text); break;
                    case TabChar: runText.Append("    "); break;
                    case Break or CarriageReturn: runText.Append('\n'); break;
                }
            }
            if (runText.Length == 0) continue;
            text.Append(runText);
            if (string.IsNullOrWhiteSpace(runText.ToString())) continue;

            textRuns++;
            var props = run.RunProperties;
            var runStyle = props?.RunStyle?.Val?.Value;
            var font = props?.RunFonts?.Ascii?.Value ?? props?.RunFonts?.HighAnsi?.Value ?? ResolveStyleFont(runStyle) ?? paragraphFont;
            if (IsMonospace(font) || IsCodeStyle(runStyle)) codeRuns++;
            if (IsEmphasized(props)) emphasizedRuns++;
        }

        var isCode = IsCodeStyle(paragraphStyle) || (textRuns > 0 && codeRuns == textRuns);
        var hasImage = paragraph.Descendants<Drawing>().Any() || paragraph.Descendants<Picture>().Any();
        return new ParagraphInfo(text.ToString(), isCode, textRuns > 0 && emphasizedRuns == textRuns, hasImage,
            HeadingLevel(paragraph, paragraphStyle));
    }

    private int? HeadingLevel(Paragraph paragraph, string? styleId)
    {
        var outline = paragraph.ParagraphProperties?.OutlineLevel?.Val?.Value;
        if (styleId != null)
        {
            _styles.TryGetValue(styleId, out var style);
            var name = style?.StyleName?.Val?.Value ?? styleId;
            if (name.Equals("Title", StringComparison.OrdinalIgnoreCase)) return 0;
            var heading = HeadingStyleName().Match(name);
            if (heading.Success) return int.Parse(heading.Groups[1].Value);
            outline ??= style?.StyleParagraphProperties?.OutlineLevel?.Val?.Value;
        }
        // outlineLvl đánh số từ 0; 9 = văn bản thường
        return outline is >= 0 and < 9 ? outline + 1 : null;
    }

    private string? ResolveStyleFont(string? styleId)
    {
        for (var depth = 0; styleId != null && depth < 10; depth++)
        {
            if (!_styles.TryGetValue(styleId, out var style)) return null;
            var fonts = style.StyleRunProperties?.RunFonts;
            if ((fonts?.Ascii?.Value ?? fonts?.HighAnsi?.Value) is { } font) return font;
            styleId = style.BasedOn?.Val?.Value;
        }
        return null;
    }

    private bool IsCodeStyle(string? styleId) =>
        styleId != null && (CodeStyleHints.Any(h => styleId.Contains(h, StringComparison.OrdinalIgnoreCase)) || IsMonospace(ResolveStyleFont(styleId)));

    private static bool IsMonospace(string? font) =>
        font != null && MonospaceFonts.Any(m => font.Contains(m, StringComparison.OrdinalIgnoreCase));

    private static bool IsEmphasized(RunProperties? props)
    {
        if (props == null) return false;
        var bold = props.Bold != null && (props.Bold.Val == null || props.Bold.Val.Value);
        var underline = props.Underline?.Val != null && props.Underline.Val.Value != UnderlineValues.None;
        var highlight = props.Highlight?.Val != null && props.Highlight.Val.Value != HighlightColorValues.None;
        return bold || underline || highlight || IsRed(props.Color?.Val?.Value);
    }

    private static bool IsRed(string? hex)
    {
        if (hex is not { Length: 6 } || !int.TryParse(hex, NumberStyles.HexNumber, null, out var rgb)) return false;
        int r = (rgb >> 16) & 0xFF, g = (rgb >> 8) & 0xFF, b = rgb & 0xFF;
        return r >= 0xB0 && g < 0x70 && b < 0x70;
    }

    // "Câu 1:", "Câu 12.", "CÂU 3)", "Question 4 -"
    [GeneratedRegex(@"^(?:c[âa]u|question)\s*(\d+)\s*[:.)\-–]?\s*(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex QuestionStart();

    // "Câu 1 — A. giải thích", "1. A", "Câu 4: B, D", "12 - Đáp án C"
    [GeneratedRegex(@"^(?:c[âa]u\s*)?(\d+)\s*[—–\-:.)]?\s*(?:đáp án\s*:?\s*)?([A-J](?:\s*(?:,|;|&|và)\s*[A-J])*)(?![\p{L}\p{N}])[\s.:)\-–—]*(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex AnswerKeyLine();

    // Tiêu đề khối đáp án (đã bỏ dấu): "ĐÁP ÁN", "BẢNG ĐÁP ÁN", "ĐÁP ÁN VÀ GIẢI THÍCH", "Answer key"
    [GeneratedRegex(@"^(phan\s+)?(bang\s+)?dap an(\s+(va|&)\s+(giai thich|loi giai))?(\s+(chi tiet|tham khao))?$|^(answer key|answers)$")]
    private static partial Regex AnswerKeyTitle();

    [GeneratedRegex(@"^heading\s*(\d)$", RegexOptions.IgnoreCase)]
    private static partial Regex HeadingStyleName();

    // "A.", "b)", "*C.", "D.*"
    [GeneratedRegex(@"(?<![\p{L}\p{N}])(\*?)([A-Ja-j])[.)](\*?)(?:\s+|$)")]
    private static partial Regex OptionMarker();

    private sealed class QuestionBuilder(int number, string? subject, string? topic, string? difficulty)
    {
        public int Number { get; } = number;

        public ParsedQuestion Question { get; } = new()
        {
            Source = $"Câu {number}",
            SubjectName = subject,
            TopicName = topic,
            DifficultyText = difficulty,
        };

        public Target Target { get; set; } = Target.Content;
        public List<string> Content { get; } = [];
        public List<string> Explanation { get; } = [];
        private readonly List<(ParsedOption Option, List<string> Lines)> _options = [];

        public char NextLetter => (char)('A' + _options.Count);

        /// <summary>Danh sách dòng đang nhận nội dung, null nếu đang ở sau dòng "Đáp án:".</summary>
        public List<string>? Lines => Target switch
        {
            Target.Content => Content,
            Target.Option => _options[^1].Lines,
            Target.Explanation => Explanation,
            _ => null,
        };

        public void AddOption(ParsedOption option, string firstLine)
        {
            _options.Add((option, firstLine.Length > 0 ? [firstLine] : []));
            Target = Target.Option;
        }

        public void Warn(string message)
        {
            if (!Question.Warnings.Contains(message)) Question.Warnings.Add(message);
        }

        public ParsedQuestion Build()
        {
            Question.Content = ImportText.JoinLines(Content);
            var explanation = ImportText.JoinLines(Explanation);
            Question.Explanation = explanation.Length > 0 ? explanation : null;
            foreach (var (option, lines) in _options) option.Content = ImportText.JoinLines(lines);
            Question.Options = _options.Select(o => o.Option).ToList();
            return Question;
        }
    }
}
