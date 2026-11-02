# QuizPrep – Luyện thi trắc nghiệm

Web app luyện thi trắc nghiệm tuyển dụng: **ASP.NET Core (.NET 9) + EF Core + SQL Server** và **Vue 3 + Vite + TypeScript + Element Plus**.

## Tính năng

**Học viên**
- **Tổng quan**: độ chính xác, độ phủ ngân hàng, chuỗi ngày học, biểu đồ điểm thi thử, độ chính xác theo chủ đề (bấm để luyện chủ đề yếu), heatmap ngày học, câu hay sai, bài đang làm dở.
- **Luyện tập**: chọn môn/chủ đề, độ khó, số câu, "chỉ câu chưa làm"; xem đáp án và giải thích ngay sau mỗi câu.
- **Thi thử**: đề sinh ngẫu nhiên theo ma trận, đồng hồ tính theo server (F5 không reset), tự lưu đáp án, đánh dấu câu, tự nộp khi hết giờ, chấm điểm và xem lại bài.
- **Ôn câu sai**: lặp lại ngắt quãng theo hộp Leitner (sai về hộp 1 ôn ngay; đúng lên hộp, giãn 1/3/7/14 ngày).
- Lịch sử làm bài, đổi tên hiển thị và mật khẩu.
- Phím tắt khi làm bài: `A`–`J` / `1`–`9` chọn đáp án, `←` `→` chuyển câu, `Enter` kiểm tra/câu tiếp, `F` đánh dấu.

**Quản trị**
- Môn & chủ đề, ngân hàng câu hỏi (Markdown + tô màu code, một/nhiều đáp án, độ khó, tag, giải thích), tự phát hiện câu trùng.
- **Import Excel/Word** có xem trước, báo lỗi từng câu, đánh dấu trùng, tự tạo môn/chủ đề mới; tải file mẫu ngay trong trang.
- **Đề thi theo ma trận** (môn/chủ đề × độ khó × số câu), cảnh báo khi ngân hàng không đủ câu.
- **Người dùng**: tạo, phân quyền, đặt lại mật khẩu, xem tiến độ từng người.
- **AI gợi ý lời giải thích** (Claude) – tùy chọn, chỉ bật khi có API key.

## Yêu cầu

- .NET SDK 9 trở lên (khuyến nghị .NET 10 LTS)
- Node.js 24 LTS
- SQL Server (mặc định `.\SQLEXPRESS`, Windows Authentication)

## Chạy dự án

**Backend** (http://localhost:5080, tài liệu API tại `/scalar/v1`):

```bash
cd backend/QuizPrep.Api
dotnet run
```

Lần chạy đầu ở môi trường Development sẽ tự tạo database `QuizPrep`, tài khoản admin, câu hỏi và đề thi mẫu.

**Frontend** (http://localhost:5173, gọi `/api` qua proxy của Vite):

```bash
cd frontend
npm install
npm run dev
```

Đăng nhập: `admin` / `Admin@123` (cấu hình trong `appsettings.Development.json`).

## Cấu hình (`backend/QuizPrep.Api/appsettings.json`)

| Khóa | Ý nghĩa |
|---|---|
| `ConnectionStrings:Default` | Chuỗi kết nối SQL Server |
| `Jwt:SigningKey` | Khóa ký JWT, tối thiểu 32 ký tự (bản dev đặt trong `appsettings.Development.json`) |
| `Auth:AllowRegistration` | `true` để người khác tự đăng ký tài khoản (mặc định `false`) |
| `Anthropic:ApiKey` | API key Claude để bật "Gợi ý bằng AI" (hoặc đặt biến môi trường `ANTHROPIC_API_KEY`) |
| `Anthropic:Model` | Model dùng cho AI, mặc định `claude-opus-5-5` |

Không commit API key vào git; nên dùng `dotnet user-secrets set "Anthropic:ApiKey" "<key>"` trong thư mục `backend/QuizPrep.Api`.

## Định dạng file import

**Excel** (sheet đầu tiên): cột `Môn`, `Chủ đề`, `Độ khó`, `Câu hỏi`, `A`…`J`, `Đáp án` (`B` hoặc `A, C`), `Giải thích`, `Tags`. Code trong ô bọc bằng ```` ```csharp ... ``` ````.

**Word**:

```
Môn: Lập trình C#
Chủ đề: Cơ bản
Câu 1: Đoạn code sau in ra gì?
int x = 5;                          ← đoạn font Consolas/Courier = code
Console.WriteLine(x++ + ++x);
A. 10
B. 11
*C. 12                              ← dấu * = đáp án đúng
D. 13
Độ khó: Trung bình
Giải thích: ...
```

Đáp án đúng có thể đánh dấu bằng `*`, dòng `Đáp án: C` hoặc in đậm/gạch chân/tô đỏ cả đáp án; hoặc gom ở mục **"ĐÁP ÁN" / "ĐÁP ÁN VÀ GIẢI THÍCH"** cuối file (Heading hoặc dòng in đậm), mỗi dòng dạng `Câu 1 — A. giải thích`, `1. A` hay `Câu 4: B, D` — ghép theo số câu, nên số trong mục đáp án phải khớp số câu hỏi. Đoạn dùng style **Heading** được lấy làm tên chủ đề. Ngôn ngữ của khối code được đoán từ tên môn/chủ đề hoặc từ chính nội dung code. Nhiều đáp án trên một dòng (cách nhau bằng Tab) vẫn đọc được. Hãy tắt đánh số tự động (Numbering) để chữ "Câu 1", "A." là chữ thật.

## Cấu trúc

```
backend/QuizPrep.Api
├─ Domain/            # Entity: User, Subject, Topic, Question, ExamTemplate, Attempt, UserQuestionStat
├─ Data/              # AppDbContext, migrations, seed dữ liệu
├─ Features/          # Auth, Users, Subjects, Questions, Import, Exams, Attempts, Stats, Ai
└─ Common/
frontend/src
├─ api/               # Gọi API + kiểu dữ liệu
├─ components/        # QuestionCard, StatsDashboard, Markdown editor/view, charts/
├─ layouts/           # UserLayout (học viên), AdminLayout
├─ views/             # Trang học viên; views/admin: trang quản trị
├─ stores/            # Pinia (auth)
└─ utils/             # Markdown + highlight code, nhãn, định dạng, xử lý lỗi
```

## Lệnh hay dùng

```bash
# Tạo migration khi đổi entity (dotnet-ef được cài dạng local tool)
cd backend
dotnet tool restore
dotnet ef migrations add <TenMigration> --project QuizPrep.Api --output-dir Data/Migrations

# Kiểm tra kiểu + build frontend
cd frontend && npm run build
```
