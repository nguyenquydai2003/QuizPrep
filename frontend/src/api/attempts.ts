import http from './http'
import type {
  Attempt,
  AttemptMode,
  AttemptQuestion,
  AttemptStatus,
  AttemptSummary,
  PagedResult,
  PracticeRequest,
  ReviewSummary,
} from './types'

export const attemptsApi = {
  startPractice: (body: PracticeRequest) => http.post<Attempt>('/attempts/practice', body).then((r) => r.data),
  startExam: (examTemplateId: number) => http.post<Attempt>('/attempts/exam', { examTemplateId }).then((r) => r.data),
  startReview: (body: { subjectId?: number; count: number }) =>
    http.post<Attempt>('/attempts/review', body).then((r) => r.data),
  reviewSummary: () => http.get<ReviewSummary>('/attempts/review-summary').then((r) => r.data),
  list: (params: { mode?: AttemptMode; status?: AttemptStatus; page: number; pageSize: number }) =>
    http.get<PagedResult<AttemptSummary>>('/attempts', { params }).then((r) => r.data),
  get: (id: string) => http.get<Attempt>(`/attempts/${id}`).then((r) => r.data),
  answer: (id: string, questionId: number, selectedKeys: number[], timeSpentSeconds: number) =>
    http
      .put<AttemptQuestion>(`/attempts/${id}/questions/${questionId}/answer`, { selectedKeys, timeSpentSeconds })
      .then((r) => r.data),
  flag: (id: string, questionId: number, flagged: boolean) =>
    http.put<AttemptQuestion>(`/attempts/${id}/questions/${questionId}/flag`, { flagged }).then((r) => r.data),
  submit: (id: string) => http.post<Attempt>(`/attempts/${id}/submit`).then((r) => r.data),
  remove: (id: string) => http.delete(`/attempts/${id}`).then(() => undefined),
}
