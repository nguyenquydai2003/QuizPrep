export type Role = 'Admin' | 'User'
export type QuestionType = 'SingleChoice' | 'MultipleChoice'
export type Difficulty = 'Easy' | 'Medium' | 'Hard'

export interface User {
  id: string
  userName: string
  displayName: string
  role: Role
}

export interface LoginResponse {
  token: string
  expiresAt: string
  user: User
}

export interface Topic {
  id: number
  name: string
  sortOrder: number
  questionCount: number
}

export interface Subject {
  id: number
  name: string
  description: string | null
  sortOrder: number
  questionCount: number
  topics: Topic[]
}

export interface SaveSubjectRequest {
  name: string
  description: string | null
  sortOrder: number
}

export interface SaveTopicRequest {
  name: string
  sortOrder: number
}

export interface QuestionListItem {
  id: number
  topicId: number
  topicName: string
  subjectName: string
  type: QuestionType
  difficulty: Difficulty
  contentPreview: string
  tags: string[]
  updatedAt: string
}

export interface QuestionOption {
  id: number
  content: string
  isCorrect: boolean
  sortOrder: number
}

export interface QuestionDetail {
  id: number
  subjectId: number
  topicId: number
  type: QuestionType
  difficulty: Difficulty
  content: string
  explanation: string | null
  tags: string[]
  options: QuestionOption[]
  createdAt: string
  updatedAt: string
}

export interface SaveQuestionRequest {
  topicId: number
  type: QuestionType
  difficulty: Difficulty
  content: string
  explanation: string | null
  tags: string[]
  options: { content: string; isCorrect: boolean }[]
}

export interface QuestionQuery {
  subjectId?: number
  topicId?: number
  difficulty?: Difficulty
  type?: QuestionType
  search?: string
  tag?: string
  page: number
  pageSize: number
}

export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

// ---------- Tài khoản ----------
export interface AuthOptions {
  allowRegistration: boolean
}

export interface UserAdmin {
  id: string
  userName: string
  displayName: string
  role: Role
  createdAt: string
  attemptCount: number
  answeredCount: number
  lastActivityAt: string | null
}

// ---------- Import ----------
export interface ImportItem {
  index: number
  source: string
  subjectName: string | null
  topicName: string | null
  type: QuestionType
  difficulty: Difficulty
  content: string
  explanation: string | null
  tags: string[]
  options: { content: string; isCorrect: boolean }[]
  errors: string[]
  warnings: string[]
  topicId: number | null
  createsSubject: boolean
  createsTopic: boolean
  duplicateOfQuestionId: number | null
  duplicateOfIndex: number | null
}

export interface ImportPreview {
  fileName: string
  items: ImportItem[]
}

export interface ImportCommitResult {
  created: number
  skipped: number
  subjectsCreated: number
  topicsCreated: number
  skippedReasons: string[]
}

// ---------- Đề thi ----------
export interface ExamRule {
  id: number
  subjectId: number | null
  subjectName: string | null
  topicId: number | null
  topicName: string | null
  difficulty: Difficulty | null
  questionCount: number
  availableCount: number
}

export interface ExamTemplate {
  id: number
  title: string
  description: string | null
  durationMinutes: number
  passScore: number
  shuffleOptions: boolean
  isPublished: boolean
  totalQuestions: number
  hasEnoughQuestions: boolean
  rules: ExamRule[]
  updatedAt: string
}

export interface SaveExamTemplateRequest {
  title: string
  description: string | null
  durationMinutes: number
  passScore: number
  shuffleOptions: boolean
  isPublished: boolean
  rules: { subjectId: number | null; topicId: number | null; difficulty: Difficulty | null; questionCount: number }[]
}

export interface AvailableExam {
  id: number
  title: string
  description: string | null
  durationMinutes: number
  passScore: number
  totalQuestions: number
  attemptCount: number
  bestScore: number | null
  lastScore: number | null
}

// ---------- Làm bài ----------
export type AttemptMode = 'Practice' | 'Exam' | 'Review'
export type AttemptStatus = 'InProgress' | 'Submitted'

export interface AttemptOption {
  key: number
  content: string
  isCorrect: boolean | null
}

export interface AttemptQuestion {
  id: number
  order: number
  type: QuestionType
  difficulty: Difficulty
  subjectName: string
  topicName: string
  content: string
  options: AttemptOption[]
  selectedKeys: number[]
  flagged: boolean
  revealed: boolean
  isCorrect: boolean | null
  explanation: string | null
  timeSpentSeconds: number
}

export interface AttemptSummary {
  id: string
  mode: AttemptMode
  status: AttemptStatus
  title: string
  startedAt: string
  deadline: string | null
  submittedAt: string | null
  totalQuestions: number
  answeredCount: number
  correctCount: number
  score: number | null
  passScore: number | null
  passed: boolean | null
}

export interface Attempt extends AttemptSummary {
  examTemplateId: number | null
  serverNow: string
  questions: AttemptQuestion[]
}

export interface PracticeRequest {
  subjectId?: number
  topicId?: number
  difficulty?: Difficulty
  count: number
  onlyUnseen: boolean
}

export interface ReviewSummary {
  dueCount: number
  wrongQuestionCount: number
}

// ---------- Thống kê ----------
export interface StatsOverview {
  displayName: string
  questionBankSize: number
  distinctAnswered: number
  coveragePercent: number
  totalAnswered: number
  totalCorrect: number
  accuracyPercent: number
  avgSecondsPerQuestion: number
  examsTaken: number
  examsPassed: number
  bestExamScore: number | null
  avgExamScore: number | null
  currentStreak: number
  longestStreak: number
  dueReviewCount: number
  scoreTrend: { attemptId: string; title: string; submittedAt: string; score: number; passScore: number | null }[]
  topics: { topicId: number; subjectName: string; topicName: string; answered: number; correct: number; accuracy: number }[]
  activity: { date: string; count: number }[]
  topWrong: { questionId: number; contentPreview: string; topicName: string; wrongCount: number; answeredCount: number }[]
  inProgress: AttemptSummary[]
  recent: AttemptSummary[]
}

export interface AiStatus {
  enabled: boolean
  model: string | null
}

export interface ProblemDetails {
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
}
