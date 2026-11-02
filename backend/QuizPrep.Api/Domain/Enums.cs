namespace QuizPrep.Api.Domain;

public enum QuestionType
{
    SingleChoice = 1,
    MultipleChoice = 2,
}

public enum Difficulty
{
    Easy = 1,
    Medium = 2,
    Hard = 3,
}

public enum AttemptMode
{
    Practice = 1,
    Exam = 2,
    Review = 3,
}

public enum AttemptStatus
{
    InProgress = 1,
    Submitted = 2,
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string User = "User";
}
